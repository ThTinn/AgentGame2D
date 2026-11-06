"""Import one Universal LPC generator export ("ZIP: Split by animation and item") into Coin Hop.

Extracts the ZIP into its own folder, checks it is one consistent outfit, works out each item's
slot from the generator's sheet_definitions, copies new items into the Parts folder with an
lpc-part.json, and merges the credits. It never deletes anything and never overwrites a part
folder that already exists.

usage:
  python -I import_export.py --zip <file.zip> --name <outfit-name> [--default] [--dry-run]
         [--exports DIR] [--parts DIR] [--credits-dir DIR] [--repo DIR]

The last line is always one of:
  RESULT: OK               every item was added or already present
  RESULT: PARTIAL          some items were left out; the report says which and why
  RESULT: FAILED           nothing was copied; the report says why
Exit code: 0 for OK, 2 for PARTIAL, 1 for FAILED.
"""
import argparse
import csv
import json
import os
import re
import struct
import sys
import zipfile

ANIMATIONS = ["idle", "walk", "run", "jump"]
SHEET_SIZE = (832, 256)  # 13 columns x 4 rows of 64 px; Build Parts crops it afterwards
SLOT_BY_TYPE = {
    "body": "Body", "head": "Head", "expression": "Face", "hair": "Hair",
    "clothes": "Torso", "legs": "Legs", "shoes": "Feet", "hat": "Hat",
}
DEFAULTS = {
    "exports": "F:/WC/CharGen/exports",
    "parts": "F:/WC/Agen/Assets/Art/Characters/Lpc/Parts",
    "credits_dir": "F:/WC/Agen/Assets/Art/Characters/Lpc",
    "repo": "F:/WC/CharGen/Universal-LPC-Spritesheet-Character-Generator",
}


def fail(msg):
    print(f"ERROR: {msg}")
    print("RESULT: FAILED")
    sys.exit(1)


