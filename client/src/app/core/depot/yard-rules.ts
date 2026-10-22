import { Block, ContainerSize, YardContainer } from './depot-models';

/*
  Same rules as Domain/Services/YardSlotRules.cs and Block.ValidateSlot, used to draw the yard map
  and to warn before submitting (the API still has the final say):
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

// A placed container that keeps its number, so warnings can name it
export interface NumberedContainer extends PlacedContainer {
  containerNumber: string;
}

// Bay/Row/Tier typed in a form (gate-in, move)
export interface SlotInput {
  bay: number | null;
  row: number | null;
  tier: number | null;
}

// One message per field; undefined = nothing to show for that field
export interface SlotErrors {
  bay?: string;
  row?: string;
  tier?: string;
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

// Same as YardSlotRules.HasContainerAbove: a container in the tier just above that shares a covered bay
// rests on this one, so this one cannot leave first (move, gate-out)
export function findContainerAbove<T extends PlacedContainer>(
  bay: number,
  tier: number,
  size: ContainerSize,
  sameRow: readonly T[],
): T | undefined {
  return findOverlap(bay, tier + 1, size, sameRow);
}

// The bay for a container of this size when the user clicks the column of odd bay `oddBay`
export function bayForClick(oddBay: number, size: ContainerSize, maxBays: number): number {
  if (size === 'Ft20') return oddBay;
  // 40ft takes the even bay to the right (covers oddBay and oddBay + 2), or the one to the left
  // at the end of the row, because bay b + 1 must stay inside the block (Block.BayOutOfRange)
  return oddBay + 2 <= maxBays ? oddBay + 1 : oddBay - 1;
}

// Containers of the yard map that have a position, optionally without one visit
// (the container being moved: its current place neither blocks nor supports its new place)
export function toPlaced(containers: readonly YardContainer[], excludeVisitId: string | null = null): NumberedContainer[] {
  return containers
    .filter((c) => c.bay !== null && c.tier !== null && c.visitId !== excludeVisitId)
    .map((c) => ({ bay: c.bay!, tier: c.tier!, size: c.size, containerNumber: c.containerNumber }));
}

/*
  Same checks as Block.ValidateSlot (range, odd/even bay, 40ft inside the block), as messages next to
  the Bay/Row/Tier fields. Shared by gate-in and move so both forms say the same thing.
  showRequired: an empty field is only reported after the user pressed submit.
*/
export function validateSlot(block: Block, slot: SlotInput, size: ContainerSize | undefined, showRequired: boolean): SlotErrors {
  if (block.isVirtual) return {};

  const { bay, row, tier } = slot;
  const required = showRequired ? 'Bắt buộc' : undefined;
  const errors: SlotErrors = {};

  if (bay === null) errors.bay = required;
  else if (bay < 1 || bay > block.maxBays!) errors.bay = `Bay từ 1 đến ${block.maxBays}`;
  else if (size === 'Ft20' && bay % 2 === 0) errors.bay = '20ft xếp bay lẻ';
  else if (size === 'Ft40' && bay % 2 === 1) errors.bay = '40ft xếp bay chẵn';
  else if (size === 'Ft40' && bay + 1 > block.maxBays!) errors.bay = `Bay ${bay + 1} vượt kích thước block`;

  if (row === null) errors.row = required;
  else if (row < 1 || row > block.maxRows!) errors.row = `Row từ 1 đến ${block.maxRows}`;

  if (tier === null) errors.tier = required;
  else if (tier < 1 || tier > block.maxTiers!) errors.tier = `Tier từ 1 đến ${block.maxTiers}`;

  return errors;
}

export function hasSlotErrors(errors: SlotErrors): boolean {
  return !!(errors.bay || errors.row || errors.tier);
}

// Overlap / support warning for a target position (same order as YardPlacement.CheckPlacementAsync)
export function placementWarning(
  bay: number,
  tier: number,
  size: ContainerSize,
  sameRow: readonly NumberedContainer[],
): string | null {
  const overlap = findOverlap(bay, tier, size, sameRow);
  if (overlap) return `Vị trí này trùng với container ${overlap.containerNumber}.`;
  if (!isSupported(bay, tier, size, sameRow)) return `Không có container đỡ bên dưới ở tier ${tier - 1}.`;
  return null;
}
