import { addDays, fromIso, mondayOf, toIso, weekdayLabel } from './date.util';

describe('date.util', () => {
  it('toIso() serialises from local components, never via UTC', () => {
    // 23:30 local on the 15th would already be the 16th (or 15th) in UTC depending on the zone;
    // the local date must win.
    const lateEvening = new Date(2026, 8, 15, 23, 30);

    expect(toIso(lateEvening)).toBe('2026-09-15');
  });

  it('toIso() zero-pads the month and day', () => {
    expect(toIso(new Date(2026, 0, 5))).toBe('2026-01-05');
  });

  it('toIso() passes null and undefined through', () => {
    expect(toIso(null)).toBeNull();
    expect(toIso(undefined)).toBeNull();
  });

  it('fromIso() parses to local midnight', () => {
    const parsed = fromIso('2026-09-15');

    expect(parsed?.getFullYear()).toBe(2026);
    expect(parsed?.getMonth()).toBe(8);
    expect(parsed?.getDate()).toBe(15);
    expect(parsed?.getHours()).toBe(0);
  });

  it('fromIso() ignores a time suffix', () => {
    expect(fromIso('2026-09-15T00:00:00')?.getDate()).toBe(15);
  });

  it('fromIso() passes null, undefined and garbage through as null', () => {
    expect(fromIso(null)).toBeNull();
    expect(fromIso(undefined)).toBeNull();
    expect(fromIso('')).toBeNull();
    expect(fromIso('not-a-date')).toBeNull();
  });

  it('round-trips a date', () => {
    expect(toIso(fromIso('2030-02-28'))).toBe('2030-02-28');
  });

  it('addDays() crosses month boundaries without mutating the input', () => {
    const source = new Date(2026, 2, 30);

    expect(toIso(addDays(source, 3))).toBe('2026-04-02');
    expect(toIso(addDays(source, -30))).toBe('2026-02-28');
    expect(toIso(source)).toBe('2026-03-30');
  });

  it('mondayOf() snaps to the Monday on or before the date', () => {
    expect(toIso(mondayOf(new Date(2026, 2, 16)))).toBe('2026-03-16'); // Monday stays
    expect(toIso(mondayOf(new Date(2026, 2, 18)))).toBe('2026-03-16'); // Wednesday
    expect(toIso(mondayOf(new Date(2026, 2, 21)))).toBe('2026-03-16'); // Saturday
    expect(toIso(mondayOf(new Date(2026, 2, 22)))).toBe('2026-03-16'); // Sunday → the Monday before
    expect(toIso(mondayOf(new Date(2026, 2, 23)))).toBe('2026-03-23'); // next Monday
  });

  it('weekdayLabel() uses the Chinese numerals with 日 for Sunday', () => {
    expect(weekdayLabel(new Date(2026, 2, 16))).toBe('一');
    expect(weekdayLabel(new Date(2026, 2, 21))).toBe('六');
    expect(weekdayLabel(new Date(2026, 2, 22))).toBe('日');
  });
});
