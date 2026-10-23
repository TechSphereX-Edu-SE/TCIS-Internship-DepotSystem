import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { toApiError } from '../../core/api/api-error';
import {
  Block,
  CATEGORIES,
  CATEGORY_LABEL,
  CONDITION_LABEL,
  ContainerCategory,
  ContainerSize,
  LineOperator,
  REPAIR_MARKER_LABEL,
  SIZES,
  SIZE_LABEL,
  YARD_REPAIR_STATES,
  YARD_REPAIR_STATE_LABEL,
  YardContainer,
  YardInventory,
  YardInventoryFilter,
  YardRepairState,
  YardSort,
  dwellDays,
  formatDate,
  formatPosition,
  historyLinkParams,
  repairMarker,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { StatisticsApi } from '../../core/depot/statistics-api';
import { YardApi } from '../../core/depot/yard-api';
import { LIST_PAGE_SIZE, Pager } from '../../shared/pager/pager';
import { PagedDetails } from '../../shared/paged-details/paged-details';

// Same default and range as GetYardInventoryQuery / its validator
const DEFAULT_LONG_STAY_DAYS = 10;
// Chip of a repair mark: amber = chờ giám định, red = cần sửa / đang xử lý (same as the yard page)
const MARKER_CHIP = { abnormal: 'chip-amber', repair: 'chip-red' } as const;

/*
  Plan phase 4 + feedback 7/8: containers in the yard per line operator, split by dwell time (mockup 06).
    - Filters above the table: line operator, type, size, dwell days; more: block, repair state, gate-in dates.
    - Each line operator opens (▸) its containers, loaded when opened and paged, longest stay first
      (or newest first). The totals and the opened rows use the same applied filters, so they always agree.
    - The physical stock includes virtual blocks; containers at an outside repair facility have left.
  Dwell days are Vietnam calendar days (gate-in day = 0), counted at the time shown in the header.
*/
@Component({
  selector: 'app-inventory-page',
  imports: [ReactiveFormsModule, DatePipe, RouterLink, Pager],
  templateUrl: './inventory-page.html',
  styleUrl: './inventory-page.css',
})
export class InventoryPage {
  private readonly api = inject(StatisticsApi);
  private readonly yard = inject(YardApi);
  private readonly masterData = inject(MasterDataApi);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly formatDate = formatDate;
  protected readonly formatPosition = formatPosition;
  protected readonly repairMarker = repairMarker;
  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly conditionLabel = CONDITION_LABEL;
  protected readonly markerLabel = REPAIR_MARKER_LABEL;
  protected readonly markerChip = MARKER_CHIP;
  protected readonly repairStateLabel = YARD_REPAIR_STATE_LABEL;
  protected readonly repairStates = YARD_REPAIR_STATES;
  protected readonly categories = CATEGORIES;
  protected readonly sizes = SIZES;

  // Whole days 1..365, same as GetYardInventoryValidator
  protected readonly longStayDays = new FormControl(DEFAULT_LONG_STAY_DAYS, {
    nonNullable: true,
    validators: [Validators.required, Validators.min(1), Validators.max(365), Validators.pattern(/^\d+$/)],
  });

  // Feedback 8: main filters always visible, the others under "Lọc thêm"
  protected readonly filterForm = this.fb.group({
    longStayDays: this.longStayDays,
    operatorId: [''],
    category: ['' as ContainerCategory | ''],
    size: ['' as ContainerSize | ''],
    minDwellDays: new FormControl<number | null>(null),
    maxDwellDays: new FormControl<number | null>(null),
    blockId: [''],
    repair: ['' as YardRepairState | ''],
    gateInFrom: [''],
    gateInTo: [''],
  });
  protected readonly moreFilters = signal(false);

  protected readonly operators = signal<LineOperator[]>([]);
  protected readonly blocks = signal<Block[]>([]);
  protected readonly inventory = signal<YardInventory | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  // Filters of the totals on screen: the opened rows load with these, never with half-edited fields
  private readonly applied = signal<YardInventoryFilter>({});
  // Longest stay first by default (feedback 7); the user can switch to newest first
  protected readonly sort = signal<YardSort>('Oldest');

  // Rows of each opened line operator (key = line operator id)
  protected readonly details = new PagedDetails<YardContainer>((operatorId, page) =>
    this.yard.search(page, LIST_PAGE_SIZE, { ...this.applied(), operatorId, sort: this.sort() }),
  );

  // "0–9 ngày" / "≥ 10 ngày": labels follow the split day the API actually used
  protected readonly shortLabel = computed(() => {
    const days = this.inventory()?.longStayDays ?? DEFAULT_LONG_STAY_DAYS;
    return days === 1 ? '0 ngày' : `0–${days - 1} ngày`;
  });
  protected readonly longLabel = computed(() => `≥ ${this.inventory()?.longStayDays ?? DEFAULT_LONG_STAY_DAYS} ngày`);

  // Bumped on every form change, so the computed checks below re-run (reactive forms are not signals)
  private readonly formVersion = signal(0);

  // Same rules as YardVisitFilterValidator, shown before calling the API
  protected readonly filterError = computed(() => {
    this.formVersion();
    const f = this.filterForm.getRawValue();
    if (f.minDwellDays !== null && f.maxDwellDays !== null && f.maxDwellDays < f.minDwellDays)
      return 'Số ngày lưu "đến" phải lớn hơn hoặc bằng "từ".';
    if (f.gateInFrom && f.gateInTo && f.gateInTo < f.gateInFrom) return 'Ngày nhập "đến" phải bằng hoặc sau "từ".';
    return null;
  });

  // Bars are scaled to the line operator with the most containers, so the biggest one fills the track
  protected readonly bars = computed(() => {
    const data = this.inventory();
    if (!data) return [];
    const max = Math.max(1, ...data.lineOperators.map((o) => o.total));
    return data.lineOperators.map((o) => ({
      ...o,
      shortWidth: (o.shortStay / max) * 100,
      longWidth: (o.longStay / max) * 100,
      label: `${o.code}: ${o.shortStay} tồn ngắn, ${o.longStay} tồn lâu`,
    }));
  });

  constructor() {
    this.filterForm.valueChanges.subscribe(() => this.formVersion.update((v) => v + 1));
    this.masterData.lineOperators('Operator').subscribe({
      next: (operators) => this.operators.set(operators),
      error: (err: unknown) => this.error.set(toApiError(err).message),
    });
    this.masterData.blocks().subscribe({
      next: (blocks) => this.blocks.set(blocks),
      error: (err: unknown) => this.error.set(toApiError(err).message),
    });
    this.load();
  }

  protected load(): void {
    this.longStayDays.markAsTouched();
    if (this.longStayDays.invalid || this.filterError()) return;

    const filter = this.currentFilter();
    this.loading.set(true);
    this.error.set(null);
    this.api.yardInventory(Number(this.longStayDays.value), filter).subscribe({
      next: (data) => {
        this.inventory.set(data);
        this.applied.set(filter);
        // Rows opened before belong to the old filters: reload them with the new ones
        this.details.reset();
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  protected clearFilters(): void {
    this.filterForm.reset();
    this.load();
  }

  protected changeSort(sort: YardSort): void {
    this.sort.set(sort);
    this.details.reset();
  }

  // Dwell days at the time the totals were counted, so rows and totals use the same moment
  protected dwellOf(item: YardContainer): number {
    const asOf = this.inventory()?.asOfTime;
    return dwellDays(item.gateInAt, asOf ? new Date(asOf) : new Date());
  }

  // History of one container, opened from its row
  protected historyParams(item: YardContainer): Record<string, string | number> {
    return historyLinkParams({ containerId: item.containerId, search: item.containerNumber });
  }

  // Open visits of a line operator in the history page, with the type and size filters carried over
  protected operatorHistoryParams(operatorId: string): Record<string, string | number> {
    const f = this.applied();
    return historyLinkParams({ operatorId, state: 'Open', category: f.category, size: f.size });
  }

  private currentFilter(): YardInventoryFilter {
    const f = this.filterForm.getRawValue();
    return {
      operatorId: f.operatorId || null,
      category: f.category || null,
      size: f.size || null,
      minDwellDays: f.minDwellDays,
      maxDwellDays: f.maxDwellDays,
      blockId: f.blockId || null,
      repair: f.repair || null,
      gateInFrom: f.gateInFrom || null,
      gateInTo: f.gateInTo || null,
    };
  }
}
