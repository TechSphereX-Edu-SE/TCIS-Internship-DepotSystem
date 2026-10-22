import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, catchError, concat, debounceTime, distinctUntilChanged, map, of, startWith, switchMap, throwError } from 'rxjs';
import { ApiError, fieldError, toApiError } from '../../core/api/api-error';
import { checkContainerNumber, normalizeContainerNumber } from '../../core/depot/container-number';
import {
  Block,
  CATEGORY_LABEL,
  CONDITION_LABEL,
  Container,
  ContainerCondition,
  ContainerGrade,
  ContainerPosition,
  DailyGateIn,
  GateInRequest,
  LineOperator,
  SIZE_LABEL,
  YardContainer,
  formatPosition,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { YardApi } from '../../core/depot/yard-api';
import { bayForClick, findOverlap, isSupported } from '../../core/depot/yard-rules';
import { ToastService } from '../../shared/toast/toast-service';
import { YardMap } from '../../shared/yard-map/yard-map';

// Result of checking the typed container number (locally, then against the API)
type Lookup =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'invalid'; message: string }
  | { kind: 'notFound'; number: string }
  | { kind: 'inYard'; container: Container; position: ContainerPosition }
  | { kind: 'ready'; container: Container; checkDigit: string }
  | { kind: 'error'; message: string };

interface PositionErrors {
  bay?: string;
  row?: string;
  tier?: string;
}

