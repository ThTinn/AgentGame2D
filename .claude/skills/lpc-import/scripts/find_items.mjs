#!/usr/bin/env node
// Search the local Universal LPC Spritesheet Character Generator catalog for parts DungeonOnline can use.
// Prints observations only (what exists, its licenses, its colors); the caller decides what to pick.
//
// usage:
//   node find_items.mjs [--repo DIR] [--body male] [--slot Hat|hat] [--query "wizard hat"] [--all-licenses] [--json]
//
// A part is listed when it has walk, idle, jump and run for the body type. Each line says whether
// every credit file of that body type offers OGA-BY or CC0 ("ok") or not ("CHECK: ..."), so
// --all-licenses only widens what is shown, never what is called ok.
import fs from "node:fs";
import path from "node:path";

const DEFAULT_REPO = "F:/WC/CharGen/Universal-LPC-Spritesheet-Character-Generator";
const NEED = ["walk", "idle", "jump", "run"];
const OK_LICENSE = /OGA-BY|CC0/;

// generator type_name -> DungeonOnline LpcSlot
export const SLOT_BY_TYPE = {
  body: "Body", head: "Head", expression: "Face", hair: "Hair",
  clothes: "Torso", legs: "Legs", shoes: "Feet", hat: "Hat",
};

function args(argv) {
  const out = { repo: DEFAULT_REPO, body: "male", slot: null, query: "", allLicenses: false, json: false };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === "--repo") out.repo = argv[++i];
    else if (a === "--body") out.body = argv[++i];
    else if (a === "--slot") out.slot = argv[++i];
    else if (a === "--query") out.query = argv[++i];
    else if (a === "--all-licenses") out.allLicenses = true;
    else if (a === "--json") out.json = true;
    else throw new Error(`unknown argument ${a}`);
  }
  return out;
}

function walk(dir, files = []) {
  for (const f of fs.readdirSync(dir)) {
    const p = path.join(dir, f);
    if (fs.statSync(p).isDirectory()) walk(p, files);
    else if (f.endsWith(".json") && !f.startsWith("meta_")) files.push(p);
  }
  return files;
}

function colorNames(repo, def) {
  if (Array.isArray(def.variants) && def.variants.length) return def.variants;
  const material = def.recolors && !Array.isArray(def.recolors) ? def.recolors.material : null;
  if (!material) return [];
  const file = path.join(repo, "palette_definitions", material, `${material}_ulpc.json`);
  return fs.existsSync(file) ? Object.keys(JSON.parse(fs.readFileSync(file, "utf8"))) : [];
}

// credits that cover this body type's layers: a credit file is a prefix of a layer path or vice versa
function bodyCredits(def, body) {
  const layers = Object.keys(def).filter((k) => /^layer_\d+$/.test(k)).map((k) => def[k][body]).filter(Boolean)
    .map((p) => p.replace(/\/$/, ""));
  return (def.credits || []).filter((c) => layers.some((l) => l.startsWith(c.file) || c.file.startsWith(l)));
}

export function findItems({ repo, body, slot, query, allLicenses }) {
  if (!fs.existsSync(path.join(repo, "sheet_definitions")))
    throw new Error(`no sheet_definitions under ${repo}; pass --repo <generator checkout>`);
  const words = query.toLowerCase().split(/\s+/).filter(Boolean);
  const hidden = { license: 0 };
  const wantType = slot ? (SLOT_BY_TYPE[slot] ? slot : Object.keys(SLOT_BY_TYPE).find((t) => SLOT_BY_TYPE[t].toLowerCase() === slot.toLowerCase())) : null;
  const items = [];
  for (const file of walk(path.join(repo, "sheet_definitions"))) {
    let def;
    try { def = JSON.parse(fs.readFileSync(file, "utf8")); } catch { continue; }
    if (!def.type_name || !SLOT_BY_TYPE[def.type_name]) continue;
    if (wantType && def.type_name !== wantType) continue;
    if (!NEED.every((a) => (def.animations || []).includes(a))) continue;
    if (!def.layer_1 || !def.layer_1[body]) continue;
    const rel = path.relative(repo, file).replace(/\\/g, "/");
    const hay = `${def.name} ${rel}`.toLowerCase();
    if (words.length && !words.every((w) => hay.includes(w))) continue;

    const credits = bodyCredits(def, body);
    const bad = credits.filter((c) => !(c.licenses || []).some((l) => OK_LICENSE.test(l)));
    const licenseOk = credits.length > 0 && bad.length === 0;
    if (!licenseOk && !allLicenses) { hidden.license++; continue; }

    const zPos = Object.keys(def).filter((k) => /^layer_\d+$/.test(k)).map((k) => def[k].zPos);
    items.push({
      slot: SLOT_BY_TYPE[def.type_name],
      typeName: def.type_name,
      name: def.name,
      // hash value format: Name_With_Underscores_<color>
      hashKey: `${def.type_name}=${def.name.replace(/ /g, "_")}_<color>`,
      colors: colorNames(repo, def),
      licenseOk,
      licenseNote: licenseOk ? "ok" : `CHECK: ${bad.map((c) => `${c.file} (${(c.licenses || []).join("/")})`).join("; ") || "no credits for this body"}`,
      definition: rel,
      zPos,
      // LayeredCharacter draws one sprite per slot, so a second layer (e.g. hair behind the body) is not supported yet
      singleLayer: zPos.length === 1,
    });
  }
  items.sort((a, b) => a.slot.localeCompare(b.slot) || a.name.localeCompare(b.name));
  return { items, hidden };
}

if (import.meta.url === `file:///${process.argv[1].replace(/\\/g, "/").replace(/^\//, "")}` || process.argv[1].endsWith("find_items.mjs")) {
  const opts = args(process.argv.slice(2));
  const { items, hidden } = findItems(opts);
  if (opts.json) { console.log(JSON.stringify({ items, hidden }, null, 2)); process.exit(0); }
  for (const i of items) {
    const colors = i.colors.length ? `${i.colors.slice(0, 12).join(",")}${i.colors.length > 12 ? ",…" : ""}` : "(none)";
    const layers = i.singleLayer ? "" : `  MULTI-LAYER (z ${i.zPos.join(",")}): not supported by LayeredCharacter yet`;
    console.log(`${i.slot.padEnd(5)} ${i.name.padEnd(28)} ${i.licenseNote.padEnd(4)}  ${i.hashKey}${layers}\n      colors: ${colors}\n      def: ${i.definition}  z ${i.zPos.join(",")}`);
  }
  console.log(`${items.length} item(s) listed`);
  if (hidden.license > 0)
    console.log(`${hidden.license} more match but have a credit file without OGA-BY/CC0; --all-licenses shows them as CHECK`);
}
