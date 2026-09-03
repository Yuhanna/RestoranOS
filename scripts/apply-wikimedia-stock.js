/**
 * Replaces stock JPGs using verified Wikimedia Commons File titles.
 * Resolves thumbnails via the Commons API (avoids 429 on original upload URLs).
 *
 * Usage: node scripts/apply-wikimedia-stock.js
 */
const fs = require("fs");
const path = require("path");
const https = require("https");
const http = require("http");

const UA = "RestaurantOS-StockSync/1.0 (local demo; restaurant menu stock photos)";
const BASE = path.join(__dirname, "..", "apps", "api", "src", "RestaurantOS.Api", "media", "stock");
const MANIFEST_PATH = path.join(BASE, "manifest.json");
const MAP_PATH = path.join(__dirname, "wikimedia-stock-map.json");
const DELAY_MS = 700;

const SEARCH_FALLBACK = {
  kebap: "shish kebab plate",
  "kebap-2": "turkish kebab",
  "kebap-3": "adana kebab",
  "kebap-4": "shish kebab",
  kofte: "köfte meatballs turkey",
  "kofte-2": "turkish kofte",
  "kofte-4": "inegol kofte",
  "doner-durum": "döner dürüm",
  "doner-durum-2": "shawarma wrap",
  "doner-plate": "döner kebab plate",
  shawarma: "shawarma wrap",
  wrap: "dürüm wrap",
  gozleme: "gözleme",
  borek: "börek",
  "sigara-boregi": "sigara böreği",
  dolma: "yaprak sarma",
  kumpir: "kumpir",
  "kuru-fasulye": "kurufasulye",
  pilav: "turkish rice pilaf",
  "karisik-izgara": "mixed grill kebab",
  "kanat-izgara": "grilled chicken wings",
  "tavuk-izgara": "grilled chicken",
  "iskembe-corbasi": "işkembe çorbası",
  "kelle-paca-corbasi": "kelle paça soup",
  "domates-corbasi": "tomato soup",
  corba: "lentil soup turkey",
  saksuka: "şakşuka",
  "acili-ezme": "ezme salad",
  "coban-salata": "çoban salata",
  "coban-salata-2": "shepherd salad turkey",
  "gavurdagi-salata": "gavurdağı salad",
  "meze-tabagi": "turkish meze",
  "meze-tabagi-2": "mezze platter",
  "cay": "turkish tea glass",
  "cay-bardak": "ince belli çay",
  "turk-kahvesi": "turkish coffee",
  raki: "rakı glass",
  salgam: "şalgam suyu",
  sutlac: "sütlaç",
  kazandibi: "kazandibi",
  trilece: "tres leches cake",
  "kahvalti-tabagi": "turkish breakfast",
  iskender: "iskender kebab yogurt",
  "iskender-3": "döner kebab meat",
};

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function requestBuffer(url) {
  return new Promise((resolve, reject) => {
    const getter = url.startsWith("https") ? https : http;
    const req = getter.get(
      url,
      { headers: { "User-Agent": UA, Accept: "image/*,application/json" } },
      (response) => {
        if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
          requestBuffer(response.headers.location).then(resolve).catch(reject);
          return;
        }
        if (response.statusCode !== 200) {
          reject(new Error(`HTTP ${response.statusCode}`));
          return;
        }
        const chunks = [];
        response.on("data", (chunk) => chunks.push(chunk));
        response.on("end", () => resolve(Buffer.concat(chunks)));
        response.on("error", reject);
      },
    );
    req.on("error", reject);
  });
}

async function requestJson(url) {
  const buffer = await requestBuffer(url);
  return JSON.parse(buffer.toString("utf8"));
}

function isImage(buffer) {
  if (buffer.length < 12) return false;
  const jpeg = buffer[0] === 0xff && buffer[1] === 0xd8;
  const png = buffer[0] === 0x89 && buffer[1] === 0x50 && buffer[2] === 0x4e;
  const webp = buffer.slice(0, 4).toString("ascii") === "RIFF";
  return jpeg || png || webp;
}

function commonsQuery(params) {
  const search = new URLSearchParams({ format: "json", origin: "*", ...params });
  return `https://commons.wikimedia.org/w/api.php?${search.toString()}`;
}

async function resolveFiles(fileNames) {
  const unique = [...new Set(fileNames.filter(Boolean))];
  const thumbs = new Map();
  for (let i = 0; i < unique.length; i += 20) {
    const batch = unique.slice(i, i + 20).map((name) => `File:${name.replace(/^File:/i, "")}`);
    const data = await requestJson(
      commonsQuery({
        action: "query",
        titles: batch.join("|"),
        prop: "imageinfo",
        iiprop: "url|mime",
        iiurlwidth: "960",
      }),
    );
    const pages = data?.query?.pages ?? {};
    for (const page of Object.values(pages)) {
      const thumb = page.imageinfo?.[0]?.thumburl || page.imageinfo?.[0]?.url;
      if (thumb && !page.missing) {
        thumbs.set(page.title.replace(/^File:/i, ""), thumb);
      }
    }
    await sleep(DELAY_MS);
  }
  return thumbs;
}

async function searchThumb(query) {
  const data = await requestJson(
    commonsQuery({
      action: "query",
      generator: "search",
      gsrsearch: query,
      gsrnamespace: "6",
      gsrlimit: "4",
      prop: "imageinfo",
      iiprop: "url|mime",
      iiurlwidth: "960",
    }),
  );
  const pages = data?.query?.pages;
  if (!pages) return null;
  const first = Object.values(pages).find((page) => page.imageinfo?.[0]?.thumburl);
  return first?.imageinfo?.[0]?.thumburl ?? null;
}

async function main() {
  const fileMap = JSON.parse(fs.readFileSync(MAP_PATH, "utf8"));
  const manifest = JSON.parse(fs.readFileSync(MANIFEST_PATH, "utf8"));
  const thumbs = await resolveFiles(Object.values(fileMap));

  let ok = 0;
  let fail = 0;

  for (const photo of manifest.photos) {
    let url = null;
    const fileName = fileMap[photo.id];
    if (fileName) {
      url = thumbs.get(fileName) ?? thumbs.get(fileName.replaceAll("_", " "));
    }
    if (!url && SEARCH_FALLBACK[photo.id]) {
      await sleep(DELAY_MS);
      try {
        url = await searchThumb(SEARCH_FALLBACK[photo.id]);
      } catch (error) {
        console.log(`SEARCH FAIL ${photo.id}: ${error.message}`);
      }
    }
    if (!url) {
      continue;
    }

    const dest = path.join(BASE, photo.file.replaceAll("/", path.sep));
    try {
      await sleep(DELAY_MS);
      const buffer = await requestBuffer(url.split("?")[0]);
      if (!isImage(buffer) || buffer.length < 8_000) {
        throw new Error(`not an image (${buffer.length} bytes)`);
      }
      fs.mkdirSync(path.dirname(dest), { recursive: true });
      fs.writeFileSync(dest, buffer);
      photo.sourceUrl = url.split("?")[0];
      console.log(`OK ${photo.id}`);
      ok++;
    } catch (error) {
      console.log(`FAIL ${photo.id}: ${error.message}`);
      fail++;
    }
  }

  manifest.version = (manifest.version || 0) + 1;
  manifest.license =
    "Mixed: Wikimedia Commons (CC) and Unsplash License (https://unsplash.com/license)";
  fs.writeFileSync(MANIFEST_PATH, JSON.stringify(manifest, null, 4), "utf8");
  console.log(`Done v${manifest.version}. OK=${ok} FAIL=${fail}`);
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
