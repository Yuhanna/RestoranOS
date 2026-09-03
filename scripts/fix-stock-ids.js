/**
 * Fix specific stock IDs from verified Commons File titles.
 * Usage: node scripts/fix-stock-ids.js
 *        node scripts/fix-stock-ids.js menemen hummus cay
 */
const fs = require("fs");
const path = require("path");
const https = require("https");
const http = require("http");

const UA = "RestaurantOS-StockSync/1.0 (local demo; restaurant menu stock photos)";
const BASE = path.join(__dirname, "..", "apps", "api", "src", "RestaurantOS.Api", "media", "stock");
const MANIFEST_PATH = path.join(BASE, "manifest.json");
const DELAY_MS = 2500;

/** Verified Commons titles only — never use ambiguous names like File:Menemen.jpg */
const FIXES = {
  // desserts
  kazandibi: "Kazandibi Dessert.jpg",
  sutlac: "Sutlac.jpg",
  "panna-cotta": "Panna cotta.jpg",
  // drinks
  cay: "Turkish tea glass.jpg",
  "cay-bardak": "Turkish tea glass.jpg",
  "bitki-cayi": "Herbal tea.jpg",
  "sicak-cikolata": "Hot chocolate.jpg",
  smoothie: "Fruit smoothie.jpg",
  "soda-maden-suyu": "Glass of cold mineral water.jpg",
  salgam: "Salgam suyu.jpg",
  "vodka-tonic": "Vodka tonic.jpg",
  margarita: "Margarita with lime in a margarita glass - Evan Swigart.jpg",
  sarap: "Glass of red wine.jpg",
  greyfurt: "Self-made gin pink grapefruit long drink.jpg",
  // mains
  kumpir: "Kumpir - Turkish Cuisine.jpg",
  dolma: "Yaprak sarma.jpg",
  "balik-tava": "Fried fish.jpg",
  somon: "Grilled salmon.jpg",
  tacos: "Tacos.jpg",
  "tavuk-izgara": "Grilled chicken breast, Santo Domingo, La Palma.jpg",
  shawarma: "Shawarma Sandwich.jpg",
  "doner-durum-2": "Shawarma Sandwich.jpg",
  "iskender-4": "Iskender kebap.jpg",
  // starters
  hummus: "Hummus plate.jpg",
  bruschetta: "Bruschetta.jpg",
  "spring-rolls": "Spring rolls.jpg",
  "kalamar-2": "Fried calamari.jpg",
  // breakfast already fixed but keep map
  menemen: "Menemen in pan.jpg",
  granola: "Yogurt, fruit, granola bowl (34999358091).jpg",
  omlet: "A potato omelette.jpg",
  pankek: "Pancake with maple syrup 1.jpg",
  gozleme: "Gözleme.JPG",
};

/** After download, copy good files onto sibling variants */
const COPIES = {
  "doner-durum": ["wrap"],
  shawarma: ["doner-durum-2"],
  kalamar: ["kalamar-2"],
  iskender: ["iskender-2", "iskender-4"],
  coban: [],
};

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

function requestBuffer(url) {
  return new Promise((resolve, reject) => {
    const getter = url.startsWith("https") ? https : http;
    getter
      .get(url, { headers: { "User-Agent": UA, Accept: "image/*,application/json" } }, (response) => {
        if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
          requestBuffer(response.headers.location).then(resolve).catch(reject);
          return;
        }
        if (response.statusCode === 429) {
          reject(new Error("HTTP 429"));
          return;
        }
        if (response.statusCode !== 200) {
          reject(new Error(`HTTP ${response.statusCode}`));
          return;
        }
        const chunks = [];
        response.on("data", (c) => chunks.push(c));
        response.on("end", () => resolve(Buffer.concat(chunks)));
        response.on("error", reject);
      })
      .on("error", reject);
  });
}

function isImage(buffer) {
  if (buffer.length < 12) return false;
  return (
    (buffer[0] === 0xff && buffer[1] === 0xd8) ||
    (buffer[0] === 0x89 && buffer[1] === 0x50) ||
    buffer.slice(0, 4).toString("ascii") === "RIFF"
  );
}

async function resolveThumb(fileName) {
  const params = new URLSearchParams({
    action: "query",
    format: "json",
    origin: "*",
    titles: `File:${fileName}`,
    prop: "imageinfo",
    iiprop: "url|mime",
    iiurlwidth: "960",
  });
  const data = JSON.parse(
    (await requestBuffer(`https://commons.wikimedia.org/w/api.php?${params}`)).toString("utf8"),
  );
  const page = Object.values(data?.query?.pages ?? {})[0];
  if (!page || page.missing) throw new Error(`missing file ${fileName}`);
  return page.imageinfo?.[0]?.thumburl || page.imageinfo?.[0]?.url;
}

async function main() {
  const ids = process.argv.slice(2);
  const targets = ids.length ? ids : Object.keys(FIXES);
  const manifest = JSON.parse(fs.readFileSync(MANIFEST_PATH, "utf8"));
  const byId = Object.fromEntries(manifest.photos.map((p) => [p.id, p]));

  let ok = 0;
  let fail = 0;

  for (const id of targets) {
    const fileName = FIXES[id];
    const photo = byId[id];
    if (!fileName || !photo) {
      console.log(`SKIP ${id}`);
      continue;
    }
    try {
      await sleep(DELAY_MS);
      const url = await resolveThumb(fileName);
      await sleep(DELAY_MS);
      const buffer = await requestBuffer(url.split("?")[0]);
      if (!isImage(buffer) || buffer.length < 8000) throw new Error(`bad image ${buffer.length}`);
      const dest = path.join(BASE, photo.file.replaceAll("/", path.sep));
      fs.writeFileSync(dest, buffer);
      photo.sourceUrl = url.split("?")[0];
      console.log(`OK ${id} <- ${fileName}`);
      ok++;
    } catch (e) {
      console.log(`FAIL ${id}: ${e.message}`);
      fail++;
      if (String(e.message).includes("429")) {
        console.log("Rate limited — waiting 45s…");
        await sleep(45_000);
      }
    }
  }

  // Copy sibling variants from already-good files
  const copies = [
    ["doner-durum", "wrap"],
    ["doner-durum", "doner-durum-2"],
    ["doner-durum", "shawarma"],
    ["kalamar", "kalamar-2"],
    ["iskender", "iskender-2"],
    ["iskender", "iskender-4"],
    ["cay", "cay-bardak"],
    ["patates-kizartmasi", "patates-kizartmasi-2"],
    ["patates-kizartmasi", "patates-kizartmasi-menu"],
  ];
  for (const [srcId, dstId] of copies) {
    const src = byId[srcId];
    const dst = byId[dstId];
    if (!src || !dst) continue;
    if (!(src.sourceUrl || "").includes("wikimedia") && !(src.sourceUrl || "").includes("wikipedia")) {
      continue;
    }
    const srcPath = path.join(BASE, src.file.replaceAll("/", path.sep));
    const dstPath = path.join(BASE, dst.file.replaceAll("/", path.sep));
    if (!fs.existsSync(srcPath)) continue;
    fs.copyFileSync(srcPath, dstPath);
    dst.sourceUrl = src.sourceUrl;
    console.log(`COPY ${srcId} -> ${dstId}`);
  }

  manifest.version = (manifest.version || 0) + 1;
  fs.writeFileSync(MANIFEST_PATH, JSON.stringify(manifest, null, 4), "utf8");
  console.log(`Done v${manifest.version}. OK=${ok} FAIL=${fail}`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
