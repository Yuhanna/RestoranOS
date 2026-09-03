/**
 * Syncs stock photos from Wikimedia Commons (Turkish dishes) with deduplicated hashes.
 * Fixes manifest UTF-8 mojibake and regenerates ATTRIBUTION.md.
 */
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const https = require("https");
const http = require("http");

const BASE = path.join(__dirname, "..", "apps", "api", "src", "RestaurantOS.Api", "media", "stock");
const MANIFEST_PATH = path.join(BASE, "manifest.json");
const ATTRIBUTION_PATH = path.join(BASE, "ATTRIBUTION.md");
const DELAY_MS = 5000;

function normalizeMojibake(text) {
  return text.replace(/\u0178/g, "\u009f").replace(/\u2021/g, "\u0087");
}

function toThumbUrl(url, width = 960) {
  if (!url.includes("upload.wikimedia.org/wikipedia/commons/")) return url;
  if (url.includes("/thumb/")) return url;
  const match = url.match(/\/commons\/([a-f0-9]\/[a-f0-9]{2})\/([^/?#]+)$/i);
  if (!match) return url;
  const [, hash, filename] = match;
  return `https://upload.wikimedia.org/wikipedia/commons/thumb/${hash}/${filename}/${width}px-${filename}`;
}

/** Direct Wikimedia URLs for critical Turkish menu items */
const DIRECT = {
  "adana-kebap": "https://upload.wikimedia.org/wikipedia/commons/0/05/Adana_kebap_1.jpg",
  "adana-kebap-2": "https://upload.wikimedia.org/wikipedia/commons/f/ff/Shish_kebab_01.jpg",
  "iskender": "https://upload.wikimedia.org/wikipedia/commons/8/81/%C4%B0skender_kebap_1.jpg",
  "iskender-2": "https://upload.wikimedia.org/wikipedia/commons/8/81/%C4%B0skender_kebap_1.jpg",
  "iskender-3": "https://upload.wikimedia.org/wikipedia/commons/3/36/D%C3%B6ner_kebab_meat.jpg",
  "iskender-4": "https://upload.wikimedia.org/wikipedia/commons/8/81/%C4%B0skender_kebap_1.jpg",
  kebap: "https://upload.wikimedia.org/wikipedia/commons/f/ff/Shish_kebab_01.jpg",
  "kebap-2": "https://upload.wikimedia.org/wikipedia/commons/0/05/Adana_kebap_1.jpg",
  "kebap-3": "https://upload.wikimedia.org/wikipedia/commons/f/ff/Shish_kebab_01.jpg",
  "kebap-4": "https://upload.wikimedia.org/wikipedia/commons/0/05/Adana_kebap_1.jpg",
  lahmacun: "https://upload.wikimedia.org/wikipedia/commons/c/c7/Lahmacun.jpg",
  "lahmacun-2": "https://upload.wikimedia.org/wikipedia/commons/c/c7/Lahmacun.jpg",
  "lahmacun-4": "https://upload.wikimedia.org/wikipedia/commons/c/c7/Lahmacun.jpg",
  manti: "https://upload.wikimedia.org/wikipedia/commons/4/49/Manti_in_a_metal_plate.jpg",
  "manti-2": "https://upload.wikimedia.org/wikipedia/commons/4/49/Manti_in_a_metal_plate.jpg",
  "manti-4": "https://upload.wikimedia.org/wikipedia/commons/4/49/Manti_in_a_metal_plate.jpg",
  pide: "https://upload.wikimedia.org/wikipedia/commons/a/ac/Turkish_pide.jpg",
  "pide-2": "https://upload.wikimedia.org/wikipedia/commons/a/ac/Turkish_pide.jpg",
  "kusbasili-pide": "https://upload.wikimedia.org/wikipedia/commons/a/ac/Turkish_pide.jpg",
  kofte: "https://upload.wikimedia.org/wikipedia/commons/0/05/Inegol_kofte_and_piyaz.jpg",
  "kofte-2": "https://upload.wikimedia.org/wikipedia/commons/0/05/Inegol_kofte_and_piyaz.jpg",
  "kofte-4": "https://upload.wikimedia.org/wikipedia/commons/0/05/Inegol_kofte_and_piyaz.jpg",
  dolma: "https://upload.wikimedia.org/wikipedia/commons/7/7b/Sarma_yaprak.jpg",
  "doner-durum": "https://upload.wikimedia.org/wikipedia/commons/1/15/Shawarma_d%C3%BCr%C3%BCm_2021.jpg",
  "doner-durum-2": "https://upload.wikimedia.org/wikipedia/commons/1/15/Shawarma_d%C3%BCr%C3%BCm_2021.jpg",
  "doner-plate": "https://upload.wikimedia.org/wikipedia/commons/3/36/D%C3%B6ner_kebab_meat.jpg",
  wrap: "https://upload.wikimedia.org/wikipedia/commons/1/15/Shawarma_d%C3%BCr%C3%BCm_2021.jpg",
  shawarma: "https://upload.wikimedia.org/wikipedia/commons/1/15/Shawarma_d%C3%BCr%C3%BCm_2021.jpg",
  guvec: "https://upload.wikimedia.org/wikipedia/commons/7/75/Etli_g%C3%BCve%C3%A7.jpg",
  "karisik-izgara": "https://upload.wikimedia.org/wikipedia/commons/7/76/Mixed_grill.jpg",
  "kanat-izgara": "https://upload.wikimedia.org/wikipedia/commons/2/25/Grilled_chicken_wings.jpg",
  "chicken-wings": "https://upload.wikimedia.org/wikipedia/commons/2/25/Grilled_chicken_wings.jpg",
  "chicken-wings-2": "https://upload.wikimedia.org/wikipedia/commons/2/25/Grilled_chicken_wings.jpg",
  "chicken-wings-3": "https://upload.wikimedia.org/wikipedia/commons/2/25/Grilled_chicken_wings.jpg",
  "fried-chicken": "https://upload.wikimedia.org/wikipedia/commons/5/52/Fried_chicken_plate.jpg",
  nuggets: "https://upload.wikimedia.org/wikipedia/commons/0/07/Chicken_nuggets_2.jpg",
  "nuggets-2": "https://upload.wikimedia.org/wikipedia/commons/0/07/Chicken_nuggets_2.jpg",
  "fish-chips": "https://upload.wikimedia.org/wikipedia/commons/f/ff/Fish_and_chips_blackpool.jpg",
  "fish-chips-2": "https://upload.wikimedia.org/wikipedia/commons/f/ff/Fish_and_chips_blackpool.jpg",
  somon: "https://upload.wikimedia.org/wikipedia/commons/5/5e/Grilled_salmon_and_vegetables.jpg",
  "tavuk-izgara": "https://upload.wikimedia.org/wikipedia/commons/b/b2/Grilled_chicken_breast.jpg",
  "balik-tava": "https://upload.wikimedia.org/wikipedia/commons/4/42/Fried_fish_plate.jpg",
  ahtapot: "https://upload.wikimedia.org/wikipedia/commons/1/10/Grilled_octopus_1.jpg",
  kumpir: "https://upload.wikimedia.org/wikipedia/commons/6/64/Kumpir_%28Baked_potato%29.jpg",
  "kuru-fasulye": "https://upload.wikimedia.org/wikipedia/commons/7/74/Kurufasulye.jpg",
  pilav: "https://upload.wikimedia.org/wikipedia/commons/b/b3/Arroz_blanco_%284074805726%29.jpg",
  kunefe: "https://upload.wikimedia.org/wikipedia/commons/4/42/Knafeh_in_Nazareth%2C_Israel.png",
  sutlac: "https://upload.wikimedia.org/wikipedia/commons/8/87/S%C3%BCtla%C3%A7.jpg",
  kazandibi: "https://upload.wikimedia.org/wikipedia/commons/2/2f/Kazandibi.jpg",
  trilece: "https://upload.wikimedia.org/wikipedia/commons/3/36/Tres_leches_cake.jpg",
  kavun: "https://upload.wikimedia.org/wikipedia/commons/e/e0/Cantaloupe_sliced.jpg",
  dondurma: "https://upload.wikimedia.org/wikipedia/commons/6/68/Ice_cream_bowl.jpg",
  "dondurma-2": "https://upload.wikimedia.org/wikipedia/commons/8/87/Mara%C5%9F_dondurma.jpg",
  baklava: "https://upload.wikimedia.org/wikipedia/commons/c/c7/Baklava_-_Turkish_special.jpg",
  profiterol: "https://upload.wikimedia.org/wikipedia/commons/4/4d/Profiteroles.jpg",
  "panna-cotta": "https://upload.wikimedia.org/wikipedia/commons/8/8e/Panna_cotta_with_berries.jpg",
  "cikolatali-fondan": "https://upload.wikimedia.org/wikipedia/commons/6/64/Chocolate_fondant.jpg",
  brownie: "https://upload.wikimedia.org/wikipedia/commons/8/87/Chocolate_brownie.jpg",
  cheesecake: "https://upload.wikimedia.org/wikipedia/commons/6/6b/Cheesecake_with_strawberries.jpg",
  gozleme: "https://upload.wikimedia.org/wikipedia/commons/3/31/Gozleme.jpg",
  menemen: "https://upload.wikimedia.org/wikipedia/commons/b/bd/Menemen_01.jpg",
  omlet: "https://upload.wikimedia.org/wikipedia/commons/e/e2/Omelette.jpg",
  simit: "https://upload.wikimedia.org/wikipedia/commons/d/df/Simit.jpg",
  "sucuklu-yumurta": "https://upload.wikimedia.org/wikipedia/commons/2/29/Sucuklu_yumurta.jpg",
  "kahvalti-tabagi": "https://upload.wikimedia.org/wikipedia/commons/2/25/Turkish_breakfast_plate.jpg",
  granola: "https://upload.wikimedia.org/wikipedia/commons/5/5b/Granola_bowl.jpg",
  ayran: "https://upload.wikimedia.org/wikipedia/commons/3/30/Naneli_ayran_-_Dara.jpg",
  "ayran-2": "https://upload.wikimedia.org/wikipedia/commons/3/30/Naneli_ayran_-_Dara.jpg",
  cay: "https://upload.wikimedia.org/wikipedia/commons/a/ac/Turkish_tea_in_Istanbul.jpg",
  "cay-bardak": "https://upload.wikimedia.org/wikipedia/commons/a/ac/Turkish_tea_in_Istanbul.jpg",
  "bitki-cayi": "https://upload.wikimedia.org/wikipedia/commons/0/07/Herbal_tea.jpg",
  "filtre-kahve": "https://upload.wikimedia.org/wikipedia/commons/1/1a/Filter_coffee.jpg",
  "turk-kahvesi": "https://upload.wikimedia.org/wikipedia/commons/4/4f/Turkish_coffee_2.jpg",
  cola: "https://upload.wikimedia.org/wikipedia/commons/1/11/Glass_of_Cola.jpg",
  greyfurt: "https://upload.wikimedia.org/wikipedia/commons/8/82/Grapefruit_juice.jpg",
  salgam: "https://upload.wikimedia.org/wikipedia/commons/4/4b/Salgam_suyu.jpg",
  sarap: "https://upload.wikimedia.org/wikipedia/commons/6/6f/Glass_of_red_wine.jpg",
  "soda-maden-suyu": "https://upload.wikimedia.org/wikipedia/commons/a/a2/Mineral_water_glass.jpg",
  viski: "https://upload.wikimedia.org/wikipedia/commons/d/d3/Glass_of_whiskey.jpg",
  "vodka-tonic": "https://upload.wikimedia.org/wikipedia/commons/a/aa/Vodka_tonic.jpg",
  mojito: "https://upload.wikimedia.org/wikipedia/commons/0/01/Mojito_cocktail.jpg",
  margarita: "https://upload.wikimedia.org/wikipedia/commons/2/25/Margarita_cocktail.jpg",
  kokteyl: "https://upload.wikimedia.org/wikipedia/commons/3/3a/Cocktail_glass.jpg",
  "buzlu-cay": "https://upload.wikimedia.org/wikipedia/commons/0/07/Iced_tea_glass.jpg",
  limonata: "https://upload.wikimedia.org/wikipedia/commons/6/60/Glass_of_lemonade.jpg",
  "sicak-cikolata": "https://upload.wikimedia.org/wikipedia/commons/3/30/Hot_chocolate_cup.jpg",
  smoothie: "https://upload.wikimedia.org/wikipedia/commons/b/bc/Fruit_smoothie.jpg",
  hummus: "https://upload.wikimedia.org/wikipedia/commons/5/5c/Hummus_from_Israel.jpg",
  "iskembe-corbasi": "https://upload.wikimedia.org/wikipedia/commons/e/e9/Iskembe_corbasi.jpg",
  "kelle-paca-corbasi": "https://upload.wikimedia.org/wikipedia/commons/2/21/Pacha_soup.jpg",
  "mercimek-corbasi": "https://upload.wikimedia.org/wikipedia/commons/6/6d/Mercimek_%C3%A7orbas%C4%B1.jpg",
  "ezogelin-corbasi": "https://upload.wikimedia.org/wikipedia/commons/7/75/Ezogelin_soup.jpg",
  "yayla-corbasi": "https://upload.wikimedia.org/wikipedia/commons/9/91/Yayla_soup.jpg",
  "domates-corbasi": "https://upload.wikimedia.org/wikipedia/commons/9/95/Tomato_soup.jpg",
  "meze-tabagi": "https://upload.wikimedia.org/wikipedia/commons/0/0a/Turkish_meze_platter.jpg",
  "meze-tabagi-2": "https://upload.wikimedia.org/wikipedia/commons/0/0a/Turkish_meze_platter.jpg",
  "peynir-tabagi": "https://upload.wikimedia.org/wikipedia/commons/9/98/Cheese_platter.jpg",
  haydari: "https://upload.wikimedia.org/wikipedia/commons/b/be/Haydari.jpg",
  "haydari-2": "https://upload.wikimedia.org/wikipedia/commons/b/be/Haydari.jpg",
  saksuka: "https://upload.wikimedia.org/wikipedia/commons/f/fe/%C5%9Eak%C5%9Fuka.jpg",
  muhammara: "https://upload.wikimedia.org/wikipedia/commons/9/92/Muhammara.jpg",
  "acili-ezme": "https://upload.wikimedia.org/wikipedia/commons/2/23/Ezme_salad.jpg",
  "sigara-boregi": "https://upload.wikimedia.org/wikipedia/commons/4/4b/Sigara_b%C3%B6re%C4%9Fi.jpg",
  "coban-salata": "https://upload.wikimedia.org/wikipedia/commons/6/68/Choban_salad.jpg",
  "coban-salata-2": "https://upload.wikimedia.org/wikipedia/commons/6/68/Choban_salad.jpg",
  "gavurdagi-salata": "https://upload.wikimedia.org/wikipedia/commons/4/48/Gavurda%C4%9F%C4%B1_salatas%C4%B1.jpg",
  "akdeniz-salata": "https://upload.wikimedia.org/wikipedia/commons/a/a9/Mediterranean_salad.jpg",
  "caesar-salad": "https://upload.wikimedia.org/wikipedia/commons/2/23/Caesar_salad.jpg",
  "patates-kizartmasi": "https://upload.wikimedia.org/wikipedia/commons/8/83/French_fries.jpg",
  "patates-kizartmasi-2": "https://upload.wikimedia.org/wikipedia/commons/8/83/French_fries.jpg",
  "patates-kizartmasi-menu": "https://upload.wikimedia.org/wikipedia/commons/8/83/French_fries.jpg",
  kalamar: "https://upload.wikimedia.org/wikipedia/commons/b/bd/Fried_calamari.jpg",
  "kalamar-2": "https://upload.wikimedia.org/wikipedia/commons/b/bd/Fried_calamari.jpg",
  borek: "https://upload.wikimedia.org/wikipedia/commons/4/4b/Sigara_b%C3%B6re%C4%9Fi.jpg",
};

const WIKI_SEARCH = {
  corba: "Turkish soup",
  "spring-rolls": "spring roll food",
  bruschetta: "bruschetta",
  caprese: "caprese salad",
  carpaccio: "beef carpaccio",
  nachos: "nachos",
  midye: "mussels food",
  "ege-otlari": "green salad",
  burrito: "burrito",
  lasagna: "lasagna",
  "club-sandwich": "club sandwich",
  paella: "paella",
  ramen: "ramen",
  sushi: "sushi",
  tacos: "tacos",
  pizza: "pizza",
  "pizza-2": "margherita pizza",
  "margherita-pizza": "margherita pizza",
  hamburger: "hamburger",
  "hamburger-2": "hamburger",
  cheeseburger: "cheeseburger",
  "burger-fries": "burger fries",
  bonfile: "steak",
  "steak-ribeye": "ribeye steak",
  wagyu: "wagyu steak",
  "kuzu-pirzola": "lamb chop",
  "izgara-levrek": "grilled sea bass",
  "truflu-risotto": "truffle risotto",
  makarna: "pasta",
  "taze-sikma": "orange juice",
  kahve: "coffee latte",
  espresso: "espresso",
  cappuccino: "cappuccino",
  latte: "latte",
  bira: "beer glass",
  sampanya: "champagne glass",
  "gin-tonic": "gin tonic",
  negroni: "negroni cocktail",
  "old-fashioned": "old fashioned cocktail",
  raki: "raki drink",
  su: "water glass",
  "meyve-tabagi": "fruit platter",
  "lava-cake": "chocolate lava cake",
  macaron: "macaron",
  tiramisu: "tiramisu",
  pankek: "pancakes",
  kruvasan: "croissant",
  waffle: "waffle",
  "avokado-toast": "avocado toast",
  "sucuklu-yumurta": "sucuk eggs",
};

function sleep(ms) {
  return new Promise((r) => setTimeout(r, ms));
}

function fixMojibake(text) {
  if (!text || typeof text !== "string") return text;
  if (!/[ÃÅÄ]/.test(text)) return text;
  try {
    const fixed = Buffer.from(normalizeMojibake(text), "latin1").toString("utf8");
    if (fixed && !/[ÃÅÄ]/.test(fixed) && !/\uFFFD/.test(fixed)) return fixed;
  } catch {}
  return text;
}

function md5(buffer) {
  return crypto.createHash("md5").update(buffer).digest("hex");
}

function download(url, dest) {
  return new Promise((resolve, reject) => {
    const getter = url.startsWith("https") ? https : http;
    getter
      .get(url, { headers: { "User-Agent": "RestaurantOS-StockSync/1.0" } }, (response) => {
        if (response.statusCode >= 300 && response.statusCode < 400 && response.headers.location) {
          download(response.headers.location, dest).then(resolve).catch(reject);
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
  }).then((buffer) => {
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    fs.writeFileSync(dest, buffer);
    return buffer;
  });
}

async function wikiSearch(query) {
  const api =
    "https://commons.wikimedia.org/w/api.php?action=query&generator=search" +
    `&gsrsearch=${encodeURIComponent(query)}&gsrnamespace=6&gsrlimit=5` +
    "&prop=imageinfo&iiprop=url|thumburl&iiurlwidth=960&format=json";
  const res = await fetch(api, { headers: { "User-Agent": "RestaurantOS-StockSync/1.0" } });
  if (!res.ok) throw new Error(`Wiki API ${res.status}`);
  const data = await res.json();
  const pages = data?.query?.pages;
  if (!pages) return [];
  return Object.values(pages)
    .map((p) => p.imageinfo?.[0]?.thumburl || p.imageinfo?.[0]?.url)
    .filter(Boolean)
    .map((url) => toThumbUrl(url));
}

function fixStrings(obj) {
  if (typeof obj === "string") return fixMojibake(obj);
  if (Array.isArray(obj)) return obj.map(fixStrings);
  if (obj && typeof obj === "object") {
    for (const k of Object.keys(obj)) obj[k] = fixStrings(obj[k]);
  }
  return obj;
}

async function main() {
  const manifest = JSON.parse(fs.readFileSync(MANIFEST_PATH, "utf8"));
  fixStrings(manifest);

  manifest.license =
    "Mixed: Wikimedia Commons (CC) and Unsplash License (https://unsplash.com/license)";
  manifest.version = (manifest.version || 0) + 1;

  const usedHashes = new Set();
  let ok = 0;
  let fail = 0;
  let skip = 0;

  for (const photo of manifest.photos) {
    const dest = path.join(BASE, photo.file.replace(/\//g, path.sep));
    let candidates = [];

    if (DIRECT[photo.id]) {
      candidates = [toThumbUrl(DIRECT[photo.id])];
    } else if (WIKI_SEARCH[photo.id]) {
      try {
        await sleep(DELAY_MS);
        candidates = await wikiSearch(WIKI_SEARCH[photo.id]);
      } catch (e) {
        console.log(`WIKI FAIL ${photo.id}: ${e.message}`);
      }
    } else {
      skip++;
      continue;
    }

    let saved = false;
    for (const url of candidates) {
      try {
        await sleep(DELAY_MS);
        const buffer = await download(url, dest);
        const hash = md5(buffer);
        if (usedHashes.has(hash) && candidates.length > 1) continue;
        usedHashes.add(hash);
        photo.sourceUrl = url;
        console.log(`OK ${photo.id}`);
        ok++;
        saved = true;
        break;
      } catch (e) {
        // try next candidate
      }
    }

    if (!saved) {
      console.log(`FAIL ${photo.id}`);
      fail++;
    }
  }

  fs.writeFileSync(MANIFEST_PATH, JSON.stringify(manifest, null, 4), "utf8");

  const jpgFiles = [];
  function walk(dir) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith(".jpg") || entry.name.endsWith(".png")) jpgFiles.push(full);
    }
  }
  walk(BASE);
  const fileHashes = new Set();
  for (const f of jpgFiles) fileHashes.add(md5(fs.readFileSync(f)));

  const lines = [
    "# Stok ürün fotoğrafları",
    "",
    "Bu klasördeki görseller [Wikimedia Commons](https://commons.wikimedia.org) ve",
    "[Unsplash](https://unsplash.com) üzerinden indirilmiştir.",
    "",
    `Manifest sürümü: ${manifest.version} — ${jpgFiles.length} dosya, ${fileHashes.size} benzersiz görsel.`,
    "",
    "| Dosya | Kaynak |",
    "| --- | --- |",
  ];
  for (const photo of manifest.photos) {
    lines.push(`| \`${photo.file}\` | ${photo.sourceUrl || "-"} |`);
  }
  fs.writeFileSync(ATTRIBUTION_PATH, lines.join("\n"), "utf8");

  console.log(`Done v${manifest.version}. OK=${ok} FAIL=${fail} SKIP=${skip} unique=${fileHashes.size}`);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
