const admin = require("firebase-admin");
admin.initializeApp({
  credential: admin.credential.cert(
    require("C:/Users/mutsa/Desktop/sbs-beauty-spa-repo/scripts/serviceAccountKey.json")
  ),
});
const db = admin.firestore();

const ph = (seed) => `https://picsum.photos/seed/${seed}/600/400`;

// Press-ons
const PO1 = "https://m.media-amazon.com/images/I/7112ZJvrFGL._AC_.jpg";
const PO2 = "https://tse4.mm.bing.net/th/id/OIP.l6L5bSsyMqnv9i7W2XhbLAHaHa?r=0&rs=1&pid=ImgDetMain&o=7&rm=3";
const PO3 = "https://i.etsystatic.com/45091564/r/il/5fba81/5996092510/il_1080xN.5996092510_gzqe.jpg";
// Acrylic nails
const AC1 = "https://tse3.mm.bing.net/th/id/OIP.QjWkXAsu8F2XgFjz_-XafAHaHX?r=0&rs=1&pid=ImgDetMain&o=7&rm=3";
const AC2 = "https://tse2.mm.bing.net/th/id/OIP.c79jFsX1b6a3ZQXWwIDBUQHaI0?r=0&w=1080&h=1286&rs=1&pid=ImgDetMain&o=7&rm=3";
const AC3 = "https://theglossychic.com/wp-content/uploads/2020/07/5E5DF661-BC0A-468A-A00E-4DDC73C49904.jpeg";
// Gel-X nails
const GX1 = "https://tse4.mm.bing.net/th/id/OIP.jKAv8QQBnGn8q7wr9PZXugHaHa?r=0&rs=1&pid=ImgDetMain&o=7&rm=3";
const GX2 = "https://belliata.com/uploads/s/siteadmin/2a2bcde2e9_caring-for-your-gel-x-nails.png";
const GX3 = "https://i.pinimg.com/originals/19/64/ff/1964ffa50058311bdbdb658de667ac52.jpg";

const serviceCategories = [
  { id: "nails",  name: "Nails",  description: "Gel-X, acrylic and gel polish", sortOrder: 1 },
  { id: "lashes", name: "Lashes", description: "Classic, volume and lash lifts", sortOrder: 2 },
];
const productCategories = [
  { id: "press-ons", name: "Press-On Nails", description: "Reusable handmade sets" },
  { id: "lashes",    name: "Lashes",         description: "Strip and cluster lashes" },
];

const now = admin.firestore.Timestamp.now();
const svc = (id, name, categoryId, price, deposit, mins, buffer, description, imageUrl) => ({
  id, name, categoryId, price, depositAmount: deposit, durationMinutes: mins,
  bufferMinutes: buffer, description, imageUrl, isActive: true, createdAt: now,
});
const services = [
  svc("gelx-full-set",    "Gel-X Full Set",      "nails",  450, 100, 120, 15, "Soft gel extensions in your choice of shape and length.", GX1),
  svc("acrylic-full-set", "Acrylic Full Set",    "nails",  380, 100, 105, 15, "Durable sculpted acrylic nails with a flawless finish.", AC1),
  svc("gel-manicure",     "Gel Polish Manicure", "nails",  180,  50,  60, 10, "Shape, cuticle care and long-lasting gel colour.", GX2),
  svc("soft-gel-overlay", "Soft Gel Overlay",    "nails",  300,  80,  90, 10, "Strengthening overlay on your natural nails.", GX3),
  svc("nail-art",         "Custom Nail Art",     "nails",  120,  30,  45, 10, "Hand-painted designs, chrome and 3D accents.", AC2),
  svc("classic-lashes",   "Classic Lash Set",    "lashes", 350, 100, 100, 10, "Natural, elegant one-to-one lash extensions.", ph("classic-lashes")),
  svc("volume-lashes",    "Volume Lash Set",     "lashes", 480, 120, 120, 10, "Fluffy handmade fans for a fuller look.", ph("volume-lashes")),
  svc("lash-lift",        "Lash Lift & Tint",    "lashes", 250,  60,  60, 10, "Lifted, curled and tinted natural lashes.", ph("lash-lift")),
  svc("lash-infill",      "Lash Infill",         "lashes", 220,  60,  60, 10, "Refresh your set every two to three weeks.", ph("lash-infill")),
];

const prod = (id, name, categoryId, price, stockQuantity, description, imageUrl) => ({
  id, name, categoryId, price, stockQuantity, description, imageUrl, isActive: true,
});
const products = [
  prod("po-pink-ombre",  "Pink Ombre Gem Set",    "press-ons", 220, 8,  "Handmade coffin press-ons with gold gems.", PO3),
  prod("po-rhinestone",  "Rhinestone French Set", "press-ons", 240, 6,  "Nude base, glitter tips and crystal accents.", PO1),
  prod("po-marble-gold", "Marble & Gold Set",     "press-ons", 230, 5,  "Lilac marble with gold foil and gems.", PO2),
  prod("po-chrome",      "Chrome Finish Set",     "press-ons", 220, 5,  "Mirror chrome in a long coffin shape.", AC3),
  prod("lash-strips-10", "Fluffy Strip Lashes (10 pair)", "lashes", 120, 20, "Dramatic wispy lashes, reusable.", ph("lash-strips-10")),
  prod("lash-clusters",  "DIY Cluster Lashes",    "lashes", 150, 15, "Easy at-home clusters in a mixed tray.", ph("lash-clusters")),
  prod("lash-natural-7", "Natural Strip Lashes (7 pair)", "lashes", 95, 25, "Soft everyday lashes.", ph("lash-natural-7")),
];

(async () => {
  const batch = db.batch();
  // remove the four blank leftover documents
  batch.delete(db.collection("serviceCategories").doc("p2qLPoLtVQ4m9n6ZS49N"));
  batch.delete(db.collection("productCategories").doc("XbenZmFlUstnfplqVJMe"));
  batch.delete(db.collection("services").doc("g6X9CUVV4Spcg8xuCi4k"));
  batch.delete(db.collection("products").doc("VqT6gIyTCySsZvWcH1Sr"));

  for (const { id, ...d } of serviceCategories) batch.set(db.collection("serviceCategories").doc(id), { ...d, isActive: true });
  for (const { id, ...d } of productCategories) batch.set(db.collection("productCategories").doc(id), { ...d, isActive: true });
  for (const { id, ...d } of services) batch.set(db.collection("services").doc(id), d);
  for (const { id, ...d } of products) batch.set(db.collection("products").doc(id), d);

  await batch.commit();
  console.log("Done:", serviceCategories.length, "service categories,", productCategories.length,
    "product categories,", services.length, "services,", products.length, "products.");
  process.exit(0);
})().catch((e) => { console.error(e.message); process.exit(1); });
