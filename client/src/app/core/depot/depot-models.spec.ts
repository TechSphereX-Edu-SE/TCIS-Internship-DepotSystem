import { dwellDays, formatPosition } from './depot-models';

describe('depot display helpers', () => {
  it('formats a physical position with a 2-digit bay, a virtual block by its code', () => {
    expect(formatPosition('A', 4, 1, 1)).toBe('A 04-1-1');
    expect(formatPosition('WAIT', null, null, null)).toBe('WAIT');
  });

  it('counts dwell days in Vietnam calendar days (gate-in day = 0)', () => {
    // 22/10 08:20 in Vietnam = 22/10 01:20 UTC
    const gateInAt = '2026-10-22T01:20:00Z';
    expect(dwellDays(gateInAt, new Date('2026-10-22T16:59:00Z'))).toBe(0); // 22/10 23:59 VN
    expect(dwellDays(gateInAt, new Date('2026-10-22T17:00:00Z'))).toBe(1); // 23/10 00:00 VN
    expect(dwellDays(gateInAt, new Date('2026-11-01T03:00:00Z'))).toBe(10);
  });

  it('uses the Vietnam date of a late-evening UTC gate-in, not the UTC date', () => {
    // 21/10 18:00 UTC is already 22/10 01:00 in Vietnam
    expect(dwellDays('2026-10-21T18:00:00Z', new Date('2026-10-22T05:00:00Z'))).toBe(0);
  });
});
