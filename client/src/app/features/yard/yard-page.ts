import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import { checkContainerNumber, containerNumberError, normalizeContainerNumber } from '../../core/depot/container-number';
import {
  Block,
  CONDITION_LABEL,
  ContainerPosition,
  MoveRequest,
  SIZE_LABEL,
  YardContainer,
  dwellDays,
  formatPosition,
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
import { ToastService } from '../../shared/toast/toast-service';
import { YardMap } from '../../shared/yard-map/yard-map';

/*
  Spec section 3: "current position in the yard" and re-stacking (đảo chuyển).
  One screen, three parts (mockup 03):
    - block cards: what is in each block, and which block the map shows
    - lookup: find a container by number (or click it on the map / in a virtual block list)
    - move: target block + Bay/Row/Tier, with the same checks as the API shown before submitting
  The API has the final say (POST /api/container-visits/move); the local checks only avoid
  sending requests that are sure to fail and explain why in Vietnamese.
*/
@Component({
  selector: 'app-yard-page',
  imports: [ReactiveFormsModule, DatePipe, YardMap],
  templateUrl: './yard-page.html',
  styleUrl: './yard-page.css',
})
export class YardPage {
  private readonly masterData = inject(MasterDataApi);
  private readonly yard = inject(YardApi);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly conditionLabel = CONDITION_LABEL;
  protected readonly formatPosition = formatPosition;

  // Lookup by container number (what yard staff read on the box)
  protected readonly searchForm = this.fb.group({ containerNumber: ['', Validators.required] });

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
  protected readonly searching = signal(false);
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

  protected search(): void {
    const number = normalizeContainerNumber(this.searchForm.controls.containerNumber.value);
    this.searchForm.controls.containerNumber.setValue(number);

    // Check the format locally first so a typo does not cost a request
    const check = checkContainerNumber(number);
    if (!check.valid) {
      this.searchError.set(containerNumberError(check));
      return;
    }

    this.searching.set(true);
    this.searchError.set(null);
    this.yard.position(number).subscribe({
      next: (position) => {
        this.select(position);
        this.searching.set(false);
      },
      error: (err: unknown) => {
        // 404 Container.NotFound / ContainerVisit.NotInYard come with their Vietnamese text
        this.current.set(null);
        this.searchError.set(toApiError(err).message);
        this.searching.set(false);
      },
    });
  }

  // Clicking a container on the map or in a virtual block list = looking it up by number
  protected pickContainer(container: YardContainer): void {
    this.searchForm.controls.containerNumber.setValue(container.containerNumber);
    this.search();
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
        this.submitting.set(false);
      },
      error: (err: unknown) => {
        this.apiError.set(toApiError(err));
        this.submitting.set(false);
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
