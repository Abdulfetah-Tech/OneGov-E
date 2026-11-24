import { describe, expect, it } from 'vitest';
import { formatDualDate } from './dates';

const t = (key: string): string =>
  ({ 'calendar.month.3': 'Hidar', 'calendar.era': 'E.C.' })[key] ?? key;

describe('formatDualDate', () => {
  it('shows the Ethiopian date first and the Gregorian date after it', () => {
    // Midday UTC is the same calendar day in every time zone from UTC-11 to UTC+11.
    const text = formatDualDate('2026-11-24T12:00:00Z', t, 'en');

    expect(text.startsWith('15 Hidar 2019 E.C. (')).toBe(true);
    expect(text).toContain('2026');
  });

  it('falls back to the Gregorian date for a date the Ethiopian calendar cannot represent', () => {
    const text = formatDualDate('0050-01-01T12:00:00Z', t, 'en');

    expect(text).not.toContain('E.C.');
  });
});
