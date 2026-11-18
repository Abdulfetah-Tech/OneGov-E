/**
 * Ethiopian (Ge'ez) calendar: 12 months of 30 days plus Pagume (5 days, or 6 when year % 4 === 3).
 * Conversion uses a continuous day count anchored to a known pair (1 Meskerem 2018 E.C. = 2025-09-11).
 * Same algorithm and test vectors as the backend (OneGov.Domain.Calendar.EthiopianDate).
 * All functions take plain year/month/day numbers, so no time-zone conversion can shift a date.
 */
export interface EthiopianDate {
  readonly year: number;
  readonly month: number;
  readonly day: number;
}

export interface GregorianDate {
  readonly year: number;
  readonly month: number;
  readonly day: number;
}

const MS_PER_DAY = 86_400_000;

const dayIndex = (year: number, month: number, day: number): number =>
  365 * (year - 1) + Math.floor(year / 4) + 30 * (month - 1) + (day - 1);

const unixDay = (year: number, month: number, day: number): number =>
  Math.floor(Date.UTC(year, month - 1, day) / MS_PER_DAY);

const OFFSET = unixDay(2025, 9, 11) - dayIndex(2018, 1, 1);

export const isLeapYear = (year: number): boolean => year % 4 === 3;

export function daysInMonth(year: number, month: number): number {
  if (month >= 1 && month <= 12) {
    return 30;
  }
  if (month === 13) {
    return isLeapYear(year) ? 6 : 5;
  }
  throw new RangeError('Month must be between 1 and 13.');
}

export function isValid(date: EthiopianDate): boolean {
  const { year, month, day } = date;
  return (
    Number.isInteger(year) &&
    Number.isInteger(month) &&
    Number.isInteger(day) &&
    year >= 1 &&
    month >= 1 &&
    month <= 13 &&
    day >= 1 &&
    day <= daysInMonth(year, month)
  );
}

/** Gregorian year must be 100 or later (JavaScript's Date.UTC maps 0-99 to 1900-1999). */
export function fromGregorian(year: number, month: number, day: number): EthiopianDate {
  if (year < 100) {
    throw new RangeError('Gregorian year must be 100 or later.');
  }
  const n = unixDay(year, month, day) - OFFSET;
  if (n < 0) {
    throw new RangeError('Date is before the start of the Ethiopian calendar.');
  }
  let y = Math.floor(n / 365.25) + 1;
  while (dayIndex(y + 1, 1, 1) <= n) {
    y++;
  }
  while (dayIndex(y, 1, 1) > n) {
    y--;
  }
  const remainder = n - dayIndex(y, 1, 1);
  return { year: y, month: Math.floor(remainder / 30) + 1, day: (remainder % 30) + 1 };
}

export function toGregorian(date: EthiopianDate): GregorianDate {
  if (!isValid(date)) {
    throw new RangeError('Invalid Ethiopian date.');
  }
  const d = new Date((OFFSET + dayIndex(date.year, date.month, date.day)) * MS_PER_DAY);
  return { year: d.getUTCFullYear(), month: d.getUTCMonth() + 1, day: d.getUTCDate() };
}

/** The user's local calendar date, expressed in the Ethiopian calendar. */
export function todayEthiopian(now: Date = new Date()): EthiopianDate {
  return fromGregorian(now.getFullYear(), now.getMonth() + 1, now.getDate());
}
