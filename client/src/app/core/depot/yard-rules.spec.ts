import { Block, YardContainer } from './depot-models';
import {
  bayForClick,
  findContainerAbove,
  findOverlap,
  footprint,
  isSupported,
  NumberedContainer,
  PlacedContainer,
  placementWarning,
  toPlaced,
  validateSlot,
} from './yard-rules';

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

  it('finds the container resting on a position (tier just above, shared bay)', () => {
    expect(findContainerAbove(4, 1, 'Ft40', row)).toEqual(row[2]);
    expect(findContainerAbove(4, 2, 'Ft40', row)).toBeUndefined();
    // The 40ft at tier 2 covers bays 3 and 5, not bay 1
    expect(findContainerAbove(1, 1, 'Ft20', row)).toBeUndefined();
  });

  it('picks the bay for a click on an odd-bay column', () => {
    expect(bayForClick(9, 'Ft20', 20)).toBe(9);
    expect(bayForClick(9, 'Ft40', 20)).toBe(10);
    expect(bayForClick(19, 'Ft40', 20)).toBe(18);
  });

  it('keeps only positioned containers and leaves out the excluded visit', () => {
    const containers = [
      { visitId: 'v1', containerNumber: 'MSCU1234566', bay: 1, tier: 1, size: 'Ft20' },
      { visitId: 'v2', containerNumber: 'CMAU7654327', bay: 4, tier: 1, size: 'Ft40' },
      { visitId: 'v3', containerNumber: 'HMMU3007776', bay: null, tier: null, size: 'Ft20' },
    ] as YardContainer[];

    expect(toPlaced(containers).map((c) => c.containerNumber)).toEqual(['MSCU1234566', 'CMAU7654327']);
    expect(toPlaced(containers, 'v2')).toEqual([{ bay: 1, tier: 1, size: 'Ft20', containerNumber: 'MSCU1234566' }]);
  });

  describe('validateSlot (same checks as Block.ValidateSlot)', () => {
    const blockA = { isVirtual: false, maxBays: 20, maxRows: 6, maxTiers: 5 } as Block;

    it('accepts a position that fits the block and the size', () => {
      expect(validateSlot(blockA, { bay: 7, row: 1, tier: 1 }, 'Ft20', true)).toEqual({});
      expect(validateSlot(blockA, { bay: 18, row: 6, tier: 5 }, 'Ft40', true)).toEqual({});
    });

    it('reports bay parity, ranges and a 40ft going past the last bay', () => {
      expect(validateSlot(blockA, { bay: 4, row: 1, tier: 1 }, 'Ft20', false).bay).toBe('20ft xếp bay lẻ');
      expect(validateSlot(blockA, { bay: 5, row: 1, tier: 1 }, 'Ft40', false).bay).toBe('40ft xếp bay chẵn');
      expect(validateSlot(blockA, { bay: 20, row: 1, tier: 1 }, 'Ft40', false).bay).toBe('Bay 21 vượt kích thước block');
      expect(validateSlot(blockA, { bay: 1, row: 7, tier: 6 }, 'Ft20', false)).toEqual({
        row: 'Row từ 1 đến 6',
        tier: 'Tier từ 1 đến 5',
      });
    });

    it('reports empty fields only after submit, and nothing for a virtual block', () => {
      const empty = { bay: null, row: null, tier: null };
      expect(validateSlot(blockA, empty, 'Ft20', false)).toEqual({ bay: undefined, row: undefined, tier: undefined });
      expect(validateSlot(blockA, empty, 'Ft20', true)).toEqual({ bay: 'Bắt buộc', row: 'Bắt buộc', tier: 'Bắt buộc' });
      expect(validateSlot({ isVirtual: true } as Block, empty, 'Ft20', true)).toEqual({});
    });
  });

  it('names the overlapping container, then checks the support below', () => {
    const numbered: NumberedContainer[] = row.map((p, i) => ({ ...p, containerNumber: `C${i}` }));
    expect(placementWarning(5, 1, 'Ft20', numbered)).toBe('Vị trí này trùng với container C1.');
    expect(placementWarning(7, 2, 'Ft20', numbered)).toBe('Không có container đỡ bên dưới ở tier 1.');
    expect(placementWarning(7, 1, 'Ft20', numbered)).toBeNull();
  });
});
