import { ContainerSize } from './depot-models';

/*
  Same rules as Domain/Services/YardSlotRules.cs, used to draw the yard map and to warn
  before submitting (the API still has the final say):
    - 20ft in odd bay o  -> covers bay o
    - 40ft in even bay b -> covers bays b-1 and b+1
  Two containers in the same row and tier clash when they share a covered bay.
  Above tier 1, every covered bay needs a container just below.
*/
export interface PlacedContainer {
  bay: number;
  tier: number;
  size: ContainerSize;
}

export function footprint(bay: number, size: ContainerSize): number[] {
  return size === 'Ft40' ? [bay - 1, bay + 1] : [bay];
}

// The container in the same tier that shares a covered bay, if any
export function findOverlap<T extends PlacedContainer>(
  bay: number,
  tier: number,
  size: ContainerSize,
  sameRow: readonly T[],
): T | undefined {
  const covered = footprint(bay, size);
  return sameRow.find((p) => p.tier === tier && footprint(p.bay, p.size).some((b) => covered.includes(b)));
}

export function isSupported(bay: number, tier: number, size: ContainerSize, sameRow: readonly PlacedContainer[]): boolean {
  if (tier === 1) return true;
  const coveredBelow = new Set(sameRow.filter((p) => p.tier === tier - 1).flatMap((p) => footprint(p.bay, p.size)));
  return footprint(bay, size).every((b) => coveredBelow.has(b));
}

// The bay for a container of this size when the user clicks the column of odd bay `oddBay`
export function bayForClick(oddBay: number, size: ContainerSize, maxBays: number): number {
  if (size === 'Ft20') return oddBay;
  // 40ft takes the even bay to the right (covers oddBay and oddBay + 2), or the one to the left
  // at the end of the row, because bay b + 1 must stay inside the block (Block.BayOutOfRange)
  return oddBay + 2 <= maxBays ? oddBay + 1 : oddBay - 1;
}
