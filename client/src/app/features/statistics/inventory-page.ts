import { Component, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { toApiError } from '../../core/api/api-error';
import { YardInventory, formatDate } from '../../core/depot/depot-models';
import { StatisticsApi } from '../../core/depot/statistics-api';

// Same default and range as GetYardInventoryQuery / its validator
const DEFAULT_LONG_STAY_DAYS = 10;

/*
  Plan phase 4: containers in the yard per line operator, split by dwell time (mockup 06).
  The split day is a parameter (the plan leaves the exact ranges to confirm with the mentor),
  so the page lets the user change it; the column titles follow the chosen value.
*/
@Component({
  selector: 'app-inventory-page',
  imports: [ReactiveFormsModule],
  templateUrl: './inventory-page.html',
  styleUrl: './inventory-page.css',
})
export class InventoryPage {
  private readonly api = inject(StatisticsApi);

  protected readonly formatDate = formatDate;

  // Whole days 1..365, same as GetYardInventoryValidator
  protected readonly longStayDays = new FormControl(DEFAULT_LONG_STAY_DAYS, {
    nonNullable: true,
    validators: [Validators.required, Validators.min(1), Validators.max(365), Validators.pattern(/^\d+$/)],
  });

  // A form group so Enter in the field submits like the "Xem" button (ngSubmit)
  protected readonly filterForm = new FormGroup({ longStayDays: this.longStayDays });

  protected readonly inventory = signal<YardInventory | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  // "0–9 ngày" / "≥ 10 ngày": labels follow the split day the API actually used
  protected readonly shortLabel = computed(() => {
    const days = this.inventory()?.longStayDays ?? DEFAULT_LONG_STAY_DAYS;
    return days === 1 ? '0 ngày' : `0–${days - 1} ngày`;
  });
  protected readonly longLabel = computed(() => `≥ ${this.inventory()?.longStayDays ?? DEFAULT_LONG_STAY_DAYS} ngày`);

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
    this.load();
  }

  protected load(): void {
    this.longStayDays.markAsTouched();
    if (this.longStayDays.invalid) return;

    this.loading.set(true);
    this.error.set(null);
    this.api.yardInventory(Number(this.longStayDays.value)).subscribe({
      next: (data) => {
        this.inventory.set(data);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toApiError(err).message);
        this.loading.set(false);
      },
    });
  }
}
