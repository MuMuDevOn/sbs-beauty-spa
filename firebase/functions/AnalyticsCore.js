/**
 * AnalyticsCore.js - pure number crunching for the admin Analytics page and the Excel report.
 * No Firebase in here, so it can be tested on its own. Not exported from index.js.
 *
 * Revenue = payments with status "completed", counted on the day they were paid (South Africa time).
 *   payment.bookingId -> spa (booking) revenue,  payment.orderId -> shop revenue.
 */

const SAST_OFFSET_MS = 2 * 60 * 60 * 1000; // South Africa is UTC+2 all year

const round2 = (n) => Math.round((Number(n) || 0) * 100) / 100;

// Shift a real instant so its UTC fields read as South African local time.
const local = (d) => new Date(d.getTime() + SAST_OFFSET_MS);
const pad = (n) => String(n).padStart(2, "0");
const dayKey = (d) => { const l = local(d); return `${l.getUTCFullYear()}-${pad(l.getUTCMonth() + 1)}-${pad(l.getUTCDate())}`; };
const monthKey = (d) => { const l = local(d); return `${l.getUTCFullYear()}-${pad(l.getUTCMonth() + 1)}`; };
const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

// Start of a South African calendar day, as a real instant.
function startOfDay(d) {
  const l = local(d);
  return new Date(Date.UTC(l.getUTCFullYear(), l.getUTCMonth(), l.getUTCDate()) - SAST_OFFSET_MS);
}
function endOfDay(d) { return new Date(startOfDay(d).getTime() + 24 * 3600 * 1000 - 1); }

/** Turns {range, from, to} from the app into a real window. */
function resolveRange(input, now = new Date()) {
  const range = (input && input.range) || "30d";
  let from, to = endOfDay(now);
  if (range === "7d") from = startOfDay(new Date(now.getTime() - 6 * 86400000));
  else if (range === "12m") {
    const l = local(now);
    from = new Date(Date.UTC(l.getUTCFullYear(), l.getUTCMonth() - 11, 1) - SAST_OFFSET_MS);
  } else if (range === "custom") {
    const f = new Date(`${input.from}T00:00:00+02:00`);
    const t = new Date(`${input.to}T00:00:00+02:00`);
    if (isNaN(f) || isNaN(t)) throw new Error("Please pick a valid start and end date.");
    if (t < f) throw new Error("The end date is before the start date.");
    from = startOfDay(f); to = endOfDay(t);
    if (to - from > 800 * 86400000) throw new Error("Please pick a range under about two years.");
  } else from = startOfDay(new Date(now.getTime() - 29 * 86400000));
  const days = Math.round((to - from) / 86400000);
  return { range, from, to, bucket: days > 62 ? "month" : "day" };
}

/** All bucket labels in the window, in order, so empty days still show as 0. */
function makeBuckets(from, to, bucket) {
  const out = [];
  if (bucket === "day") {
    for (let t = from.getTime(); t <= to.getTime(); t += 86400000) {
      const d = new Date(t); const l = local(d);
      out.push({ key: dayKey(d), label: `${l.getUTCDate()} ${MONTHS[l.getUTCMonth()]}`, bookings: 0, shop: 0 });
    }
  } else {
    const l0 = local(from), l1 = local(to);
    let y = l0.getUTCFullYear(), m = l0.getUTCMonth();
    while (y < l1.getUTCFullYear() || (y === l1.getUTCFullYear() && m <= l1.getUTCMonth())) {
      out.push({ key: `${y}-${pad(m + 1)}`, label: `${MONTHS[m]} ${String(y).slice(2)}`, bookings: 0, shop: 0 });
      m++; if (m > 11) { m = 0; y++; }
    }
  }
  return out;
}

const toDate = (v) => (v && typeof v.toDate === "function" ? v.toDate() : v instanceof Date ? v : v ? new Date(v) : null);

/**
 * payments: [{id, ...data}]   (completed, paidAt in window)
 * bookings: map id -> booking data     orders: map id -> order data
 * windowBookings: bookings whose appointment date is in the window (for the status pie)
 */
