/**
 * Date helpers for `date` columns, which travel as ISO `yyyy-MM-dd` strings.
 *
 * Both directions work in **local** time. `Date.toISOString()` converts to UTC first, which shifts
 * an evening date back a day for a UTC+8 user — so it is never used here.
 */

/** Serialises a `Date` to `yyyy-MM-dd` from its local components. Null passes through. */
export function toIso(date: Date | null | undefined): string | null {
  if (!date) return null;

  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${year}-${month}-${day}`;
}

/** A new local `Date` shifted by whole days; the input is not mutated. */
export function addDays(date: Date, days: number): Date {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate() + days);
}

/** Local midnight of the Monday on or before `date` — the start of its Monday-to-Sunday week. */
export function mondayOf(date: Date): Date {
  // getDay(): Sunday = 0 … Saturday = 6. Shift so Monday = 0 … Sunday = 6.
  const offset = (date.getDay() + 6) % 7;
  return addDays(date, -offset);
}

/** 一 … 日 for a local `Date`, the way the 上稿作業 day headers show it. */
export function weekdayLabel(date: Date): string {
  return ['日', '一', '二', '三', '四', '五', '六'][date.getDay()];
}

/** Parses `yyyy-MM-dd` (or a longer ISO string) into a local-midnight `Date`. Null passes through. */
export function fromIso(iso: string | null | undefined): Date | null {
  if (!iso) return null;

  const [year, month, day] = iso.slice(0, 10).split('-').map(Number);

  if (!year || !month || !day) return null;

  return new Date(year, month - 1, day);
}
