import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, catchError, concat, map, of, startWith, switchMap } from 'rxjs';
import { ApiError, fieldError, toApiError } from '../../core/api/api-error';
import {
  Block,
  CATEGORY_LABEL,
  CONDITION_LABEL,
  Container,
  ContainerCondition,
  ContainerPosition,
  DailyGateIn,
  GateInRequest,
  LineOperator,
  SIZE_LABEL,
  YardContainer,
  blockAccepts,
  categoriesText,
  formatPosition,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { YardApi } from '../../core/depot/yard-api';
import { SlotErrors, bayForClick, hasSlotErrors, placementWarning, toPlaced, validateSlot } from '../../core/depot/yard-rules';
import { SearchSelect, SearchPageLoader } from '../../shared/search-select/search-select';
import { ToastService } from '../../shared/toast/toast-service';
import { YardMap } from '../../shared/yard-map/yard-map';

// Rows asked per page by the search dropdowns (about 5 are visible, the rest load while scrolling)
const SEARCH_PAGE_SIZE = 20;

/*
  State of the selected container: only catalog containers can be picked (feedback 3),
  so the number is always valid; what is left to check is whether it is already in the yard,
  and (feedback 2) whether a confirmed operating assignment names its line operator for today.
*/
type Lookup =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'inYard'; container: Container; position: ContainerPosition }
  | { kind: 'noOperator'; container: Container }
  | { kind: 'ready'; container: Container }
  | { kind: 'error'; message: string };

@Component({
  selector: 'app-gate-in-page',
  imports: [ReactiveFormsModule, DatePipe, RouterLink, YardMap, SearchSelect],
  templateUrl: './gate-in-page.html',
  styleUrl: './gate-in-page.css',
})
export class GateInPage {
  private readonly masterData = inject(MasterDataApi);
  private readonly yard = inject(YardApi);
  private readonly toast = inject(ToastService);

  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly conditionLabel = CONDITION_LABEL;
  protected readonly formatPosition = formatPosition;
  protected readonly categoriesText = categoriesText;

  /*
    Same fields as GateInCommand; Bay/Row/Tier are disabled for a virtual block.
    The container holds the picked row (or null): the dropdown never keeps typed text.
    Feedback 2: the line operator is not typed here. It comes from the container's confirmed operating
    assignment for today; operatorFilter only narrows the container list to the containers it operates.
    The A/B/C grade is gone (feedback 3: it was only a placeholder label, not a quality rule).
  */
  protected readonly form = inject(FormBuilder).nonNullable.group({
    container: new FormControl<Container | null>(null, Validators.required),
    operatorFilter: new FormControl<LineOperator | null>(null),
    blockId: ['', Validators.required],
    bay: new FormControl<number | null>(null),
    row: new FormControl<number | null>(1),
    tier: new FormControl<number | null>(null),
    condition: ['Normal' as ContainerCondition],
    conditionNote: ['', Validators.maxLength(500)],
    vehicleNo: ['', [Validators.required, Validators.maxLength(20)]],
  });

  // Search loaders for the two dropdowns; the API ranks exact match, then prefix, then the rest.
  // With a line operator chosen, only containers it operates today are listed.
  protected readonly loadContainers: SearchPageLoader<Container> = (term, page) =>
    this.masterData.containerPage(term, page, SEARCH_PAGE_SIZE, this.form.controls.operatorFilter.value?.id);
  protected readonly loadOperators: SearchPageLoader<LineOperator> = (term, page) =>
    this.masterData.lineOperatorPage(page, SEARCH_PAGE_SIZE, term, 'Operator');

  protected readonly containerKey = (c: Container) => c.id;
  protected readonly containerLabel = (c: Container) => c.containerNumber;
  protected readonly containerDetail = (c: Container) =>
    `${c.isoCode} · ${SIZE_LABEL[c.size]} · ${CATEGORY_LABEL[c.category]} · KT ${c.currentOperatorCode ?? 'chưa có'}`;
  protected readonly ownerText = (c: Container) => c.ownerCode ?? `${c.ownerName} (chưa đối chiếu)`;
  protected readonly operatorKey = (o: LineOperator) => o.id;
  protected readonly operatorLabel = (o: LineOperator) => o.code;
  protected readonly operatorDetail = (o: LineOperator) => o.name;

  protected readonly blocks = signal<Block[]>([]);
  protected readonly lookup = signal<Lookup>({ kind: 'idle' });
  protected readonly mapContainers = signal<YardContainer[]>([]);
  protected readonly todayGateIns = signal<DailyGateIn[]>([]);
  protected readonly loadError = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  // Form values as a signal, so computed() below reacts to typing
  private readonly value = toSignal(
    this.form.valueChanges.pipe(
      startWith(null),
      map(() => this.form.getRawValue()),
    ),
    { initialValue: this.form.getRawValue() },
  );

