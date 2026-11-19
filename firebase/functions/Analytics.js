/**
 * getAnalytics        (admin only)  Input: { range: "7d"|"30d"|"12m"|"custom", from?, to? }  (from/to = YYYY-MM-DD)
 *                     Output: totals, series (revenue per day/month), split, paymentKinds, bookingStatus, topServices, topProducts
 * exportRevenueReport (admin only)  Same input. Output: { fileName, base64 }  - an Excel (.xlsx) accounting workbook.
 *
 * Needs:  npm install exceljs   (inside firebase\functions)
 */

const { onCall, HttpsError } = require("./secure");
const admin = require("firebase-admin");
const { resolveRange, aggregate, round2, toDate } = require("./AnalyticsCore");

if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

function requireAdmin(request) {
  if (!request.auth) throw new HttpsError("unauthenticated", "Please log in first.");
  if (request.auth.token.admin !== true) throw new HttpsError("permission-denied", "Admins only.");
}

async function getAllById(collection, ids) {
  const out = new Map();
  const unique = [...new Set(ids.filter(Boolean))];
  for (let i = 0; i < unique.length; i += 300) {
    const refs = unique.slice(i, i + 300).map((id) => db.collection(collection).doc(id));
    const snaps = await db.getAll(...refs);
    snaps.forEach((s) => { if (s.exists) out.set(s.id, s.data()); });
  }
  return out;
}

// Loads everything the numbers need. Single-field range queries only, so no extra Firestore indexes.
async function loadData(input) {
  let window;
  try { window = resolveRange(input); } catch (e) { throw new HttpsError("invalid-argument", e.message); }

  const paySnap = await db.collection("payments")
    .where("paidAt", ">=", admin.firestore.Timestamp.fromDate(window.from))
    .where("paidAt", "<=", admin.firestore.Timestamp.fromDate(window.to))
    .get();
  const payments = paySnap.docs.map((d) => ({ id: d.id, ...d.data() })).filter((p) => p.status === "completed");

  const bookings = await getAllById("bookings", payments.map((p) => p.bookingId));
  const orders = await getAllById("orders", payments.map((p) => p.orderId));

  const fromDay = new Date(window.from.getTime() + 2 * 3600000).toISOString().slice(0, 10);
  const toDay = new Date(window.to.getTime() + 2 * 3600000).toISOString().slice(0, 10);
  const bSnap = await db.collection("bookings").where("date", ">=", fromDay).where("date", "<=", toDay).get();
  const windowBookings = bSnap.docs.map((d) => ({ id: d.id, ...d.data() }));

  return { window, payments, bookings, orders, windowBookings };
}

exports.getAnalytics = onCall(async (request) => {
  requireAdmin(request);
  const data = await loadData(request.data || {});
  const result = aggregate(data);
  return {
    range: data.window.range,
    bucket: data.window.bucket,
    from: data.window.from.toISOString(),
    to: data.window.to.toISOString(),
    ...result,
  };
});

async function clientNames(ids) {
  const names = new Map();
  const unique = [...new Set(ids.filter(Boolean))];
  for (let i = 0; i < unique.length; i += 100) {
    try {
      const res = await admin.auth().getUsers(unique.slice(i, i + 100).map((uid) => ({ uid })));
      res.users.forEach((u) => names.set(u.uid, u.displayName || u.email || u.uid));
    } catch (err) {
      console.error("clientNames lookup failed", err.message);
    }
  }
  return names;
}

