import { fromGregorian } from '../calendar/ethiopian-date';

/**
 * A date in both calendars, Ethiopian first, for example "15 Hidar 2019 E.C. (Nov 24, 2026)".
 * Uses the viewer's local date. Falls back to the Gregorian date alone if the date is outside the Ethiopian calendar's range.
 */
export function formatDualDate(iso: string, t: (key: string) => string, lang: string): string {
  const date = new Date(iso);
  const gregorian = new Intl.DateTimeFormat(lang, { dateStyle: 'medium' }).format(date);
  try {
    const e = fromGregorian(date.getFullYear(), date.getMonth() + 1, date.getDate());
    return `${e.day} ${t('calendar.month.' + e.month)} ${e.year} ${t('calendar.era')} (${gregorian})`;
  } catch {
    return gregorian;
  }
}
