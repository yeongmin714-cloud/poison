# Figma 15:4 Inventory / Detail / Storage — Phase 2 status

Figma source: `figma-file.json`, frame `15:4`; canvas-local hierarchy snapshot: `15-4-inventory-hierarchy.json`; full frame render: `renders/15-4.png`.

## Verified target outer bounds

| panel | bounds (x, y, w, h) | state |
|---|---:|---|
| Inventory | 144, 84, 504, 912 | applied by `InventoryClusterFigmaLayout` after `InventoryWindowUTK.Show` legacy placement |
| Detail | 672, 175.2, 576, 729.6 | applied by `InventoryClusterFigmaLayout` after `ItemDescriptionWindowUTK.Open` center operation |
| Storage | 1272, 84, 504, 912 | applied by `InventoryClusterFigmaLayout` after `WarehouseWindowUTK.Show` legacy placement |

All three existing windows continue to be independent top-level VisualElements. No reparenting, PanelSettings change, data-system change, or DnD registration/lifecycle change was made.

## Internal structure and current state

- **Inventory:** outer panel bound is applied by `InventoryClusterFigmaLayout` after Show placement. 5×2 equipment grid is retained and uses Figma cell sizing/spacing. Bag displays at least 5×5 cells in a 5-row viewport and remains vertically scrollable for all PlayerInventory slots. Existing item data, hover, selection, DnD and EquipManager routes remain.
- **Detail:** outer panel bound is applied. Header/name/image/description regions use Figma internal bounds within the independent existing detail window; the stats boxes remain included in the description/details area. ShowItem, Clear, rarity/item data and existing entry points remain.
- **Storage:** outer panel bound is applied. Header/stats/grid/footer/actions use Figma internal panel-local bounds. Visible area is a 5-column × 6-row (30-cell) grid with 81.6 canonical cell size / 9.6 gaps; expansion beyond 30 stays accessible with scroll. WarehouseSystem default capacity 20 / expansion max 35, tenant key, save/load and single-item transaction APIs remain unchanged. All-store/retrieve use existing per-item paths, preserve remaining inventory/storage items when limits are reached, and are regression-tested.
- DnD review fixed the standalone storage target to register only its visible warehouse column and accept inventory-origin deposits there. Warehouse-origin drags are rejected by this target, so they cannot accidentally withdraw from the currently selected territory; storage-to-inventory remains the visible inventory window's route.

## Remaining work / validation

- The title tabs and cosmetic internals still need pixel-level Play comparison and any missing detail where Figma has real controls not represented by existing UI. No fresh 1920×1080 Game-view screenshot exists; visual parity is not approved.
- Run dynamic resize validation and the full relevant route/close/ESC/drag/drop suite. Latest verification available: fresh compile exit 0 and unique filtered `warehouse-phase2-final.xml` **23/23 passed**, with 0 compiler errors in its paired log; full EditMode suite and Play were not run.
- No staging, commit or push.

## Acceptance / validation

- Compile: `timeout 300 bash compile_test.sh` exit 0.
- Focused EditMode: `TestOutput/figma-inventory-cluster-final.xml`, 7 passed, 0 failed, 0 skipped; corresponding log contains 0 `error CS` and no compiler abort.
- `git diff --check` passed for Phase 2 files.
- No Play capture or live Figma-vs-game screenshot comparison yet. Outer bounds are code-applied but visual parity is not established. Root geometry change after opening is not subscribed/reapplied yet.
- No stage/commit/push.