def png_size(path):
    with open(path, "rb") as f:
        head = f.read(24)
    if head[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    return struct.unpack(">II", head[16:24])


def safe_name(text):
    # the generator's export file names: every non [a-z0-9.] character becomes "_", lower case
    return re.sub(r"[^a-z0-9.]", "_", text, flags=re.I).lower()


def pascal(text):
    return "".join(w[:1].upper() + w[1:] for w in re.split(r"[^A-Za-z0-9]+", text) if w)


def load_definitions(repo):
    root = os.path.join(repo, "sheet_definitions")
    if not os.path.isdir(root):
        fail(f"no sheet_definitions under {repo}; pass --repo <generator checkout>")
    defs = []
    for dirpath, _, files in os.walk(root):
        for f in files:
            if not f.endswith(".json") or f.startswith("meta_"):
                continue
            try:
                d = json.load(open(os.path.join(dirpath, f), encoding="utf-8"))
            except (ValueError, OSError):
                continue
            if d.get("type_name") in SLOT_BY_TYPE and d.get("name"):
                zs = [d[k].get("zPos") for k in d if re.fullmatch(r"layer_\d+", k)]
                defs.append({"name": d["name"], "type": d["type_name"], "z": zs})
    return defs


def resolve(stem, defs):
    """'035 longsleeve_2__blue_' -> (definition, color, z) or (None, reason, z)."""
    m = re.fullmatch(r"(\d{3}) (.+)", stem)
    if not m:
        return None, "file name is not '<zPos> <name>'", None
    z, rest = int(m.group(1)), m.group(2)
    matches = []
    for d in defs:
        prefix = safe_name(d["name"]) + "__"
        if rest.startswith(prefix) and rest.endswith("_") and len(rest) > len(prefix) + 1:
            matches.append((len(prefix), d, rest[len(prefix):-1]))
    if not matches:
        return None, "no sheet definition matches this name", z
    best = max(m[0] for m in matches)
    matches = [m for m in matches if m[0] == best]
    by_z = [m for m in matches if z in m[1]["z"]]
    if len(by_z) == 1:
        matches = by_z
    if len({(m[1]["type"], m[1]["name"]) for m in matches}) > 1:
        return None, "ambiguous: " + ", ".join(f"{m[1]['type']}/{m[1]['name']}" for m in matches), z
    _, d, color = matches[0]
    return d, color.replace("_", " "), z


def existing_parts(parts_dir):
    """(slot, displayName) -> folder for every part already imported."""
    found = {}
    if not os.path.isdir(parts_dir):
        return found
    for folder in os.listdir(parts_dir):
        p = os.path.join(parts_dir, folder, "lpc-part.json")
        if os.path.isfile(p):
            j = json.load(open(p, encoding="utf-8"))
            found[(j.get("slot"), j.get("displayName"))] = folder
    return found


def merge_credits(src_dir, dest_dir):
    added = 0
    csv_dest = os.path.join(dest_dir, "credits.csv")
    rows, header = {}, None
    for path in (csv_dest, os.path.join(src_dir, "credits.csv")):
        if not os.path.isfile(path):
            continue
        with open(path, encoding="utf-8", newline="") as f:
            r = csv.reader(f)
            h = next(r, None)
            header = header or h
            for row in r:
                if row and row[0].strip() not in rows:
                    rows[row[0].strip()] = [c.strip() for c in row]
                    added += path != csv_dest
    if header:
        with open(csv_dest, "w", encoding="utf-8", newline="") as f:
            w = csv.writer(f)
            w.writerow(header)
            for k in sorted(rows):
                w.writerow(rows[k])

    txt_dest = os.path.join(dest_dir, "CREDITS.txt")
    blocks = {}
    for path in (txt_dest, os.path.join(src_dir, "credits.txt")):
        if os.path.isfile(path):
            for b in open(path, encoding="utf-8").read().strip().split("\n\n"):
                b = b.strip()
                if b:
                    blocks.setdefault(b.splitlines()[0].strip(), b)
    if blocks:
        open(txt_dest, "w", encoding="utf-8").write("\n\n".join(blocks[k] for k in sorted(blocks)) + "\n")
    bad = [k for k, v in rows.items() if len(v) > 3 and not re.search(r"OGA-BY|CC0", v[3])]
    return added, len(rows), bad


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--zip", required=True)
    ap.add_argument("--name", required=True, help="folder name under --exports for this outfit")
    ap.add_argument("--default", action="store_true", help="new parts are worn by default")
    ap.add_argument("--dry-run", action="store_true", help="check and report, copy nothing")
    for k, v in DEFAULTS.items():
        ap.add_argument("--" + k.replace("_", "-"), default=v)
    a = ap.parse_args()

    if not os.path.isfile(a.zip):
        fail(f"zip not found: {a.zip}")
    if not re.fullmatch(r"[A-Za-z0-9._-]+", a.name):
        fail("--name may only use letters, digits, '.', '_' and '-'")
    out = os.path.join(a.exports, a.name)
    if os.path.exists(out) and os.listdir(out):
        fail(f"{out} already exists and is not empty; pick another --name (nothing is deleted)")

    # 1. extract, refusing paths that escape the folder
    with zipfile.ZipFile(a.zip) as z:
        for member in z.namelist():
            target = os.path.realpath(os.path.join(out, member))
            if not target.startswith(os.path.realpath(out) + os.sep) and target != os.path.realpath(out):
                fail(f"zip entry escapes the folder: {member}")
        if not a.dry_run:
            os.makedirs(out, exist_ok=True)
            z.extractall(out)
        names = z.namelist()

    # 2. one consistent outfit: the four animations list the same items, every sheet full size
    per_anim = {}
    for anim in ANIMATIONS:
        per_anim[anim] = sorted(n.split("/")[-1][:-4] for n in names
                                if n.startswith(f"standard/{anim}/") and n.endswith(".png"))
    if not per_anim["run"]:
        fail("no standard/run/*.png in the zip; export with 'ZIP: Split by animation and item'")
    reference = per_anim["run"]
    for anim, items in per_anim.items():
        if items != reference:
            extra = sorted(set(items) - set(reference))
            missing = sorted(set(reference) - set(items))
            fail(f"'{anim}' lists other items than 'run' (extra {extra}, missing {missing}); "
                 "two exports probably overlapped: export again, one click, and wait for it")
    if not a.dry_run:
        for anim in ANIMATIONS:
            for stem in reference:
                size = png_size(os.path.join(out, "standard", anim, stem + ".png"))
                if size != SHEET_SIZE:
                    fail(f"{anim}/{stem}.png is {size}, expected {SHEET_SIZE}")

    # 3. resolve each item to a slot; refuse multi-layer items rather than import half of one
    defs = load_definitions(a.repo)
    resolved, skipped = [], []
    by_item = {}
    for stem in reference:
        d, info, z = resolve(stem, defs)
        if d is None:
            skipped.append((stem, info))
            continue
        by_item.setdefault((d["type"], d["name"], info), []).append((stem, z, d))
    for (type_name, name, color), files in by_item.items():
        if len(files) > 1:
            skipped.append((", ".join(f[0] for f in files),
                            "multi-layer item; LayeredCharacter draws one sprite per slot"))
            continue
        stem, z, d = files[0]
        resolved.append({"stem": stem, "z": z, "slot": SLOT_BY_TYPE[type_name],
                         "display": f"{name} ({color})",
                         "folder": SLOT_BY_TYPE[type_name] + pascal(name) + pascal(color)})

    # 4. copy what is new
    have = existing_parts(a.parts)
    added, present = [], []
    for r in resolved:
        key = (r["slot"], r["display"])
        if key in have:
            present.append(f"{r['display']} -> {have[key]}")
            continue
        dest = os.path.join(a.parts, r["folder"])
        if os.path.exists(dest):
            skipped.append((r["stem"], f"folder {r['folder']} exists with other content; not overwritten"))
            continue
        if not a.dry_run:
            os.makedirs(dest)
            for anim in ANIMATIONS:
                with open(os.path.join(out, "standard", anim, r["stem"] + ".png"), "rb") as src, \
                        open(os.path.join(dest, anim + ".png"), "wb") as dst:
                    dst.write(src.read())
            json.dump({"slot": r["slot"], "sortingOrder": r["z"], "displayName": r["display"],
                       "equippedByDefault": a.default},
                      open(os.path.join(dest, "lpc-part.json"), "w", encoding="utf-8"), indent=2)
        added.append(f"{r['slot']:5s} {r['display']} -> {r['folder']} (z {r['z']})")

    # 5. credits
    credits_note = "skipped (dry run)"
    bad = []
    if not a.dry_run and added:
        n_new, n_total, bad = merge_credits(os.path.join(out, "credits"), a.credits_dir)
        credits_note = f"{n_new} new entries, {n_total} total"
    elif not added:
        credits_note = "unchanged (nothing added)"

    print(f"export: {out}{' (dry run, not extracted)' if a.dry_run else ''}")
    print(f"items in zip: {len(reference)}")
    print("added:" if added else "added: none")
    for line in added:
        print("  " + line)
    if present:
        print("already present:")
        for line in present:
            print("  " + line)
    if skipped:
        print("left out:")
        for stem, why in skipped:
            print(f"  {stem}: {why}")
    print(f"credits: {credits_note}")
    if bad:
        print("WARNING: credit entries without OGA-BY/CC0: " + ", ".join(bad))
    if skipped or bad:
        print("RESULT: PARTIAL")
        sys.exit(2)
    print("RESULT: OK")


if __name__ == "__main__":
    main()
