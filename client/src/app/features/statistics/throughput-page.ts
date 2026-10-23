import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { map, startWith } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import {
  LineOperator,
  THROUGHPUT_MAX_DAYS,
  Throughput,
  addDays,
  dailyTotals,
  daysInRange,
  formatDate,
  vietnamToday,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { StatisticsApi } from '../../core/depot/statistics-api';

// Tallest bar in px; the other bars are scaled to the busiest day
const BAR_MAX_HEIGHT = 200;

// The page opens on the last 7 days (today included), enough to see a week of work at a glance
const DEFAULT_DAYS = 7;

/*
  Plan phase 4: gate-in / gate-out volume per day and line operator (mockup 07).
  The chart shows the daily totals of the range (all operators, or the one picked),
  the table below lists the API rows (date x line operator) with the exact numbers.
  Dates are Vietnam dates, the same way the API groups them.
*/
@Component({
  selector: 'app-throughput-page',
  imports: [ReactiveFormsModule],
  templateUrl: './throughput-page.html',
  styleUrl: './throughput-page.css',
})
export class ThroughputPage {
  private readonly api = inject(StatisticsApi);
  private readonly masterData = inject(MasterDataApi);

  protected readonly formatDate = formatDate;
  protected readonly maxDays = THROUGHPUT_MAX_DAYS;

  private readonly today = vietnamToday();

  // Same parameters as GET /api/statistics/throughput; '' = every line operator
  protected readonly form = inject(FormBuilder).nonNullable.group({
    from: [addDays(this.today, -(DEFAULT_DAYS - 1)), Validators.required],
    to: [this.today, Validators.required],
    lineOperatorId: [''],
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

    const { from, to, lineOperatorId } = this.form.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.api.throughput(from, to, lineOperatorId || undefined).subscribe({
      next: (data) => {
        this.result.set(data);
        this.shownOperator.set(this.operators().find((o) => o.id === lineOperatorId)?.code ?? null);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toApiError(err).message);
        this.loading.set(false);
      },
    });
  }
}
