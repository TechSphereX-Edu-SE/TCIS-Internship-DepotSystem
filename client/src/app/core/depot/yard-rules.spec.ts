import { bayForClick, findOverlap, footprint, isSupported, PlacedContainer } from './yard-rules';

describe('yard rules', () => {
  // Row used by the tests: 20ft at bay 1, 40ft at bay 4 (covers 3 and 5), 40ft on top of it at tier 2
  const row: PlacedContainer[] = [
    { bay: 1, tier: 1, size: 'Ft20' },
    { bay: 4, tier: 1, size: 'Ft40' },
    { bay: 4, tier: 2, size: 'Ft40' },
  ];

  it('maps a position to the odd bays it covers', () => {
    expect(footprint(7, 'Ft20')).toEqual([7]);
    expect(footprint(4, 'Ft40')).toEqual([3, 5]);
  });

  it('finds the container sharing a covered bay in the same tier', () => {
    expect(findOverlap(5, 1, 'Ft20', row)).toEqual(row[1]);
    expect(findOverlap(2, 1, 'Ft40', row)).toEqual(row[0]);
    expect(findOverlap(7, 1, 'Ft20', row)).toBeUndefined();
  });

  it('needs every covered bay to be held just below', () => {
    expect(isSupported(7, 1, 'Ft20', row)).toBe(true);
    expect(isSupported(3, 3, 'Ft20', row)).toBe(true);
    expect(isSupported(7, 2, 'Ft20', row)).toBe(false);
    expect(isSupported(2, 2, 'Ft40', row)).toBe(true);
    expect(isSupported(6, 2, 'Ft40', row)).toBe(false);
  });

  it('picks the bay for a click on an odd-bay column', () => {
    expect(bayForClick(9, 'Ft20', 20)).toBe(9);
    expect(bayForClick(9, 'Ft40', 20)).toBe(10);
    expect(bayForClick(19, 'Ft40', 20)).toBe(18);
  });
});