  // Bumped after a gate-in so the map reloads even if block and row did not change
  private readonly refreshMap = signal(0);

  protected readonly container = computed(() => this.value().container);

  // A new filter drops the picked container and its results (the dropdown's resetKey)
  protected readonly operatorFilterId = computed(() => this.value().operatorFilter?.id ?? null);

  /*
    Feedback 3: the block dropdown only offers blocks whose list of types contains the container's
    type (one block can take several types). Before a container is picked every block is listed.
  */
  protected readonly blockOptions = computed(() => {
    const container = this.container();
    return container ? this.blocks().filter((b) => blockAccepts(b, container.category)) : this.blocks();
  });

  protected readonly selectedBlock = computed(
    () => this.blockOptions().find((b) => b.id === this.value().blockId) ?? null,
  );

  protected readonly ready = computed(() => {
    const state = this.lookup();
    return state.kind === 'ready' ? state : null;
  });

  // Message under the container field (null when there is nothing to report)
  protected readonly containerError = computed(() => {
    const state = this.lookup();
    switch (state.kind) {
      case 'error':
        return state.message;
      case 'inYard':
        return `Container đang ở trong bãi tại ${this.positionText(state.position)}.`;
      case 'noOperator':
        return 'Container chưa có quan hệ khai thác đã xác nhận cho hôm nay, nên chưa biết hãng khai thác. ' +
          'Ghi nhận và xác nhận ở Danh mục · Container trước khi nhập bãi.';
      case 'idle':
        return this.submitted() ? 'Chọn container trong danh sách.' : null;
      default:
        return null;
    }
  });

  // The API would reject these anyway, so the button stays disabled
  protected readonly blocked = computed(() => ['inYard', 'noOperator', 'checking'].includes(this.lookup().kind));

  // Row shown on the map = row typed in the form (when it is inside the block), otherwise row 1
  protected readonly mapRow = computed(() => {
    const block = this.selectedBlock();
    const row = this.value().row;
    return block?.maxRows && row && row >= 1 && row <= block.maxRows ? row : 1;
  });

  protected readonly rowOptions = computed(() =>
    Array.from({ length: this.selectedBlock()?.maxRows ?? 0 }, (_, i) => i + 1),
  );

  protected readonly mapSelection = computed(() => ({
    bay: this.value().bay,
    tier: this.value().tier,
    size: this.container()?.size ?? 'Ft20',
  }));

  // Same checks as Block.ValidateSlot (range, odd/even bay), shown next to the fields (shared with move)
  protected readonly positionErrors = computed<SlotErrors>(() => {
    const block = this.selectedBlock();
    return block ? validateSlot(block, this.value(), this.container()?.size, this.submitted()) : {};
  });

  // Overlap / support warning computed from the map data (same rules as YardSlotRules)
  protected readonly slotWarning = computed(() => {
    const { bay, tier } = this.value();
    const size = this.container()?.size;
    if (!size || bay === null || tier === null || hasSlotErrors(this.positionErrors())) return null;
    return placementWarning(bay, tier, size, toPlaced(this.mapContainers()));
  });

  constructor() {
    this.loadBlocks();
    this.loadToday();

    /*
      A picked container is checked against the yard (open visit -> blocked, with its position).
      Feedback 3: changing the container re-checks the block, because the new type may not fit it.
    */
    this.form.controls.container.valueChanges
      .pipe(
        switchMap((container) => {
          this.dropBlockIfNotAccepted(container);
          return container ? this.checkInYard(container) : of<Lookup>({ kind: 'idle' });
        }),
        takeUntilDestroyed(),
      )
      .subscribe((state) => this.applyLookup(state));

    // Virtual block: no Bay/Row/Tier. Physical block: fields enabled again
    this.form.controls.blockId.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => this.applyBlockRules());

