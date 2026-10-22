import {
  DeliveryOrder,
  dwellDays,
  formatDate,
  formatDateTime,
  formatPosition,
  orderStatus,
  releasedSummary,
  vietnamToday,
} from './depot-models';

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

it('gives today as a Vietnam date, whatever the UTC date is', () => {
  expect(vietnamToday(new Date('2026-10-22T16:59:00Z'))).toBe('2026-10-22');
  expect(vietnamToday(new Date('2026-10-22T17:00:00Z'))).toBe('2026-10-23');
});

it('formats an API date as dd/MM/yyyy', () => {
  expect(formatDate('2026-10-31')).toBe('31/10/2026');
});

it('formats an instant in Vietnam time', () => {
  expect(formatDateTime('2026-10-22T01:20:00Z')).toBe('22/10/2026 08:20');
  expect(formatDateTime('2026-10-21T18:00:00Z')).toBe('22/10/2026 01:00');
});

describe('delivery order status and summary', () => {
  const order = (isExpired: boolean, lines: [number, number][]) =>
    ({
      isExpired,
      lines: lines.map(([quantity, released], i) => ({
        category: i === 0 ? 'Dry' : 'Reefer',
        quantity,
        releasedQuantity: released,
        remainingQuantity: quantity - released,
      })),
    }) as DeliveryOrder;

  it('is open while a line has quantity left and the order has not expired', () => {
    expect(orderStatus(order(false, [[2, 0], [1, 1]]))).toBe('open');
  });

  it('is done when every line is fully released, even after the expiry date', () => {
    expect(orderStatus(order(false, [[1, 1]]))).toBe('done');
    expect(orderStatus(order(true, [[1, 1]]))).toBe('done');
  });

  it('is expired when quantity is left but the expiry date has passed', () => {
    expect(orderStatus(order(true, [[3, 1]]))).toBe('expired');
  });

  it('summarises released / ordered per type', () => {
    expect(releasedSummary(order(false, [[2, 0], [1, 1]]))).toBe('Dry 0/2 · Reefer 1/1');
  });
});
