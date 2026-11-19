const admin = require("firebase-admin");
admin.initializeApp({
  credential: admin.credential.cert(
    require("C:/Users/mutsa/Desktop/sbs-beauty-spa-repo/scripts/serviceAccountKey.json")
  ),
});
const db = admin.firestore();

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

const updates = {
  services: {
    "gelx-full-set":    GX1,
    "gel-manicure":     GX2,
    "soft-gel-overlay": GX3,
    "acrylic-full-set": AC1,
    "nail-art":         AC2,
  },
  products: {
    "po-pink-ombre":  PO3,
    "po-rhinestone":  PO1,
    "po-marble-gold": PO2,
    "po-chrome":      AC3,
  },
};

(async () => {
  const batch = db.batch();
  for (const [col, items] of Object.entries(updates))
    for (const [id, url] of Object.entries(items))
      batch.update(db.collection(col).doc(id), { imageUrl: url });
  await batch.commit();
  console.log("Images updated.");
  process.exit(0);
})().catch((e) => { console.error(e.message); process.exit(1); });