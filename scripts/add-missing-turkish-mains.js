/**
 * Add dedicated Wikimedia photos for missing Turkish mains (Urfa kebap, etc.)
 * and clean misleading alias tags from older entries.
 *
 * Usage: node scripts/add-missing-turkish-mains.js
 */
const fs = require("fs");
const path = require("path");
const https = require("https");
const http = require("http");

const UA = "RestaurantOS-StockSync/1.0 (local demo; restaurant menu stock photos)";
const BASE = path.join(__dirname, "..", "apps", "api", "src", "RestaurantOS.Api", "media", "stock");
const MANIFEST_PATH = path.join(BASE, "manifest.json");
const ATTR_PATH = path.join(BASE, "ATTRIBUTION.md");
const DELAY_MS = 1500;

/** @type {{ id: string, commonsFile: string, title: string, alt: string, tags: string[] }[]} */
const ADDITIONS = [
  {
    id: "urfa-kebap",
    commonsFile: "Urfa kebap.jpg",
    title: "Urfa kebap",
    alt: "Urfa kebap şiş tabak sunumu",
    tags: ["urfa kebap", "urfa", "kebap", "acısız", "şiş", "ana yemek", "türk"],
  },
  {
    id: "ali-nazik",
    commonsFile: "Ali Nazik.jpg",
    title: "Ali nazik",
    alt: "Ali nazik kebap tabak sunumu",
    tags: ["ali nazik", "alinazik", "kebap", "patlıcan", "yoğurt", "gaziantep", "ana yemek"],
  },
  {
    id: "cag-kebap",
    commonsFile: "20250104 Cağ kebabı.jpg",
    title: "Cağ kebap",
    alt: "Cağ kebap şiş sunumu",
    tags: ["cağ kebap", "cag kebap", "kebap", "erzurum", "döner", "ana yemek", "türk"],
  },
  {
    id: "cig-kofte",
    commonsFile: "Turkish çiğ köfte.jpg",
    title: "Çiğ köfte",
    alt: "Çiğ köfte porsiyon sunumu",
    tags: ["çiğ köfte", "cig kofte", "köfte", "meze", "başlangıç", "türk", "ana yemek"],
  },
  {
    id: "ciger-sis",
    commonsFile: "Ciğer şiş.jpg",
    title: "Ciğer şiş",
    alt: "Izgara ciğer şiş",
    tags: ["ciğer şiş", "ciğer", "ciger", "şiş", "kebap", "izgara", "ana yemek"],
  },
  {
    id: "patlican-kebap",
    commonsFile: "Eggplant kebab and isot.jpg",
    title: "Patlıcan kebap",
    alt: "Patlıcan kebap ve isot biber",
    tags: ["patlıcan kebap", "patlıcanlı kebap", "patlıcan", "kebap", "urfa", "ana yemek"],
  },
  {
    id: "tas-kebabi",
    commonsFile: "Tas kebabı.jpg",
    title: "Tas kebabı",
    alt: "Tas kebabı pilav ile",
    tags: ["tas kebabı", "tas kebab", "kebap", "güveç", "ev yemeği", "ana yemek"],
  },
  {
    id: "islim-kebabi",
    commonsFile: "Patlıcan islim kebabı.jpg",
    title: "İslim kebabı",
    alt: "Patlıcan islim kebabı",
    tags: ["islim kebabı", "islim kebab", "patlıcan", "kebap", "ana yemek", "türk"],
  },
  {
    id: "cop-sis",
    commonsFile: "Çöp şiş.jpg",
    title: "Çöp şiş",
    alt: "Çöp şiş ızgara",
    tags: ["çöp şiş", "cop sis", "şiş", "kebap", "kuzu", "izgara", "ana yemek"],
  },
];

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function requestBuffer(url, attempt = 0) {
  return new Promise((resolve, reject) => {
    const getter = url.startsWith("https") ? https : http;
    const req = getter.get(
      url,
      { headers: { "User-Agent": UA, Accept: "image/*,application/json" } },
      (response) => {
        if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
          requestBuffer(response.headers.location, attempt).then(resolve).catch(reject);
          return;
        }
        if (response.statusCode === 429 && attempt < 6) {
          const waitMs = Number(response.headers["retry-after"] || 0) * 1000 || 8000 * (attempt + 1);
          response.resume();
          sleep(waitMs)
            .then(() => requestBuffer(url, attempt + 1))
            .then(resolve)
            .catch(reject);
          return;
        }
        if (response.statusCode !== 200) {
          reject(new Error(`HTTP ${response.statusCode} for ${url}`));
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
  return JSON.parse((await requestBuffer(url)).toString("utf8"));
}

function commonsQuery(params) {
  const search = new URLSearchParams({ format: "json", origin: "*", ...params });
  return `https://commons.wikimedia.org/w/api.php?${search.toString()}`;
}

function isJpeg(buffer) {
  return buffer.length > 12 && buffer[0] === 0xff && buffer[1] === 0xd8;
}

function stripTags(photo, remove) {
  const drop = new Set(remove.map((t) => t.toLocaleLowerCase("tr-TR")));
  photo.tags = (photo.tags || []).filter((t) => !drop.has(String(t).toLocaleLowerCase("tr-TR")));
}

async function resolveThumbs(fileNames) {
  const map = new Map();
  const batch = fileNames.map((name) => `File:${name}`);
  const data = await requestJson(
    commonsQuery({
      action: "query",
      titles: batch.join("|"),
      prop: "imageinfo",
      iiprop: "url|mime",
      iiurlwidth: "960",
    }),
  );
  for (const page of Object.values(data?.query?.pages ?? {})) {
    const thumb = page.imageinfo?.[0]?.thumburl || page.imageinfo?.[0]?.url;
    if (thumb && !page.missing) {
      map.set(page.title.replace(/^File:/i, ""), thumb.split("?")[0]);
    }
  }
  return map;
}

async function main() {
  const manifest = JSON.parse(fs.readFileSync(MANIFEST_PATH, "utf8"));
  const existing = new Set(manifest.photos.map((p) => p.id));

  console.log("Resolving Commons thumbnails…");
  const thumbs = await resolveThumbs(ADDITIONS.map((a) => a.commonsFile));
  await sleep(DELAY_MS);

  const added = [];
  for (const item of ADDITIONS) {
    if (existing.has(item.id)) {
      console.log(`skip existing id: ${item.id}`);
      continue;
    }
    const thumb = thumbs.get(item.commonsFile);
    if (!thumb) {
      console.error(`FAIL resolve: ${item.commonsFile}`);
      continue;
    }

    const rel = `mains/${item.id}.jpg`;
    const dest = path.join(BASE, rel);
    fs.mkdirSync(path.dirname(dest), { recursive: true });

    console.log(`download ${item.id} ← ${item.commonsFile}`);
    const buffer = await requestBuffer(thumb);
    if (!isJpeg(buffer) && !buffer.slice(0, 8).toString("ascii").includes("PNG")) {
      // Accept jpeg primarily; convert path still .jpg — skip non-image
      if (!(buffer[0] === 0x89 && buffer[1] === 0x50)) {
        console.error(`FAIL not image: ${item.id} (${buffer.length} bytes)`);
        await sleep(DELAY_MS);
        continue;
      }
    }
    fs.writeFileSync(dest, buffer);

    const photo = {
      id: item.id,
      category: "mains",
      file: rel,
      title: item.title,
      alt: item.alt,
      tags: item.tags,
      sourceUrl: thumb,
    };
    manifest.photos.push(photo);
    added.push(photo);
    await sleep(DELAY_MS);
  }

  // Clean misleading aliases now that dedicated photos exist
  for (const photo of manifest.photos) {
    if (photo.id === "kebap") {
      stripTags(photo, ["urfa kebap", "urfa", "beyti kebap", "beyti"]);
    }
    if (photo.id === "adana-kebap") {
      stripTags(photo, ["urfa kebap", "urfa"]);
    }
    if (photo.id === "shawarma") {
      stripTags(photo, ["beyti kebap", "beyti"]);
    }
    if (photo.id === "guvec") {
      stripTags(photo, ["tas kebab", "tas kebabı", "hünkar beğendi", "hunkar begendi"]);
    }
    if (photo.id === "manti-4") {
      stripTags(photo, ["islim kebab", "islim kebabı"]);
    }
  }

  manifest.version = Number(manifest.version || 0) + 1;
  fs.writeFileSync(MANIFEST_PATH, JSON.stringify(manifest, null, 4) + "\n", "utf8");

  if (added.length && fs.existsSync(ATTR_PATH)) {
    const lines = added.map((p) => `| ${p.file} | ${p.sourceUrl} |`);
    let attr = fs.readFileSync(ATTR_PATH, "utf8");
    // Keep as plain append block (ATTRIBUTION may be one long line historically)
    const block =
      `\n\n## v${manifest.version} — eksik Türk ana yemekleri\n\n` +
      `| Dosya | Kaynak |\n| --- | --- |\n` +
      lines.join("\n") +
      "\n";
    if (!attr.includes(`v${manifest.version} — eksik`)) {
      fs.writeFileSync(ATTR_PATH, attr.trimEnd() + block, "utf8");
    }
  }

  console.log(`\nDone. Added ${added.length} photos. Manifest version → ${manifest.version}`);
  for (const p of added) console.log(`  + ${p.id}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
