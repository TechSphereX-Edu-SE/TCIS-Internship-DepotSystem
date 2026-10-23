import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import {
  Block,
  CATEGORIES,
  CATEGORY_LABEL,
  CONDITION_LABEL,
  ContainerCategory,
  ContainerPosition,
  ContainerSize,
  LineOperator,
  MoveRequest,
  REPAIR_MARKER_LABEL,
  REPAIR_STATUS_LABEL,
  SIZE_LABEL,
  YARD_REPAIR_STATE_LABEL,
  YardContainer,
  YardRepairState,
  YardSearchFilter,
  dwellDays,
  formatPosition,
  repairMarker,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { YardApi } from '../../core/depot/yard-api';
import {
  SlotErrors,
  bayForClick,
  findContainerAbove,
  footprint,
  hasSlotErrors,
  placementWarning,
  toPlaced,
  validateSlot,
} from '../../core/depot/yard-rules';
import { Pager } from '../../shared/pager/pager';
import { SearchPageLoader, SearchSelect } from '../../shared/search-select/search-select';
import { ToastService } from '../../shared/toast/toast-service';
import { YardMap } from '../../shared/yard-map/yard-map';

const PAGE_SIZE = 20;
const REPAIR_STATES: YardRepairState[] = ['Abnormal', 'InRepair', 'AnyOpen', 'None'];

// Chip of a repair mark in the lists: amber = chờ giám định, red = cần sửa / đang xử lý
const MARKER_CHIP = { abnormal: 'chip-amber', repair: 'chip-red' } as const;

/*
  Spec section 3: "current position in the yard" and re-stacking (đảo chuyển).
  One screen (mockup 03 + feedback 5 / 8):
    - block cards: what is in each block, and which block the map shows
    - lookup: a search dropdown of the containers in the yard (catalog-only, gone and outside-repair
      containers have no open visit, so they never show up); picking one opens its block and row
    - list: the containers in the yard with filters (number, block, type, size; more: operator,
      repair state, dwell days), filtered and paged by the API
    - move: target block + Bay/Row/Tier, with the same checks as the API shown before submitting;
      the move sends where the container was seen, so a container moved meanwhile is not moved blindly
  Repair marks: "chờ giám định" and "cần sửa / đang xử lý" are shown apart, on the map and in the lists.
  The API has the final say (POST /api/container-visits/move); the local checks only avoid
  sending requests that are sure to fail and explain why in Vietnamese.
*/
@Component({
  selector: 'app-yard-page',
  imports: [ReactiveFormsModule, DatePipe, RouterLink, YardMap, SearchSelect, Pager],
  templateUrl: './yard-page.html',
  styleUrl: './yard-page.css',
})
export class YardPage {
  private readonly masterData = inject(MasterDataApi);
  private readonly yard = inject(YardApi);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly conditionLabel = CONDITION_LABEL;
  protected readonly repairStatusLabel = REPAIR_STATUS_LABEL;
  protected readonly markerLabel = REPAIR_MARKER_LABEL;
  protected readonly markerChip = MARKER_CHIP;
  protected readonly repairStateLabel = YARD_REPAIR_STATE_LABEL;
  protected readonly repairStates = REPAIR_STATES;
  protected readonly categories = CATEGORIES;
  protected readonly sizes: ContainerSize[] = ['Ft20', 'Ft40'];
  protected readonly formatPosition = formatPosition;
  protected readonly repairMarker = repairMarker;
  protected readonly dwellDays = dwellDays;

  // Lookup: pick a container in the yard (the dropdown never keeps half-typed text)
  protected readonly lookup = new FormControl<YardContainer | null>(null);
  protected readonly loadLookup: SearchPageLoader<YardContainer> = (term, page) => this.yard.search(page, PAGE_SIZE, { search: term });
  protected readonly lookupKey = (c: YardContainer) => c.visitId;
  protected readonly lookupLabel = (c: YardContainer) => c.containerNumber;
  protected readonly lookupDetail = (c: YardContainer) => {
    const marker = repairMarker(c);
    return (
      `${SIZE_LABEL[c.size]} ${CATEGORY_LABEL[c.category]} · ${formatPosition(c.blockCode, c.bay, c.row, c.tier)} · ` +
      (marker ? REPAIR_MARKER_LABEL[marker] : CONDITION_LABEL[c.condition])
    );
  };

  // List filters (feedback 8): main ones always visible, the others under "Lọc thêm"
  protected readonly filters = this.fb.group({
    search: [''],
    blockId: [''],
    category: ['' as ContainerCategory | ''],
    size: ['' as ContainerSize | ''],
    operatorId: [''],
    repair: ['' as YardRepairState | ''],
    minDwellDays: new FormControl<number | null>(null),
    maxDwellDays: new FormControl<number | null>(null),
  });
  protected readonly moreFilters = signal(false);
  protected readonly operators = signal<LineOperator[]>([]);
  protected readonly items = signal<YardContainer[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly listError = signal<string | null>(null);

  // Same fields as MoveContainerCommand (containerId comes from the container found).
  // blockId is also the block the map shows, so picking a target and looking at it are one action.
  protected readonly moveForm = this.fb.group({
    blockId: ['', Validators.required],
    bay: new FormControl<number | null>(null),
    row: new FormControl<number | null>(1),
    tier: new FormControl<number | null>(null),
  });

  protected readonly blocks = signal<Block[]>([]);
  protected readonly loadError = signal<string | null>(null);
  // Container being looked at / moved (response of GET position or POST move)
  protected readonly current = signal<ContainerPosition | null>(null);
  protected readonly searchError = signal<string | null>(null);
  // Containers of the row where the current container stands, to know if one rests on it
  protected readonly currentRowContainers = signal<YardContainer[]>([]);
  // Containers of the block and row shown on the map (or of the virtual block shown as a list)
  protected readonly mapContainers = signal<YardContainer[]>([]);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  // Bumped after a move so the map, the current row and the block counts reload
  private readonly refresh = signal(0);

  // Move form values as a signal, so computed() below reacts to typing
  private readonly value = toSignal(
    this.moveForm.valueChanges.pipe(
      startWith(null),
      map(() => this.moveForm.getRawValue()),
    ),
    { initialValue: this.moveForm.getRawValue() },
  );

  protected readonly targetBlock = computed(() => this.blocks().find((b) => b.id === this.value().blockId) ?? null);

  // Row shown on the map = row typed in the form (when it is inside the block), otherwise row 1
  protected readonly mapRow = computed(() => {
    const block = this.targetBlock();
    const row = this.value().row;
    return block?.maxRows && row && row >= 1 && row <= block.maxRows ? row : 1;
  });

  protected readonly rowOptions = computed(() => Array.from({ length: this.targetBlock()?.maxRows ?? 0 }, (_, i) => i + 1));

  protected readonly mapSelection = computed(() => ({
    bay: this.value().bay,
    tier: this.value().tier,
    size: this.current()?.size ?? null,
  }));

  // "40ft (chiếm bay 03 và 05)" - the odd bays a container really takes, as in the mockup
  protected readonly sizeText = computed(() => {
    const c = this.current();
    if (!c) return '';
    if (c.size !== 'Ft40' || c.bay === null) return SIZE_LABEL[c.size];
    const [left, right] = footprint(c.bay, c.size).map((b) => String(b).padStart(2, '0'));
    return `40ft (chiếm bay ${left} và ${right})`;
  });

  protected readonly currentDwellDays = computed(() => {
    const c = this.current();
    return c ? dwellDays(c.gateInAt) : 0;
  });

  protected readonly currentMarker = computed(() => {
    const c = this.current();
    return c ? repairMarker(c) : null;
  });

  // Same check as YardPlacement.CheckNothingAboveAsync: the box on top would be left floating
  protected readonly containerAbove = computed(() => {
    const c = this.current();
    if (!c || c.bay === null || c.tier === null) return null;
    return findContainerAbove(c.bay, c.tier, c.size, toPlaced(this.currentRowContainers(), c.visitId)) ?? null;
  });

  // Same checks as Block.ValidateSlot, shared with gate-in
  protected readonly positionErrors = computed<SlotErrors>(() => {
    const block = this.targetBlock();
    const c = this.current();
    return block && c ? validateSlot(block, this.value(), c.size, this.submitted()) : {};
  });

  // Same as the API's 400 SamePosition: nothing would change
  protected readonly samePosition = computed(() => {
    const c = this.current();
    const block = this.targetBlock();
    if (!c || !block) return false;
    const { bay, row, tier } = this.value();
    return block.id === c.blockId && (block.isVirtual || (bay === c.bay && row === c.row && tier === c.tier));
  });

  // Overlap / support at the target, ignoring the container itself (same as excludeVisitId in the API)
  protected readonly slotWarning = computed(() => {
    const c = this.current();
    const block = this.targetBlock();
    const { bay, tier } = this.value();
    if (!c || !block || block.isVirtual || bay === null || tier === null || hasSlotErrors(this.positionErrors())) {
      return null;
    }
    return placementWarning(bay, tier, c.size, toPlaced(this.mapContainers(), c.visitId));
  });

  // The API would reject these anyway, so the button stays disabled and the reason is shown instead
  protected readonly blocked = computed(() => !!this.containerAbove() || this.samePosition());

  constructor() {
    this.loadBlocks();
    this.loadPage(1);
    this.masterData.lineOperators('Operator').subscribe({
      next: (operators) => this.operators.set(operators),
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });

    // A container picked in the lookup: open it (its position is read again, it may have moved)
    this.lookup.valueChanges.pipe(takeUntilDestroyed()).subscribe((item) => {
      if (item) this.openContainer(item.containerNumber);
    });

    // Map / list of the target block: one row of a physical block, the whole virtual block.
    // Reloads when the block or row changes, or after a move.
    toObservable(computed(() => ({ block: this.targetBlock(), row: this.mapRow(), tick: this.refresh() })))
      .pipe(
        switchMap(({ block, row }) => {
          if (!block) return of([]);
          const row$ = block.isVirtual ? this.yard.inYard(block.id) : this.yard.inYard(block.id, row);
          return row$.pipe(catchError(() => of([])));
        }),
        takeUntilDestroyed(),
      )
      .subscribe((containers) => this.mapContainers.set(containers));

    // Row of the current container, for the "container above" check (it may differ from the map row)
    toObservable(computed(() => ({ current: this.current(), tick: this.refresh() })))
      .pipe(
        switchMap(({ current }) =>
          current && current.row !== null
            ? this.yard.inYard(current.blockId, current.row).pipe(catchError(() => of([])))
            : of([]),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((containers) => this.currentRowContainers.set(containers));

    // Virtual block: no Bay/Row/Tier. Physical block: fields enabled again
    this.moveForm.controls.blockId.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.applyBlockRules());
  }

  protected applyFilters(): void {
    this.loadPage(1);
  }

  protected clearFilters(): void {
    this.filters.reset();
    this.loadPage(1);
  }

  protected goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) this.loadPage(page);
  }

  // Clicking a container on the map, in a virtual block list or in the list = looking it up by number
  protected pickContainer(container: YardContainer): void {
    this.openContainer(container.containerNumber);
  }

  // Block cards switch the block shown on the map (and the target of the move)
  protected showBlock(block: Block): void {
    this.moveForm.controls.blockId.setValue(block.id);
  }

  protected pickOnMap(cell: { oddBay: number; tier: number }): void {
    const block = this.targetBlock();
    const c = this.current();
    if (!block?.maxBays || !c) return;
    this.moveForm.patchValue({ bay: bayForClick(cell.oddBay, c.size, block.maxBays), tier: cell.tier, row: this.mapRow() });
  }

  protected selectMapRow(row: string): void {
    this.moveForm.controls.row.setValue(Number(row));
  }

  protected submit(): void {
    this.submitted.set(true);
    this.apiError.set(null);

    const c = this.current();
    const block = this.targetBlock();
    if (!c || !block || this.moveForm.invalid || hasSlotErrors(this.positionErrors()) || this.blocked()) return;

    const v = this.moveForm.getRawValue();
    const request: MoveRequest = {
      containerId: c.containerId,
      blockId: block.id,
      // A virtual block takes no position (Block.VirtualHasNoSlot)
      bay: block.isVirtual ? null : v.bay,
      row: block.isVirtual ? null : v.row,
      tier: block.isVirtual ? null : v.tier,
      // Where the clerk saw it: 409 PositionChanged if someone moved it meanwhile
      fromBlockId: c.blockId,
      fromBay: c.bay,
      fromRow: c.row,
      fromTier: c.tier,
    };

    const from = this.positionText(c);
    this.submitting.set(true);
    this.yard.move(request).subscribe({
      next: (moved) => {
        this.toast.success(`Đã chuyển ${moved.containerNumber} từ ${from} sang ${this.positionText(moved)}`);
        // Stay on the container at its new place, ready for another move if needed
        this.current.set(moved);
        this.clearTarget();
        this.refresh.update((n) => n + 1);
        this.loadBlocks();
        this.loadPage(this.page());
        this.submitting.set(false);
      },
      error: (err: unknown) => {
        const error = toApiError(err);
        this.apiError.set(error);
        this.submitting.set(false);
        // Moved by someone else: show where it is now, the clerk checks again before moving
        if (error.code === 'ContainerVisit.PositionChanged') this.reloadCurrent(c.containerNumber);
      },
    });
  }

  // "Huỷ": keep the container and the block shown, forget the target typed so far
  protected clearTarget(): void {
    this.moveForm.patchValue({ bay: null, tier: null });
    this.submitted.set(false);
    this.apiError.set(null);
  }

  protected positionText(position: { blockCode: string; bay: number | null; row: number | null; tier: number | null }): string {
    return formatPosition(position.blockCode, position.bay, position.row, position.tier);
  }

  protected blockSize(block: Block): string {
    return block.isVirtual
      ? 'Không quản lý bay/row/tier'
      : `${block.maxBays} bay × ${block.maxRows} row × ${block.maxTiers} tier`;
  }

  // Reads the position again (404 NotInYard when it left meanwhile) and opens it
  private openContainer(containerNumber: string): void {
    this.searchError.set(null);
    this.yard.position(containerNumber).subscribe({
      next: (position) => this.select(position),
      error: (err: unknown) => {
        this.current.set(null);
        this.searchError.set(toApiError(err).message);
      },
    });
  }

  // After a refused move: new position on screen, the error message stays visible
  private reloadCurrent(containerNumber: string): void {
    this.yard.position(containerNumber).subscribe({
      next: (position) => {
        this.current.set(position);
        this.moveForm.patchValue({ blockId: position.blockId, row: position.row ?? 1 });
        this.applyBlockRules();
        this.refresh.update((n) => n + 1);
        this.loadPage(this.page());
      },
      error: (err: unknown) => {
        this.current.set(null);
        this.searchError.set(toApiError(err).message);
      },
    });
  }

  private currentFilter(): YardSearchFilter {
    const f = this.filters.getRawValue();
    return {
      search: f.search,
      blockId: f.blockId || null,
      category: f.category || null,
      size: f.size || null,
      operatorId: f.operatorId || null,
      repair: f.repair || null,
      minDwellDays: f.minDwellDays,
      maxDwellDays: f.maxDwellDays,
    };
  }

  private loadPage(page: number): void {
    this.yard.search(page, PAGE_SIZE, this.currentFilter()).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.listError.set(null);
      },
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });
  }

  // A container found: show its block and row on the map, and start the move from there
  private select(position: ContainerPosition): void {
    this.current.set(position);
    this.moveForm.patchValue({ blockId: position.blockId, row: position.row ?? 1 });
    this.applyBlockRules();
    this.clearTarget();
  }

  private applyBlockRules(): void {
    // Read the control directly: the form-level value signal updates after this control's event
    const block = this.blocks().find((b) => b.id === this.moveForm.controls.blockId.value);
    const position = [this.moveForm.controls.bay, this.moveForm.controls.row, this.moveForm.controls.tier];
    if (block?.isVirtual) {
      position.forEach((control) => {
        control.setValue(null);
        control.disable({ emitEvent: false });
      });
    } else {
      position.forEach((control) => control.enable({ emitEvent: false }));
      if (this.moveForm.controls.row.value === null) this.moveForm.controls.row.setValue(1);
    }
  }

  private loadBlocks(): void {
    this.masterData.blocks().subscribe({
      next: (blocks) => {
        this.blocks.set(blocks);
        // Start on the first physical block so the map is visible right away
        if (!this.moveForm.controls.blockId.value) {
          const first = blocks.find((b) => !b.isVirtual) ?? blocks[0];
          if (first) this.moveForm.controls.blockId.setValue(first.id);
        }
      },
      error: (err: unknown) => this.loadError.set(toApiError(err).message),
    });
  }
}