@Component({
  selector: 'app-gate-in-page',
  imports: [ReactiveFormsModule, DatePipe, RouterLink, YardMap],
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
  protected readonly grades: ContainerGrade[] = ['A', 'B', 'C'];

  // Same fields as GateInCommand; Bay/Row/Tier are disabled for a virtual block
  protected readonly form = inject(FormBuilder).nonNullable.group({
    containerNumber: ['', Validators.required],
    lineOperatorId: ['', Validators.required],
    blockId: ['', Validators.required],
    bay: new FormControl<number | null>(null),
    row: new FormControl<number | null>(1),
    tier: new FormControl<number | null>(null),
    grade: ['A' as ContainerGrade],
    condition: ['Normal' as ContainerCondition],
    conditionNote: ['', Validators.maxLength(500)],
    vehicleNo: ['', [Validators.required, Validators.maxLength(20)]],
  });

  protected readonly operators = signal<LineOperator[]>([]);
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

  protected readonly selectedBlock = computed(() => this.blocks().find((b) => b.id === this.value().blockId) ?? null);
  protected readonly physicalBlocks = computed(() => this.blocks().filter((b) => !b.isVirtual));

  protected readonly container = computed(() => {
    const state = this.lookup();
    return state.kind === 'ready' || state.kind === 'inYard' ? state.container : null;
  });

  protected readonly ready = computed(() => {
    const state = this.lookup();
    return state.kind === 'ready' ? state : null;
  });

  // Message under the container number field (null when there is nothing to report)
  protected readonly numberError = computed(() => {
    const state = this.lookup();
    switch (state.kind) {
      case 'invalid':
      case 'error':
        return state.message;
      case 'inYard':
        return `Container đang ở trong bãi tại ${this.positionText(state.position)}.`;
      case 'idle':
        return this.submitted() ? 'Nhập đủ 11 ký tự số container.' : null;
      default:
        return null;
    }
  });

  // The API would reject these anyway, so the button stays disabled
  protected readonly blocked = computed(() => ['invalid', 'inYard', 'notFound'].includes(this.lookup().kind));

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

  // Same checks as Block.ValidateSlot (range, odd/even bay), shown next to the fields
  protected readonly positionErrors = computed<PositionErrors>(() => {
    const block = this.selectedBlock();
    if (!block || block.isVirtual) return {};

    const { bay, row, tier } = this.value();
    const size = this.container()?.size;
    const required = this.submitted() ? 'Bắt buộc' : undefined;
    const errors: PositionErrors = {};

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
  });

  // Overlap / support warning computed from the map data (same rules as YardSlotRules)
  protected readonly slotWarning = computed(() => {
    const { bay, tier } = this.value();
    const size = this.container()?.size;
    const errors = this.positionErrors();
    if (!size || bay === null || tier === null || errors.bay || errors.tier || errors.row) return null;

    const placed = this.mapContainers().map((c) => ({ bay: c.bay!, tier: c.tier!, size: c.size, number: c.containerNumber }));
    const overlap = findOverlap(bay, tier, size, placed);
    if (overlap) return `Vị trí này trùng với container ${overlap.number}.`;
    if (!isSupported(bay, tier, size, placed)) return `Không có container đỡ bên dưới ở tier ${tier - 1}.`;
    return null;
  });

  constructor() {
    this.loadMasterData();
    this.loadToday();

    // Check the number locally first, then look it up in the catalog and in the yard
    this.form.controls.containerNumber.valueChanges
      .pipe(
        map(normalizeContainerNumber),
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((number) => this.checkNumber(number)),
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
    if (state.kind !== 'ready' || !block || this.form.invalid || errors.bay || errors.row || errors.tier) {
      return;
    }

    const v = this.form.getRawValue();
    const request: GateInRequest = {
      containerId: state.container.id,
      lineOperatorId: v.lineOperatorId,
      blockId: v.blockId,
      bay: block.isVirtual ? null : v.bay,
      row: block.isVirtual ? null : v.row,
      tier: block.isVirtual ? null : v.tier,
      grade: v.grade,
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
        this.submitting.set(false);
      },
      error: (err: unknown) => {
        this.apiError.set(toApiError(err));
        this.submitting.set(false);
      },
    });
  }

  // "Làm lại": clear the container data but keep the block and row the user is working on
  protected resetForNext(): void {
    const { blockId, row } = this.form.getRawValue();
    this.form.reset({ blockId, row });
    this.applyBlockRules();
    this.submitted.set(false);
    this.apiError.set(null);
    this.lookup.set({ kind: 'idle' });
  }

  // Red border + message for a plain required/length rule or a matching API validation error
  protected invalid(name: 'lineOperatorId' | 'vehicleNo' | 'conditionNote'): boolean {
    const control = this.form.controls[name];
    return (control.invalid && (control.touched || this.submitted())) || !!fieldError(this.apiError(), name);
  }

  protected positionText(position: ContainerPosition): string {
    return formatPosition(position.blockCode, position.bay, position.row, position.tier);
  }

  private checkNumber(number: string): Observable<Lookup> {
    // Wait for the full 11 characters before judging the number
    if (number.length < 11) return of<Lookup>({ kind: 'idle' });

    const check = checkContainerNumber(number);
    if (!check.valid) {
      const message =
        check.reason === 'format'
          ? 'Số container gồm 3 chữ cái + U + 6 số + 1 ký tự kiểm tra (VD: MSCU1234566).'
          : `Sai check digit: với ${number.slice(0, 10)} ký tự cuối phải là ${check.expected}.`;
      return of<Lookup>({ kind: 'invalid', message });
    }

    const lookup$ = this.masterData.findContainer(number).pipe(
      switchMap((container): Observable<Lookup> => {
        if (!container) return of<Lookup>({ kind: 'notFound', number });
        // In the yard already -> position found; 404 NotInYard -> ready for gate-in
        return this.yard.position(number).pipe(
          map((position): Lookup => ({ kind: 'inYard', container, position })),
          catchError((err: unknown) =>
            toApiError(err).code === 'ContainerVisit.NotInYard'
              ? of<Lookup>({ kind: 'ready', container, checkDigit: check.checkDigit })
              : throwError(() => err),
          ),
        );
      }),
      catchError((err: unknown) => of<Lookup>({ kind: 'error', message: toApiError(err).message })),
    );

    return concat(of<Lookup>({ kind: 'checking' }), lookup$);
  }

  private applyLookup(state: Lookup): void {
    this.lookup.set(state);
    if (state.kind !== 'ready') return;

    // Suggest the operator whose code starts the owner name (MSC -> MSC, "CMA CGM" -> CMA); the user can change it
    if (!this.form.controls.lineOperatorId.value) {
      const owner = state.container.ownerName.trim().split(/\s+/)[0]?.toUpperCase();
      const operator = this.operators().find((o) => o.code.toUpperCase() === owner);
      if (operator) this.form.controls.lineOperatorId.setValue(operator.id);
    }

    // Start from the condition recorded on the container; the gate check may change it
    this.form.controls.condition.setValue(state.container.condition);
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

  private loadMasterData(): void {
    this.masterData.lineOperators().subscribe({
      next: (operators) => this.operators.set(operators),
      error: (err: unknown) => this.loadError.set(toApiError(err).message),
    });
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
