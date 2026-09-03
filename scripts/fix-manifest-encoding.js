/** Fixes UTF-8 mojibake in manifest.json and regenerates ATTRIBUTION.md */
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const { execSync } = require("child_process");

const BASE = path.join(__dirname, "..", "apps", "api", "src", "RestaurantOS.Api", "media", "stock");
const MANIFEST_PATH = path.join(BASE, "manifest.json");
const ATTRIBUTION_PATH = path.join(BASE, "ATTRIBUTION.md");
const REPO_ROOT = path.join(__dirname, "..");

function normalizeMojibake(text) {
  return text.replace(/\u0178/g, "\u009f").replace(/\u2021/g, "\u0087");
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

function fixStrings(obj) {
  if (typeof obj === "string") return fixMojibake(obj);
  if (Array.isArray(obj)) return obj.map(fixStrings);
  if (obj && typeof obj === "object") {
    for (const k of Object.keys(obj)) obj[k] = fixStrings(obj[k]);
  }
  return obj;
}

function md5(buffer) {
  return crypto.createHash("md5").update(buffer).digest("hex");
}

function readHeadManifest() {
  const relative = "apps/api/src/RestaurantOS.Api/media/stock/manifest.json";
  const raw = execSync(`git -C "${REPO_ROOT}" show HEAD:${relative}`, { encoding: "buffer" });
  return JSON.parse(raw.toString("utf8"));
}

function readCurrentSourceUrls() {
  if (!fs.existsSync(MANIFEST_PATH)) return new Map();
  const current = JSON.parse(fs.readFileSync(MANIFEST_PATH, "utf8"));
  return new Map(current.photos.map((photo) => [photo.id, photo.sourceUrl]));
}

const manifest = readHeadManifest();
fixStrings(manifest);

const sourceUrls = readCurrentSourceUrls();
for (const photo of manifest.photos) {
  const currentUrl = sourceUrls.get(photo.id);
  if (currentUrl && !currentUrl.includes("unsplash.com/photo-15")) {
    photo.sourceUrl = currentUrl;
  }
}

manifest.version = (manifest.version || 0) + 1;
manifest.license =
  "Mixed: Wikimedia Commons (CC) and Unsplash License (https://unsplash.com/license)";

fs.writeFileSync(MANIFEST_PATH, JSON.stringify(manifest, null, 4), "utf8");

const jpgFiles = [];
function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full);
    else if (/\.(jpg|jpeg|png)$/i.test(entry.name)) jpgFiles.push(full);
  }
}
walk(BASE);

const unique = new Set(jpgFiles.map((f) => md5(fs.readFileSync(f))));

const lines = [
  "# Stok ürün fotoğrafları",
  "",
  "Bu klasördeki görseller [Wikimedia Commons](https://commons.wikimedia.org) ve",
  "[Unsplash](https://unsplash.com) üzerinden indirilmiştir.",
  "",
  `Manifest sürümü: ${manifest.version} — ${jpgFiles.length} dosya, ${unique.size} benzersiz görsel.`,
  "",
  "| Dosya | Kaynak |",
  "| --- | --- |",
];
for (const photo of manifest.photos) {
  lines.push(`| \`${photo.file}\` | ${photo.sourceUrl || "-"} |`);
}
fs.writeFileSync(ATTRIBUTION_PATH, lines.join("\n"), "utf8");

const written = fs.readFileSync(MANIFEST_PATH, "utf8");
const mojibakeLeft = written.match(/[ÃÅÄ]/g);
const replacementLeft = written.match(/\uFFFD/g);
console.log(
  `Manifest v${manifest.version}. Unique hashes: ${unique.size}. Mojibake: ${mojibakeLeft?.length ?? 0}. Replacement: ${replacementLeft?.length ?? 0}`,
);
