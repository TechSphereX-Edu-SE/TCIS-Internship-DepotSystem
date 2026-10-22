import { Component, computed, input, output } from '@angular/core';
import { Block, ContainerSize, YardContainer } from '../../core/depot/depot-models';
import { findOverlap, footprint, isSupported } from '../../core/depot/yard-rules';

// Position chosen in the form, drawn on the map (bay may be odd or even depending on the size)
export interface MapSelection {
  bay: number | null;
  tier: number | null;
  size: ContainerSize | null;
}

/*
  Side view of one row of a physical block: columns = odd bays (20ft units), rows = tiers (top = highest).
  A 40ft container in even bay b is drawn over the columns of bays b-1 and b+1.
  Clicking an empty cell emits the odd bay and tier; the page turns it into a bay for the container size.
*/
@Component({
  selector: 'app-yard-map',
  templateUrl: './yard-map.html',
  styleUrl: './yard-map.css',
})
export class YardMap {
  readonly block = input.required<Block>();
  // Containers of the row shown (the page loads them for the selected block and row)
  readonly containers = input<YardContainer[]>([]);
  readonly selection = input<MapSelection>({ bay: null, tier: null, size: null });
  readonly pick = output<{ oddBay: number; tier: number }>();

  protected readonly bays = computed(() => {
    const result: number[] = [];
    for (let bay = 1; bay <= (this.block().maxBays ?? 0); bay += 2) result.push(bay);
    return result;
  });

  protected readonly tiers = computed(() => {
    const max = this.block().maxTiers ?? 0;
    return Array.from({ length: max }, (_, i) => max - i);
  });

  protected readonly columns = computed(() => `repeat(${this.bays().length}, minmax(40px, 1fr))`);

  // Containers placed on the grid: grid column from the first covered odd bay, row from the tier
  protected readonly boxes = computed(() =>
    this.containers()
      .filter((c) => c.bay !== null && c.tier !== null)
      .map((c) => {
        const is40 = c.size === 'Ft40';
        return {
          visitId: c.visitId,
          is40,
          damaged: c.condition === 'Damaged',
          area: this.gridArea(footprint(c.bay!, c.size)[0], is40 ? 2 : 1, c.tier!),
          label: is40 ? c.containerNumber : `${c.containerNumber.slice(0, 4)}…${c.containerNumber.slice(-3)}`,
          sub: is40 ? `40' · bay ${pad(c.bay!)}` : "20'",
          title: `${c.containerNumber} · ${c.isoCode} · ${c.lineOperatorCode} · Phân loại ${c.grade}`,
        };
      }),
  );

  // Every (odd bay, tier) not covered by a container is a clickable empty cell
  protected readonly emptyCells = computed(() => {
    const placed = this.placed();
    const taken = new Set(placed.flatMap((p) => footprint(p.bay, p.size).map((bay) => `${bay}-${p.tier}`)));
    return this.tiers().flatMap((tier) =>
      this.bays()
        .filter((bay) => !taken.has(`${bay}-${tier}`))
        .map((bay) => ({ key: `${bay}-${tier}`, bay, tier, area: this.gridArea(bay, 1, tier) })),
    );
  });

  // The position typed in the form; red when it overlaps a container or has nothing underneath
  protected readonly selected = computed(() => {
    const { bay, tier, size } = this.selection();
    if (bay === null || tier === null || size === null) return null;

    const covered = footprint(bay, size);
    const maxBays = this.block().maxBays ?? 0;
    const maxTiers = this.block().maxTiers ?? 0;
    if (covered[0] < 1 || covered[covered.length - 1] > maxBays || tier < 1 || tier > maxTiers) return null;
    if (covered.some((b) => b % 2 === 0)) return null;

    const placed = this.placed();
    const conflict = !!findOverlap(bay, tier, size, placed) || !isSupported(bay, tier, size, placed);
    return { area: this.gridArea(covered[0], covered.length, tier), conflict };
  });

  private readonly placed = computed(() =>
    this.containers()
      .filter((c) => c.bay !== null && c.tier !== null)
      .map((c) => ({ bay: c.bay!, tier: c.tier!, size: c.size })),
  );

  protected pad(bay: number): string {
    return pad(bay);
  }

  // CSS grid-area "row / column / span rows / span columns" for an odd bay and a tier
  private gridArea(oddBay: number, span: number, tier: number): string {
    const column = (oddBay - 1) / 2 + 1;
    const row = (this.block().maxTiers ?? 0) - tier + 1;
    return `${row} / ${column} / span 1 / span ${span}`;
  }
}

function pad(bay: number): string {
  return String(bay).padStart(2, '0');
}
