---
name: lpc-import
description: "Import LPC character parts into DungeonOnline from a plain description: finds the items in the local LPC generator, exports them from the generator site, copies them into the wardrobe and rebuilds parts. Use when the user wants new clothes, hats, hair or a new outfit for the player character."
argument-hint: "[description, e.g. \"purple wizard hat, leather boots, white shirt\"] [--default]"
user-invocable: true
allowed-tools: Read, Glob, Grep, Edit, AskUserQuestion, Bash(node */.claude/skills/lpc-import/scripts/find_items.mjs*), Bash(python -I */.claude/skills/lpc-import/scripts/import_export.py*), mcp__Claude_Browser__navigate, mcp__Claude_Browser__preview_start, mcp__Claude_Browser__find, mcp__Claude_Browser__computer, mcp__Claude_Browser__javascript_tool
---

# LPC Import

Turns "a green cap and brown boots" into wearable parts in the DungeonOnline wardrobe, the same way the
first outfit was built. Talk to the user in their language (Vietnamese so far).

**Paths** (this machine):
- Generator checkout: `F:/WC/CharGen/Universal-LPC-Spritesheet-Character-Generator` (catalog only; it is not run locally, Node there needs 22+)
- Generator site: `https://liberatedpixelcup.github.io/Universal-LPC-Spritesheet-Character-Generator/`
- Exports: `F:/WC/CharGen/exports/<outfit-name>/`
- Parts: `Assets/Art/Characters/Lpc/Parts/<Slot><Name><Color>/` with `idle|walk|run|jump.png` + `lpc-part.json`
- Credits: `Assets/Art/Characters/Lpc/CREDITS.txt`, `credits.csv`
- Scripts: `${CLAUDE_SKILL_DIR}/scripts/find_items.mjs`, `${CLAUDE_SKILL_DIR}/scripts/import_export.py`

**Limits of the current runtime** (say so when a request runs into them, never work around silently):
- Male body only. Every part in the wardrobe is drawn for the male body; a female, teen or other
  body needs its own wardrobe, which does not exist yet.
- One layer per item. `LayeredCharacter` draws one sprite per slot, so items with a second layer
  (long hair or ponytails drawn behind the body, z 9) are refused by `import_export.py`.
- Slots: Body, Head, Face, Hair, Torso, Legs, Feet, Hat. Capes, weapons, belts and so on have no slot yet.
- Right-facing frames that `Assets/Data/LpcAnimations.asset` uses (walk, idle, jump, run) only.

Each phase ends with one of: **done**, **skipped (reason)**, **not run (reason)**. The final report
lists every phase with its status; a phase that did not run is never reported as done.

## Phase 1: Find candidates

Split the description into slots. For each slot run:

```bash
node "${CLAUDE_SKILL_DIR}/scripts/find_items.mjs" --slot <Slot> --query "<words>"
```

Keep only lines marked `ok` (every credit file offers OGA-BY or CC0) and without `MULTI-LAYER`.
If the script prints "N more match but have a credit file without OGA-BY/CC0", mention it: those
exist but cannot ship under the project's license rule (design/game-brief.md). Pick a color from
the `colors:` list that fits the description. A query with no usable result is reported as such,
with the closest usable alternatives (run again with a shorter query or no query for that slot).

If the description needs a non-male body, multi-layer hair or a slot that does not exist, stop for
that item and say which limit it hits.

## Phase 2: Approve the list

AskUserQuestion with a table: slot, item, color, license, and whether new parts are worn by default
(`--default`, normally no). State in the question that approving it also allows downloading the
export ZIP(s) from the generator site into Downloads. Nothing below runs without this answer; a
dismissed question is re-asked in plain text.

## Phase 3: Compose and check in the generator

The generator holds one item per slot, so items that share a slot go into separate exports. For each
export build the URL hash, starting from the base body:

```
sex=male&body=Body_Color_light&head=Human_Male_light&expression=Neutral_light&<type>=<Name_With_Underscores>_<color>&...
```

`find_items.mjs` prints each item's `hashKey` (`hat=Tricorne_<color>`). Open it in the built-in
browser (`preview_start` with the URL, or `navigate`), wait ~3 s, and read back the selections:

```js
await new Promise(r => setTimeout(r, 3000));
const h = [...document.querySelectorAll('h2,h3,.title,summary')].find(e => /Current Selections/.test(e.innerText));
h ? h.parentElement.innerText.replace(/\n/g, ', ') : 'NOT FOUND'
```

Every planned item must appear with its color. A missing item means the hash key did not resolve:
fix the name or color and check again. Do not export a selection that differs from the approved list.

