import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { map, startWith } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import {
  CATEGORIES,
  CATEGORY_LABEL,
  ContainerCategory,
  ContainerSize,
  GATE_OUT_PURPOSES,
  GATE_OUT_PURPOSE_LABEL,
  GateOutPurpose,
  LineOperator,
  SIZES,
  SIZE_LABEL,
  THROUGHPUT_MAX_DAYS,
  Throughput,
  ThroughputEventKind,
  ThroughputFilter,
  VisitHistoryRow,
  addDays,
  dailyTotals,
  daysInRange,
  exitText,
  formatDate,
  historyLinkParams,
  vietnamToday,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { StatisticsApi } from '../../core/depot/statistics-api';
import { LIST_PAGE_SIZE, Pager } from '../../shared/pager/pager';
import { PagedDetails } from '../../shared/paged-details/paged-details';

// Tallest bar in px; the other bars are scaled to the busiest day
const BAR_MAX_HEIGHT = 200;

// The page opens on the last 7 days (today included), enough to see a week of work at a glance
const DEFAULT_DAYS = 7;
// Dates and filters of the totals on screen; the detail tabs load with exactly these
interface AppliedThroughput {
  from: string;
  to: string;
  filter: ThroughputFilter;
}

// Key of one detail table: "<line operator id>|GateIn" or "<line operator id>|GateOut"
function detailKey(operatorId: string, kind: ThroughputEventKind): string {
  return `${operatorId}|${kind}`;
}

/*
  Plan phase 4 + feedback 7/8: gate-in / gate-out volume per day and line operator (mockup 07).
    - Filters: from / to (both included), line operator, type, size; more: exit purpose.
    - Totals split gate-outs into cấp rỗng and chuyển sửa ngoài: not every gate-out is a release.
    - Each line operator opens (▸) two tabs, Lượt nhập / Lượt xuất, loaded when opened and paged,
      newest event first, with the same applied filters as the totals (so their counts match).
    - A gate-in is counted by its gate-in time, a gate-out by its gate-out time.
  The chart and the date x line operator table stay as before. Dates are Vietnam dates.
*/
@Component({
  selector: 'app-throughput-page',
  imports: [ReactiveFormsModule, DatePipe, RouterLink, Pager],
  templateUrl: './throughput-page.html',
  styleUrl: './throughput-page.css',
})
export class ThroughputPage {
  private readonly api = inject(StatisticsApi);
  private readonly masterData = inject(MasterDataApi);

  protected readonly formatDate = formatDate;
  protected readonly exitText = exitText;
  protected readonly maxDays = THROUGHPUT_MAX_DAYS;
  protected readonly categories = CATEGORIES;
  protected readonly sizes = SIZES;
  protected readonly purposes = GATE_OUT_PURPOSES;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly purposeLabel = GATE_OUT_PURPOSE_LABEL;
  protected readonly detailKey = detailKey;

  private readonly today = vietnamToday();

  // Same parameters as GET /api/statistics/throughput; '' = no filter
  protected readonly form = inject(FormBuilder).nonNullable.group({
    from: [addDays(this.today, -(DEFAULT_DAYS - 1)), Validators.required],
    to: [this.today, Validators.required],
    lineOperatorId: [''],
    category: ['' as ContainerCategory | ''],
    size: ['' as ContainerSize | ''],
    purpose: ['' as GateOutPurpose | ''],
  });
  protected readonly moreFilters = signal(false);
  private readonly applied = signal<AppliedThroughput | null>(null);

  // Tab shown under each opened line operator (default Lượt nhập)
  private readonly tabs = signal<Record<string, ThroughputEventKind>>({});

  // Rows of each opened line operator and tab, with the applied dates and filters
  protected readonly details = new PagedDetails<VisitHistoryRow>((key, page) => {
    const [lineOperatorId, kind] = key.split('|') as [string, ThroughputEventKind];
    const { from, to, filter } = this.applied()!;
    return this.api.throughputEvents(kind, from, to, { ...filter, lineOperatorId }, page, LIST_PAGE_SIZE);
  });

  protected readonly operators = signal<LineOperator[]>([]);
  protected readonly result = signal<Throughput | null>(null);
  // Line operator code of the result shown (null = all), for the chart title
  protected readonly shownOperator = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  private readonly value = toSignal(
    this.form.valueChanges.pipe(
      startWith(null),
      map(() => this.form.getRawValue()),
    ),
    { initialValue: this.form.getRawValue() },
  );

  // Same rules as GetThroughputValidator, shown before calling the API
  protected readonly rangeError = computed(() => {
    const { from, to } = this.value();
    if (!from || !to) return 'Chọn đủ từ ngày và đến ngày.';
    if (to < from) return 'Đến ngày phải bằng hoặc sau từ ngày.';
    if (daysInRange(from, to) > THROUGHPUT_MAX_DAYS) return `Khoảng thời gian tối đa ${THROUGHPUT_MAX_DAYS} ngày.`;
    return null;
  });

  // One column per date of the range; bars scaled to the busiest value so the tallest one is BAR_MAX_HEIGHT
  protected readonly chart = computed(() => {
    const data = this.result();
    if (!data) return [];
    const totals = dailyTotals(data.from, data.to, data.days);
    const max = Math.max(1, ...totals.map((t) => Math.max(t.gateIn, t.gateOut)));
    // A 0 keeps a 2px stub so the day is still visible on the axis
    const height = (value: number) => Math.max(2, Math.round((value / max) * BAR_MAX_HEIGHT));
    return totals.map((t) => ({
      ...t,
      label: formatDate(t.date).slice(0, 5),
      inHeight: height(t.gateIn),
      outHeight: height(t.gateOut),
      aria: `${formatDate(t.date)}: ${t.gateIn} vào, ${t.gateOut} ra`,
    }));
  });

  constructor() {
    // Only partners with the line operator role issue orders / appear in the throughput filter
    this.masterData.lineOperators('Operator').subscribe({
      next: (operators) => this.operators.set(operators),
      error: (err: unknown) => this.error.set(toApiError(err).message),
    });
    this.load();
  }

  protected load(): void {
    if (this.rangeError()) return;

    const { from, to, lineOperatorId, category, size, purpose } = this.form.getRawValue();
    const filter: ThroughputFilter = {
      lineOperatorId: lineOperatorId || null,
      category: category || null,
      size: size || null,
      purpose: purpose || null,
    };
    this.loading.set(true);
    this.error.set(null);
    this.api.throughput(from, to, filter).subscribe({
      next: (data) => {
        this.result.set(data);
        this.applied.set({ from, to, filter });
        // Rows opened before belong to the old filters: reload them with the new ones
        this.details.reset();
        this.shownOperator.set(this.operators().find((o) => o.id === lineOperatorId)?.code ?? null);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  protected clearFilters(): void {
    this.form.reset();
    this.load();
  }

  protected tabOf(operatorId: string): ThroughputEventKind {
    return this.tabs()[operatorId] ?? 'GateIn';
  }

  // ▸ opens the operator on its current tab; ▾ closes both tabs
  protected isOpen(operatorId: string): boolean {
    return this.details.isOpen(detailKey(operatorId, this.tabOf(operatorId)));
  }

  protected toggle(operatorId: string): void {
    if (this.isOpen(operatorId)) {
      for (const kind of ['GateIn', 'GateOut'] as const) {
        if (this.details.isOpen(detailKey(operatorId, kind))) this.details.toggle(detailKey(operatorId, kind));
      }
      return;
    }
    this.details.open(detailKey(operatorId, this.tabOf(operatorId)));
  }

  protected showTab(operatorId: string, kind: ThroughputEventKind): void {
    this.tabs.update((tabs) => ({ ...tabs, [operatorId]: kind }));
    this.details.open(detailKey(operatorId, kind));
  }

  // Same rows in the history page: the period applies to the event of the tab, filters carried over
  protected historyParams(operatorId: string, kind: ThroughputEventKind): Record<string, string | number> {
    const applied = this.applied();
    if (!applied) return {};
    return historyLinkParams({
      operatorId,
      from: applied.from,
      to: applied.to,
      event: kind,
      category: applied.filter.category,
      size: applied.filter.size,
      purpose: kind === 'GateOut' ? applied.filter.purpose : null,
    });
  }
}