    // The map shows the selected block and row; reload when either changes or after a gate-in
    toObservable(computed(() => ({ block: this.selectedBlock(), row: this.mapRow(), tick: this.refreshMap() })))
      .pipe(
        switchMap(({ block, row }) =>
          block && !block.isVirtual ? this.yard.inYard(block.id, row).pipe(catchError(() => of([]))) : of([]),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((containers) => this.mapContainers.set(containers));
  }

  protected pickOnMap(cell: { oddBay: number; tier: number }): void {
    const block = this.selectedBlock();
    if (!block?.maxBays) return;
    const size = this.container()?.size ?? 'Ft20';
    this.form.patchValue({ bay: bayForClick(cell.oddBay, size, block.maxBays), tier: cell.tier, row: this.mapRow() });
  }

  protected selectMapRow(row: string): void {
    this.form.controls.row.setValue(Number(row));
  }

  protected submit(): void {
    this.submitted.set(true);
    this.apiError.set(null);
    this.form.markAllAsTouched();

    const state = this.lookup();
    const block = this.selectedBlock();
    const errors = this.positionErrors();
    const v = this.form.getRawValue();
    if (state.kind !== 'ready' || !state.container.currentOperatorId || !block || this.form.invalid || hasSlotErrors(errors)) {
      return;
    }

    const request: GateInRequest = {
      containerId: state.container.id,
      // The operator of today's confirmed assignment; the API checks it again
      lineOperatorId: state.container.currentOperatorId,
      blockId: block.id,
      bay: block.isVirtual ? null : v.bay,
      row: block.isVirtual ? null : v.row,
      tier: block.isVirtual ? null : v.tier,
      condition: v.condition,
      // The API only keeps a note for a damaged container
      conditionNote: v.condition === 'Damaged' ? v.conditionNote.trim() || null : null,
      vehicleNo: v.vehicleNo.trim(),
    };

    this.submitting.set(true);
    this.yard.gateIn(request).subscribe({
      next: (result) => {
        this.toast.success(
          `Đã nhập ${result.containerNumber} vào ${formatPosition(result.blockCode, result.bay, result.row, result.tier)}`,
        );
        this.resetForNext();
        this.refreshMap.update((n) => n + 1);
        this.loadToday();
        this.loadBlocks();
        this.submitting.set(false);
      },
      error: (err: unknown) => {
        this.apiError.set(toApiError(err));
        this.submitting.set(false);
      },
    });
  }

  // "Làm lại": clear the container data but keep the block, row and operator filter the user is working on
  protected resetForNext(): void {
    const { blockId, row, operatorFilter } = this.form.getRawValue();
    this.form.reset({ blockId, row, operatorFilter });
    this.applyBlockRules();
    this.submitted.set(false);
    this.apiError.set(null);
    this.lookup.set({ kind: 'idle' });
  }

  // Red border + message for a plain required/length rule or a matching API validation error
  protected invalid(name: 'blockId' | 'vehicleNo' | 'conditionNote'): boolean {
    const control = this.form.controls[name];
    return (control.invalid && (control.touched || this.submitted())) || !!fieldError(this.apiError(), name);
  }

  protected positionText(position: ContainerPosition): string {
    return formatPosition(position.blockCode, position.bay, position.row, position.tier);
  }

  // In the yard already -> position found; 404 NotInYard -> ready, unless no operator is confirmed for today
  private checkInYard(container: Container): Observable<Lookup> {
    const lookup$ = this.yard.position(container.containerNumber).pipe(
      map((position): Lookup => ({ kind: 'inYard', container, position })),
      catchError((err: unknown) => {
        const error = toApiError(err);
        if (error.code !== 'ContainerVisit.NotInYard') return of<Lookup>({ kind: 'error', message: error.message });
        return of<Lookup>(container.currentOperatorId ? { kind: 'ready', container } : { kind: 'noOperator', container });
      }),
    );
    return concat(of<Lookup>({ kind: 'checking' }), lookup$);
  }

  private applyLookup(state: Lookup): void {
    this.lookup.set(state);
    // Start from the condition recorded on the container; the gate check may change it.
    // The line operator is NOT guessed from the owner, the number prefix or an earlier visit (feedback 2).
    if (state.kind === 'ready') this.form.controls.condition.setValue(state.container.condition);
  }

  // Feedback 1: a filter change drops a choice that no longer fits (the user picks a block again)
  private dropBlockIfNotAccepted(container: Container | null): void {
    const block = this.blocks().find((b) => b.id === this.form.controls.blockId.value);
    if (container && block && !blockAccepts(block, container.category)) {
      this.form.controls.blockId.setValue('');
    }
  }

  private applyBlockRules(): void {
    // Read the control directly: the form-level value signal updates after this control's event
    const block = this.blocks().find((b) => b.id === this.form.controls.blockId.value);
    const position = [this.form.controls.bay, this.form.controls.row, this.form.controls.tier];
    if (block?.isVirtual) {
      position.forEach((c) => {
        c.setValue(null);
        c.disable({ emitEvent: false });
      });
    } else {
      position.forEach((c) => c.enable({ emitEvent: false }));
      if (this.form.controls.row.value === null) this.form.controls.row.setValue(1);
    }
  }

  // Also reloaded after a gate-in, so the container count of a virtual block stays current
  private loadBlocks(): void {
    this.masterData.blocks().subscribe({
      next: (blocks) => {
        this.blocks.set(blocks);
        // Start on the first physical block so the map is visible right away
        if (!this.form.controls.blockId.value) {
          const first = blocks.find((b) => !b.isVirtual) ?? blocks[0];
          if (first) this.form.controls.blockId.setValue(first.id);
        }
      },
      error: (err: unknown) => this.loadError.set(toApiError(err).message),
    });
  }

  private loadToday(): void {
    this.yard.daily().subscribe({
      next: (daily) => this.todayGateIns.set(daily.gateIns),
      error: () => this.todayGateIns.set([]),
    });
  }
}