## Phase 4: Export, one click at a time

Lessons from the first import, all of which broke an export:
- **One export at a time.** Two overlapping exports mix outfits (one animation folder gets the other
  outfit's items). Never start an export while another may still be running.
- **No long-running page scripts.** `javascript_tool` gives up after ~45 s but the script keeps
  running in the page; a loop that clicks "export" keeps clicking after the tool reported a timeout.
  Keep each JS call short (≤ 35 s) and never click export from JS.
- Click the button for real: `find` "Split by animation and item", then `computer` `left_click` on
  that ref.

Before the click, hook the download so its file name is known:

```js
window.__dl = window.__dl || [];
if (!window.__lpcHooked) { const oc = HTMLAnchorElement.prototype.click;
  HTMLAnchorElement.prototype.click = function () { window.__dl.push(this.download); return oc.apply(this, arguments); };
  window.__lpcHooked = true; }
window.__dl.length
```

After the click, poll in calls of ≤ 35 s until `window.__dl` grows (an export takes 10-20 s):

```js
let t = 0; const n = <length before>; for (; t < 30 && window.__dl.length === n; t++) await new Promise(r => setTimeout(r, 1000));
JSON.stringify(window.__dl.slice(n))
```

The file reaches `~/Downloads` a few minutes later. Wait for it in the background (Bash
`run_in_background`), not with foreground sleeps:

```bash
for i in $(seq 1 600); do [ -f "$USERPROFILE/Downloads/<file>.zip" ] && break; sleep 1; done; ls -l "$USERPROFILE/Downloads/<file>.zip"
```

If the file never appears, report phase 4 as not run and give the user the URL to export by hand.

## Phase 5: Import

```bash
python -I "${CLAUDE_SKILL_DIR}/scripts/import_export.py" --zip "<Downloads>/<file>.zip" --name <outfit-name> [--default]
```

Run it with `--dry-run` first when unsure. Read the last line:
- `RESULT: OK`: report what was added and what was already present.
- `RESULT: PARTIAL`: report each "left out" item and its reason; the rest was imported.
- `RESULT: FAILED`: nothing was copied. An overlap error means: export again (phase 4), one click.

The script never deletes or overwrites; an existing export folder name is refused, so pick a new one.

## Phase 6: Build Parts in Unity

Check whether the editor has the project open:

```powershell
Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like 'Agen*' }
```

- **Editor open:** ask the user to focus Unity and run **Tools > LPC > Build Parts**. Watch
  `%LOCALAPPDATA%/Unity/Editor/Editor.log` in the background for `LPC: built`, `LPC: cropped`,
  `error CS` or an exception, counting only lines after the request.
- **Editor closed:** run it yourself:
  `"D:/Unity/Install/6000.3.7f1/Editor/Unity.exe" -batchmode -quit -projectPath F:/WC/Agen -executeMethod DungeonOnline.EditorTools.LpcPartImporter.BuildParts -logFile Logs/lpc-build.log`
  then read the same lines from that log.

Done means the log shows `LPC: built <n> parts` with `n` = previous count + parts added, and
`LPC: cropped` for the new sheets. No such line within the wait is "not run", not "done".

## Phase 7: Record

Ask "May I write the new parts to the outfit list in `design/game-brief.md`?" and, on yes, add
them under "Đồ thay thế". Tell the user the new parts appear in the Outfit panel in Play mode.

## Final report

```
LPC import: <outfit-name>
1 Find      done | not run (<reason>)
2 Approve   done
3 Compose   done (<n> export(s))
4 Export    done (<zip names>) | not run (<reason>)
5 Import    OK | PARTIAL (<left out>) | FAILED (<reason>)
6 Build     done (<n> parts) | waiting on user | not run (<reason>)
7 Record    done | skipped (user declined)

Verdict: COMPLETE | PARTIAL | BLOCKED (<phase and reason>)
```

**COMPLETE** only when phases 4-6 are done and the import was OK. **PARTIAL** when some items were
left out or phase 7 was skipped. **BLOCKED** when any of phases 1-6 did not run or the import
FAILED; name the phase. Phase 6 still waiting on the user is BLOCKED, not COMPLETE.

## Follow-Up

- Try the parts in Play mode with the Outfit panel (Tools > LPC > Add Wardrobe UI if the scene has none).
- New clothes change the look of the player: take a screenshot for `production/qa/evidence/`
  (`.claude/docs/run-and-observe.md`).
- Run `/asset-audit` after several imports to check sizes and naming across `Assets/Art`.
