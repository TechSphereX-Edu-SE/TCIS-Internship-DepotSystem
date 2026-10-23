import {
  DailyThroughput,
  DeliveryOrder,
  addDays,
  assignmentCovers,
  blockAccepts,
  categoriesText,
  dailyTotals,
  daysInRange,
  dwellDays,
  formatDate,
  formatDateTime,
  formatPosition,
  orderStatus,
  PHOTO_MAX_BYTES,
  REPAIR_STATUSES,
  REPAIR_STATUS_LABEL,
  partnerRolesText,
  photoFileError,
  releasedSummary,
  vietnamToday,
} from './depot-models';

describe('depot display helpers', () => {
  it('checks a block by its list of types, not by its name', () => {
    // A block named "khu khô" that only takes Reefer still refuses Dry
    expect(blockAccepts({ allowedCategories: ['Reefer'] }, 'Reefer')).toBe(true);
    expect(blockAccepts({ allowedCategories: ['Reefer'] }, 'Dry')).toBe(false);
  });

  it('lists block types in enum order, or "Tất cả loại" for every type', () => {
    expect(categoriesText(['OpenTop', 'Dry'])).toBe('Dry, Open Top');
    expect(
      categoriesText(['Dry', 'Reefer', 'OpenTop', 'FlatRack', 'Bunker', 'Ventilated', 'Specialized']),
    ).toBe('Tất cả loại');
  });

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
  
  it('adds days across month and year ends', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2026-10-22', -6)).toBe('2026-10-16');
  });

  it('counts the days of a range with both ends included', () => {
    expect(daysInRange('2026-10-18', '2026-10-22')).toBe(5);
    expect(daysInRange('2026-10-22', '2026-10-22')).toBe(1);
    expect(daysInRange('2026-10-23', '2026-10-22')).toBe(0);
    expect(daysInRange('2026-01-01', '2026-12-31')).toBe(365);
  });

  it('totals every date of the range, with zeros for quiet days', () => {
    const rows = [
      { date: '2026-10-18', lineOperatorCode: 'CMA', gateIn: 1, gateOut: 0 },
      { date: '2026-10-18', lineOperatorCode: 'MSC', gateIn: 2, gateOut: 1 },
      { date: '2026-10-20', lineOperatorCode: 'MSC', gateIn: 0, gateOut: 3 },
    ] as DailyThroughput[];

    expect(dailyTotals('2026-10-18', '2026-10-20', rows)).toEqual([
      { date: '2026-10-18', gateIn: 3, gateOut: 1 },
      { date: '2026-10-19', gateIn: 0, gateOut: 0 },
      { date: '2026-10-20', gateIn: 0, gateOut: 3 },
    ]);
  });
});

describe('partners and operating assignments', () => {
  it('lists the roles of a partner', () => {
    expect(partnerRolesText({ isOwner: true, isOperator: true })).toBe('Chủ sở hữu, Hãng khai thác');
    expect(partnerRolesText({ isOwner: true, isOperator: false })).toBe('Chủ sở hữu');
    expect(partnerRolesText({ isOwner: false, isOperator: true })).toBe('Hãng khai thác');
  });

  it('only counts a confirmed assignment whose period includes the date (both ends included)', () => {
    const period = { status: 'Confirmed' as const, validFrom: '2026-10-01', validTo: '2026-10-31' };

    expect(assignmentCovers(period, '2026-10-01')).toBe(true);
    expect(assignmentCovers(period, '2026-10-31')).toBe(true);
    expect(assignmentCovers(period, '2026-11-01')).toBe(false);
    expect(assignmentCovers({ ...period, validTo: null }, '2027-05-01')).toBe(true);
    expect(assignmentCovers({ ...period, status: 'Pending' }, '2026-10-15')).toBe(false);
    expect(assignmentCovers({ ...period, status: 'Cancelled' }, '2026-10-15')).toBe(false);
  });

  // Q4: the same limits as the API (RepairPhotoRules), checked before anything is uploaded
  it('accepts only JPG / PNG photos of at most 5 MB', () => {
    expect(photoFileError({ name: 'a.jpg', type: 'image/jpeg', size: 1000 })).toBeNull();
    expect(photoFileError({ name: 'a.png', type: 'image/png', size: PHOTO_MAX_BYTES })).toBeNull();
    expect(photoFileError({ name: 'a.pdf', type: 'application/pdf', size: 1000 })).toContain('JPG hoặc PNG');
    expect(photoFileError({ name: 'big.png', type: 'image/png', size: PHOTO_MAX_BYTES + 1 })).toContain('5 MB');
    expect(photoFileError({ name: 'empty.png', type: 'image/png', size: 0 })).toContain('rỗng');
  });

  it('labels every repair status, ending with Đạt', () => {
    expect(REPAIR_STATUSES.map((s) => REPAIR_STATUS_LABEL[s])).toEqual([
      'Chờ giám định',
      'Chờ duyệt phương án',
      'Đã duyệt sửa',
      'Đang sửa',
      'Chờ nghiệm thu',
      'Đạt',
    ]);
  });
});