exports.exportRevenueReport = onCall({ memory: "512MiB", timeoutSeconds: 120 }, async (request) => {
  requireAdmin(request);
  const ExcelJS = require("exceljs");
  const data = await loadData(request.data || {});
  const agg = aggregate(data);
  const names = await clientNames([
    ...data.payments.map((p) => p.clientId),
    ...[...data.orders.values()].map((o) => o.clientId),
    ...[...data.bookings.values()].map((b) => b.clientId),
  ]);

  const wb = new ExcelJS.Workbook();
  wb.creator = "SBS Beauty Spa";
  wb.created = new Date();
  const PURPLE = "FF6D3FE0";
  const money = '"R" #,##0.00';
  const head = (row) => {
    row.font = { bold: true, color: { argb: "FFFFFFFF" } };
    row.fill = { type: "pattern", pattern: "solid", fgColor: { argb: PURPLE } };
    row.alignment = { vertical: "middle" };
  };
  const dayText = (d) => new Date(d.getTime() + 2 * 3600000).toISOString().slice(0, 10);
  const fromText = dayText(data.window.from), toText = dayText(data.window.to);

  // 1. Summary
  const s = wb.addWorksheet("Summary");
  s.columns = [{ width: 34 }, { width: 22 }];
  s.addRow(["SBS Beauty Spa - Revenue report"]).font = { bold: true, size: 16, color: { argb: PURPLE } };
  s.addRow([`Period: ${fromText} to ${toText}`]);
  s.addRow([`Created: ${new Date().toISOString().slice(0, 16).replace("T", " ")} UTC`]);
  s.addRow([]);
  head(s.addRow(["Measure", "Amount"]));
  const rows = [
    ["Spa booking revenue", agg.totals.spa],
    ["Shop revenue", agg.totals.shop],
    ["Total revenue", null],
    ["Number of payments", agg.totals.payments],
    ["Average payment", null],
    ["Average per day", agg.totals.perDay],
  ];
  const first = s.rowCount + 1;
  rows.forEach((r) => s.addRow(r));
  s.getCell(`B${first + 2}`).value = { formula: `B${first}+B${first + 1}`, result: agg.totals.total };
  s.getCell(`B${first + 4}`).value = { formula: `IF(B${first + 3}=0,0,B${first + 2}/B${first + 3})`, result: agg.totals.averagePayment };
  [first, first + 1, first + 2, first + 4, first + 5].forEach((r) => (s.getCell(`B${r}`).numFmt = money));
  s.getRow(first + 2).font = { bold: true };
  s.addRow([]);
  s.addRow(["Amounts are Rands (ZAR), counted on the day each payment was received. Includes completed payments only."]).font = { italic: true, color: { argb: "FF6E63A6" } };

  // 2. Revenue by period (totals use formulas)
  const p = wb.addWorksheet("Revenue by period");
  p.columns = [{ width: 14 }, { width: 18 }, { width: 18 }, { width: 18 }];
  head(p.addRow([data.window.bucket === "day" ? "Day" : "Month", "Spa bookings", "Shop", "Total"]));
  agg.series.forEach((r, i) => {
    const n = i + 2;
    p.addRow([r.label, r.bookings, r.shop, { formula: `B${n}+C${n}`, result: round2(r.bookings + r.shop) }]);
  });
  const last = agg.series.length + 1;
  const tr = p.addRow(["Total", { formula: `SUM(B2:B${last})`, result: agg.totals.spa }, { formula: `SUM(C2:C${last})`, result: agg.totals.shop }, { formula: `SUM(D2:D${last})`, result: agg.totals.total }]);
  tr.font = { bold: true };
  p.eachRow((row, i) => { if (i > 1) [2, 3, 4].forEach((c) => (row.getCell(c).numFmt = money)); });
  p.views = [{ state: "frozen", ySplit: 1 }];

  // 3. Payments (every completed payment)
  const pay = wb.addWorksheet("Payments");
  pay.columns = [{ width: 18 }, { width: 14 }, { width: 22 }, { width: 28 }, { width: 16 }, { width: 18 }, { width: 28 }];
  head(pay.addRow(["Paid on", "Type", "Reference", "Client", "Amount", "For", "Details"]));
  const sorted = [...data.payments].sort((a, b) => toDate(a.paidAt) - toDate(b.paidAt));
  sorted.forEach((x) => {
    const bk = x.bookingId ? data.bookings.get(x.bookingId) : null;
    const od = x.orderId ? data.orders.get(x.orderId) : null;
    const details = bk ? ((bk.services || []).map((v) => v.name).join(", ") + (bk.date ? ` (${bk.date})` : "")) : od ? (od.items || []).map((v) => `${v.quantity} x ${v.name}`).join(", ") : "";
    pay.addRow([
      dayText(toDate(x.paidAt)), x.type || "", x.reference || "", names.get(x.clientId) || x.clientId || "",
      round2(x.amount), x.bookingId ? "Spa booking" : "Shop order", details,
    ]);
  });
  const pLast = pay.rowCount;
  const ptr = pay.addRow(["Total", "", "", "", { formula: `SUM(E2:E${Math.max(pLast, 2)})`, result: agg.totals.total }]);
  ptr.font = { bold: true };
  pay.eachRow((row, i) => { if (i > 1) row.getCell(5).numFmt = money; });
  pay.views = [{ state: "frozen", ySplit: 1 }];
  pay.autoFilter = { from: "A1", to: "G1" };

  // 4. Shop orders
  const so = wb.addWorksheet("Shop orders");
  so.columns = [{ width: 24 }, { width: 28 }, { width: 14 }, { width: 14 }, { width: 14 }, { width: 14 }, { width: 40 }];
  head(so.addRow(["Order", "Client", "Subtotal", "Delivery", "Total", "Courier", "Items"]));
  const seen = new Set();
  data.payments.filter((x) => x.orderId && !seen.has(x.orderId) && seen.add(x.orderId)).forEach((x) => {
    const o = data.orders.get(x.orderId); if (!o) return;
    so.addRow([x.orderId, names.get(o.clientId) || o.clientId || "", round2(o.subtotal), round2(o.shippingFee), round2(o.total),
      (o.deliveryService && o.deliveryService.name) || "", (o.items || []).map((v) => `${v.quantity} x ${v.name}`).join(", ")]);
  });
  so.eachRow((row, i) => { if (i > 1) [3, 4, 5].forEach((c) => (row.getCell(c).numFmt = money)); });
  so.views = [{ state: "frozen", ySplit: 1 }];

  // 5. Best sellers
  const t = wb.addWorksheet("Top items");
  t.columns = [{ width: 32 }, { width: 12 }, { width: 18 }];
  head(t.addRow(["Services (revenue shared by price)", "Bookings", "Revenue"]));
  agg.topServices.forEach((x) => t.addRow([x.name, x.count, x.revenue]));
  t.addRow([]);
  head(t.addRow(["Products", "Units", "Revenue"]));
  agg.topProducts.forEach((x) => t.addRow([x.name, x.count, x.revenue]));
  t.eachRow((row) => { const c = row.getCell(3); if (typeof c.value === "number") c.numFmt = money; });

  const buffer = await wb.xlsx.writeBuffer();
  return { fileName: `SBS-Revenue-${fromText}-to-${toText}.xlsx`, base64: Buffer.from(buffer).toString("base64") };
});
