# Build Spec for FeaturedPromoItem (上稿作業)
- database schema: `.\database\promotion.sql`
- functional spec and mock-ups: `.\spec\custom\FeaturedPromoItem\FeaturedPromoItem.spec.md` (+ `ui-query / ui-update / ui-new.spec.png`)

This is a **custom** feature, not the standard list / detail / form triple: the page is a weekly
grid — one tab per training center, a Monday-to-Sunday week, seven day sections of three slots —
with an inline New / Edit form in the cell being edited.

---

## Summary

| Item | Detail |
|------|--------|
| Primary Key | `pkid` `int` IDENTITY(1,1) — database-assigned |
| Natural key | **UNIQUE** (`ScheduleOn`, `TrainingCenter_pkid`, `Slot`) — one promotion per slot per day per center |
| Foreign Keys | `TrainingCenter_pkid` → `TrainingCenter.pkid` (NOT NULL); `Promotion_pkid` → `Promotion2.pkid` (NOT NULL) |
| Required Fields | every column — nothing is nullable |
| N-N Relationships | N/A |
| Primary-Foreign Links | N/A — nothing FKs into this table, so delete is a plain 204 / 404 |
| Query Filters | `trainingCenterPkid` (the active tab) and `weekOf` (any date; snapped to Monday–Sunday) |
| Default Sort | `ScheduleOn ASC, TrainingCenter_pkid ASC, Slot ASC` |

## Localization

- FeaturedPromoItem: 上稿作業
- pkid: 主代碼 · ScheduleOn: 上稿日期 · TrainingCenter_pkid: 教育中心 · Slot: 版位 · Promotion_pkid: 促銷 (shown as 促銷代碼 PromoCode) · Topic: 主題 · Description: 說明

## Lookup endpoints

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/training-centers` | **New** — lookup-only `ITrainingCenterRepository` | `TrainingCenterLookup { pkid, name, displayOrder }`, `DisplayOrder ASC, pkid ASC` — the tab strip |
| `GET /api/lookups/promotions?keyword=` | **New** — lookup-only `IPromotion2Repository` | `Promotion2Lookup { pkid, promoCode, topic, description }` — PromoCode **prefix** match, newest code first, `TOP 20` |

Neither `TrainingCenter` nor `Promotion2` is a generated feature. When either gets its own `/crud`,
repoint the lookup at the new repository and delete the lookup-only one (`AppUser` pattern).

## API

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/featured-promo-items` | every row (31k+) — the grid never calls it |
| `POST` | `/api/featured-promo-items/query` | `{ trainingCenterPkid?, weekOf? }` → that center's rows from the Monday to the Sunday of `weekOf`'s week, inclusive |
| `GET` | `/api/featured-promo-items/{id:int}` | 404 when missing |
| `POST` | `/api/featured-promo-items` | 201; **409** when (day, center, slot) is already taken — checked with `IsSlotTakenAsync` before the INSERT |
| `PUT` | `/api/featured-promo-items` | key in the body; 404; **409** when moved onto another row's slot (its own row is excluded) |
| `DELETE` | `/api/featured-promo-items/{id:int}` | 204 / 404 |
| `POST` | `/api/featured-promo-items/{id:int}/move-slot` | body `{ targetSlot }` (1–3). If the target is occupied the two rows **swap**; same slot is a no-op. 200 with the moved row / 404 / 400 |

### Week snapping

`FeaturedPromoItemRepository.WeekOf(date)` is `public static` and shared with the in-memory fake:
Monday = `date − ((DayOfWeek + 6) % 7)`, Sunday = Monday + 6. A Sunday snaps to the Monday
*before* it.

### Slot swap

The UNIQUE key makes a direct swap impossible, so `MoveSlotAsync` runs one transaction:
occupant → slot `0` (a parking value never used by the grid), item → target, occupant → item's
old slot, then re-read inside the transaction.

## Frontend

- Route `/featured-promo-items` only; sidebar group **`首頁 Home`** (new, first) → `上稿作業 FeaturedPromoItem` (`pi pi-megaphone`).
- `features/featured-promo-items/featured-promo-item-list` — the grid. State: `activeTrainingCenterPkid`, `weekStart` (local-midnight Monday), `items`, `editing` (the open cell), `clipboard`. `days` is a computed seven-day × three-slot matrix so empty cells render too. Persists `{ trainingCenterPkid, weekOf }` under `featured-promo-item-list-filters` and the Copy buffer under `featured-promo-item-clipboard`.
- Week navigator: `<<--` / `-->>` step seven days; `本週` returns to today's Monday. Day headers read `3/16 (一)` … `3/22 (日)`; the label reads `3/16 -- 3/22`. Helpers `addDays`, `mondayOf`, `weekdayLabel` live in `core/utils/date.util.ts`.
- Each slot row: slot number · `+` (down, disabled at 3 or when empty) · `−` (up, disabled at 1) · 編輯／新增 · 複製／刪除 (occupied) or 貼上 (empty, needs a clipboard) · PromoCode · Topic · Description.
- `features/featured-promo-items/featured-promo-item-form` — the inline form. Signal inputs `scheduleOn`, `trainingCenterPkid`, `slot`, `item` (edit), `prefill` (paste); outputs `saved`, `cancelled`. PromoCode is a `p-autocomplete` (`forceSelection`, `dropdown`) over `getPromotions(keyword)`; **selecting a promotion overwrites Topic and Description** with that promotion's values (a deliberate pick — the user edits afterwards). Edit mode ignores `prefill`.
- 409 on save shows 「此日期、教育中心的版位已有上稿資料。」 and keeps the form open.

## Tests

- Backend `FeaturedPromoItemsControllerTests` (33): sort + nav objects, the training-center filter, the one-week filter (Wednesday → Mon–Sun, Monday/Thursday/Sunday all give the same range, inclusive ends, excludes the surrounding Sunday/Monday), combined filters, `WeekOf` theory, get, create (201 / trim / 409 slot taken / same slot on another center OK / 400), update (ok / own slot OK / 409 / 404 / 400), delete, move-slot (empty target / swap / no-op / 404 / 400). `FeaturedPromoItemsRoutingConventionTests` pins the routes including `{id:int}/move-slot`. `LookupsControllerTests` covers the tab order and the PromoCode lookup (no keyword, prefix, case-insensitive, Topic/Description carried, no match, 20-row cap).
- Frontend: `featured-promo-item.service.spec` (every method incl. `moveSlot`), `lookup.service.spec` (training centers; promotions with / without keyword), `date.util.spec` (`addDays`, `mondayOf`, `weekdayLabel`), `featured-promo-item-list.spec` (restore + snap, first-tab fallback, default week, grid shape, cell placement, rendering, tabs, week nav, inline form open/close, copy/paste, move guards + calls, delete), `featured-promo-item-form.spec` (new / paste / edit modes, autocomplete search, select pre-fill, required guard, POST / PUT bodies, 409, cancel).
