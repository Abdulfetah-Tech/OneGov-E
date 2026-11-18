import { describe, expect, it } from 'vitest';
import { daysInMonth, fromGregorian, isLeapYear, isValid, toGregorian } from './ethiopian-date';

const KNOWN: ReadonlyArray<readonly [[number, number, number], [number, number, number]]> = [
  [[2018, 1, 1], [2025, 9, 11]], // Enkutatash 2018
  [[2016, 1, 1], [2023, 9, 12]], // new year after a leap year
  [[2015, 13, 6], [2023, 9, 11]], // Pagume 6 in leap year 2015
  [[2017, 13, 5], [2025, 9, 10]],
  [[2017, 4, 29], [2025, 1, 7]], // Genna
  [[2017, 6, 23], [2025, 3, 2]], // Adwa Victory Day
  [[2019, 3, 6], [2026, 11, 15]],
];

describe('Ethiopian calendar', () => {
  it.each(KNOWN)('converts %j <-> %j', (eth, greg) => {
    expect(toGregorian({ year: eth[0], month: eth[1], day: eth[2] })).toEqual({
      year: greg[0],
      month: greg[1],
      day: greg[2],
    });
    expect(fromGregorian(greg[0], greg[1], greg[2])).toEqual({ year: eth[0], month: eth[1], day: eth[2] });
  });

  it('round-trips every day from 1900 to 2100', () => {
    for (let t = Date.UTC(1900, 0, 1); t < Date.UTC(2100, 0, 1); t += 86_400_000) {
      const d = new Date(t);
      const g = { year: d.getUTCFullYear(), month: d.getUTCMonth() + 1, day: d.getUTCDate() };
      const e = fromGregorian(g.year, g.month, g.day);
      expect(isValid(e)).toBe(true);
      expect(toGregorian(e)).toEqual(g);
    }
  });

  it('knows leap years and Pagume length', () => {
    expect(isLeapYear(2015)).toBe(true);
    expect(isLeapYear(2016)).toBe(false);
    expect(daysInMonth(2015, 13)).toBe(6);
    expect(daysInMonth(2016, 13)).toBe(5);
  });

  it('rejects invalid dates', () => {
    expect(isValid({ year: 2016, month: 13, day: 6 })).toBe(false);
    expect(isValid({ year: 2018, month: 14, day: 1 })).toBe(false);
    expect(isValid({ year: 2018, month: 1, day: 31 })).toBe(false);
    expect(() => toGregorian({ year: 2018, month: 0, day: 1 })).toThrow(RangeError);
  });
});