function aggregate({ window, payments, bookings, orders, windowBookings }) {
  const buckets = makeBuckets(window.from, window.to, window.bucket);
  const byKey = new Map(buckets.map((b) => [b.key, b]));
  const keyOf = window.bucket === "day" ? dayKey : monthKey;

  let spa = 0, shop = 0, spaCount = 0, shopCount = 0;
  const services = new Map(), products = new Map(), kinds = new Map();

  for (const p of payments) {
    const amt = round2(p.amount);
    const paid = toDate(p.paidAt);
    if (!paid || !amt) continue;
    const b = byKey.get(keyOf(paid));
    if (p.bookingId) {
      spa += amt; spaCount++;
      if (b) b.bookings = round2(b.bookings + amt);
      const kind = p.type === "deposit" ? "Deposits" : p.type === "balance" ? "Balances" : "Full payments";
      kinds.set(kind, round2((kinds.get(kind) || 0) + amt));
      // Split the payment across the booking's services in proportion to their price.
      const bk = bookings.get(p.bookingId);
      const list = (bk && bk.services) || [];
      const sum = list.reduce((s, x) => s + (Number(x.price) || 0), 0);
      for (const s of list) {
        const share = sum > 0 ? amt * ((Number(s.price) || 0) / sum) : amt / list.length;
        const cur = services.get(s.name || "Service") || { name: s.name || "Service", count: 0, revenue: 0 };
        cur.revenue = round2(cur.revenue + share);
        if (p.type !== "balance") cur.count += 1; // count each booking once (deposit or full, not both)
        services.set(cur.name, cur);
      }
    } else if (p.orderId) {
      shop += amt; shopCount++;
      if (b) b.shop = round2(b.shop + amt);
      const o = orders.get(p.orderId);
      const lines = (o && o.items) || [];
      const sum = lines.reduce((s, x) => s + (Number(x.lineTotal) || 0), 0);
      for (const l of lines) {
        const lineRev = sum > 0 ? round2(amt * ((Number(l.lineTotal) || 0) / sum)) : 0;
        const cur = products.get(l.name || "Product") || { name: l.name || "Product", count: 0, revenue: 0 };
        cur.count += Number(l.quantity) || 1;
        cur.revenue = round2(cur.revenue + lineRev);
        products.set(cur.name, cur);
      }
    }
  }

  const top = (m) => [...m.values()].sort((a, b) => b.revenue - a.revenue).slice(0, 8)
    .map((x) => ({ name: x.name, count: x.count, revenue: round2(x.revenue) }));

  const statusLabels = { confirmed: "Confirmed", completed: "Completed", pending: "Pending", cancelled: "Cancelled", "no-show": "No-show", noshow: "No-show" };
  const status = new Map();
  for (const b of windowBookings) {
    const label = statusLabels[String(b.status || "").toLowerCase()] || "Other";
    status.set(label, (status.get(label) || 0) + 1);
  }

  const total = round2(spa + shop);
  const days = Math.max(1, Math.round((window.to - window.from) / 86400000));
  return {
    totals: {
      total, spa: round2(spa), shop: round2(shop),
      payments: spaCount + shopCount, bookingPayments: spaCount, shopPayments: shopCount,
      averagePayment: spaCount + shopCount ? round2(total / (spaCount + shopCount)) : 0,
      perDay: round2(total / days),
      bookingsInRange: windowBookings.length,
    },
    series: buckets.map(({ label, bookings: b, shop: s }) => ({ label, bookings: round2(b), shop: round2(s) })),
    split: [{ label: "Spa bookings", value: round2(spa) }, { label: "Shop", value: round2(shop) }],
    paymentKinds: [...kinds.entries()].map(([label, value]) => ({ label, value })),
    bookingStatus: [...status.entries()].map(([label, value]) => ({ label, value })).sort((a, b) => b.value - a.value),
    topServices: top(services),
    topProducts: top(products),
  };
}

module.exports = { resolveRange, makeBuckets, aggregate, round2, dayKey, toDate, SAST_OFFSET_MS };
