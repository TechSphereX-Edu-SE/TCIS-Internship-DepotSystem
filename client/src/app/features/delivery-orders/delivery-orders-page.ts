import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { map, startWith } from 'rxjs';
import { ApiError, fieldError, toApiError } from '../../core/api/api-error';
import { DeliveryOrderApi } from '../../core/depot/delivery-order-api';
import {
  CATEGORIES,
  CATEGORY_HINT,
  CATEGORY_LABEL,
  ContainerCategory,
  ContainerSize,
  CreateDeliveryOrderRequest,
  Customer,
  DELIVERY_ORDER_STATUSES,
  DELIVERY_ORDER_STATUS_LABEL,
  DeliveryOrder,
  DeliveryOrderStatus,
  LineOperator,
  SIZE_LABEL,
  canCancelOrder,
  formatDate,
  formatDateTime,
  releasedSummary,
  vietnamToday,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { LIST_PAGE_SIZE, Pager } from '../../shared/pager/pager';
import { ToastService } from '../../shared/toast/toast-service';


// Chip colour per status (mockup 05 + feedback 6): usable ones green / amber, finished indigo, the rest grey / red
const STATUS_CHIP: Record<DeliveryOrderStatus, string> = {
  NotStarted: 'chip-green',
  InProgress: 'chip-amber',
  Completed: 'chip-indigo',
  Expired: 'chip-grey',
  Cancelled: 'chip-red',
};

const SIZES: ContainerSize[] = ['Ft20', 'Ft40'];

// "Dry|Ft20": one line per type and size (Q1)
const lineKey = (category: ContainerCategory, size: ContainerSize) => `${category}|${size}`;

/*
  Spec section 3: delivery orders (lệnh giao container rỗng) of the line operators.
  Mockup 05: the list of orders with released / ordered per type and a status, then the create form.
  The form has the fields of CreateDeliveryOrderCommand; the same rules as the validator are checked
  here so the user sees them next to the fields, the API still checks everything.
  Feedback 6 / Q1 / Q7: lines by type + size, status computed by the API (Chưa giao, Đang giao, Đã giao đủ,
  Hết hạn, Đã huỷ), filters, and cancel with a reason while nothing has been released.
*/
@Component({
  selector: 'app-delivery-orders-page',
  imports: [ReactiveFormsModule, RouterLink, Pager],
  templateUrl: './delivery-orders-page.html',
  styleUrl: './delivery-orders-page.css',
})
export class DeliveryOrdersPage {
  private readonly api = inject(DeliveryOrderApi);
  private readonly masterData = inject(MasterDataApi);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly categoryHint = CATEGORY_HINT;
  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly sizes = SIZES;
  protected readonly categories = CATEGORIES;
  protected readonly statuses = DELIVERY_ORDER_STATUSES;
  protected readonly statusLabel = DELIVERY_ORDER_STATUS_LABEL;
  protected readonly statusChip = STATUS_CHIP;
  protected readonly releasedSummary = releasedSummary;
  protected readonly canCancel = canCancelOrder;
  protected readonly formatDate = formatDate;
  protected readonly formatDateTime = formatDateTime;

  // List filters: they apply on "Lọc" (and on Enter in the search box), not on every key press
  protected readonly filterForm = this.fb.group({
    search: [''],
    status: ['' as DeliveryOrderStatus | ''],
    operatorId: [''],
  });
  protected readonly cancellingId = signal<string | null>(null);

  // An expiry date before today (Vietnam) is rejected by the API (DeliveryOrder.AlreadyExpired)
  protected readonly today = vietnamToday();

  protected readonly form = this.fb.group({
    // Same pattern and length as CreateDeliveryOrderValidator
    orderNumber: ['', [Validators.required, Validators.maxLength(30), Validators.pattern(/^[A-Za-z0-9\-/.]+$/)]],
    lineOperatorId: ['', Validators.required],
    customerId: ['', Validators.required],
    expiryDate: [this.today, Validators.required],
    vesselVoyage: ['', [Validators.required, Validators.maxLength(50)]],
    lines: this.fb.array([this.newLine('Dry', 'Ft20')]),
  });

  protected readonly orders = signal<DeliveryOrder[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly listError = signal<string | null>(null);
  protected readonly operators = signal<LineOperator[]>([]);
  protected readonly customers = signal<Customer[]>([]);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  // Form values as a signal, so the type options below follow what is picked in each line
  private readonly value = toSignal(
    this.form.valueChanges.pipe(
      startWith(null),
      map(() => this.form.getRawValue()),
    ),
    { initialValue: this.form.getRawValue() },
  );

  // Indexes of lines whose type + size is already used by an earlier line (DeliveryOrder.DuplicateLine)
  protected readonly duplicateLines = computed(() => {
    const seen = new Set<string>();
    const duplicates = new Set<number>();
    this.value().lines.forEach((l, i) => {
      const key = lineKey(l.category, l.size);
      if (seen.has(key)) duplicates.add(i);
      seen.add(key);
    });
    return duplicates;
  });

  protected readonly canAddLine = computed(() => this.value().lines.length < CATEGORIES.length * SIZES.length);

  protected readonly expiryInPast = computed(() => {
    const expiry = this.value().expiryDate;
    // ISO dates compare correctly as strings
    return !!expiry && expiry < this.today;
  });

  constructor() {
    this.loadPage(1);
    // Only partners with the line operator role issue orders / appear in the throughput filter
    this.masterData.lineOperators('Operator').subscribe({
      next: (operators) => this.operators.set(operators),
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });
    this.masterData.customers().subscribe({
      next: (customers) => this.customers.set(customers),
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });
  }

  protected addLine(): void {
    // Start the new line on the first type + size not used yet (20ft before 40ft)
    const picked = new Set(this.form.getRawValue().lines.map((l) => lineKey(l.category, l.size)));
    for (const category of CATEGORIES) {
      const size = SIZES.find((s) => !picked.has(lineKey(category, s)));
      if (size) {
        this.form.controls.lines.push(this.newLine(category, size));
        return;
      }
    }
  }

  protected removeLine(index: number): void {
    // An order needs at least one line (validator: Lines not empty)
    if (this.form.controls.lines.length > 1) this.form.controls.lines.removeAt(index);
  }

  protected goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) this.loadPage(page);
  }

  protected applyFilters(): void {
    this.loadPage(1);
  }

  protected clearFilters(): void {
    this.filterForm.reset();
    this.loadPage(1);
  }

  /*
    Q7: cancel needs a reason and is refused once a container left under the order (409 HasReleases).
    prompt() keeps it to one step, as for "Báo hư" on the container catalog.
  */
  protected cancel(order: DeliveryOrder): void {
    const reason = prompt(`Lý do huỷ lệnh ${order.orderNumber}:`)?.trim();
    if (!reason) return;

    this.cancellingId.set(order.id);
    this.api.cancel(order.id, reason).subscribe({
      next: () => {
        this.toast.success(`Đã huỷ lệnh ${order.orderNumber}`);
        this.cancellingId.set(null);
        this.loadPage(this.page());
      },
      error: (err: unknown) => {
        this.toast.error(toApiError(err).message);
        this.cancellingId.set(null);
        // The order may have changed meanwhile (e.g. a release): show its current state
        this.loadPage(this.page());
      },
    });
  }

  protected submit(): void {
    this.submitted.set(true);
    this.apiError.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid || this.expiryInPast() || this.duplicateLines().size > 0) return;

    const v = this.form.getRawValue();
    const request: CreateDeliveryOrderRequest = {
      orderNumber: v.orderNumber.trim(),
      lineOperatorId: v.lineOperatorId,
      customerId: v.customerId,
      expiryDate: v.expiryDate,
      vesselVoyage: v.vesselVoyage.trim(),
      lines: v.lines.map((l) => ({ category: l.category, size: l.size, quantity: Number(l.quantity) })),
    };

    this.submitting.set(true);
    this.api.create(request).subscribe({
      next: (created) => {
        this.toast.success(`Đã tạo lệnh giao ${created.orderNumber}`);
        this.resetForm();
        // The new order is the newest one, so it shows at the top of page 1
        this.loadPage(1);
        this.submitting.set(false);
      },
      error: (err: unknown) => {
        this.apiError.set(toApiError(err));
        this.submitting.set(false);
      },
    });
  }

  // "Làm lại": empty form with one Dry 20ft line; the operator stays because orders often come in batches
  protected resetForm(): void {
    const { lineOperatorId } = this.form.getRawValue();
    this.form.controls.lines.clear();
    this.form.controls.lines.push(this.newLine('Dry', 'Ft20'));
    this.form.reset({ lineOperatorId, expiryDate: this.today });
    this.submitted.set(false);
    this.apiError.set(null);
  }

  // Red border + message for a plain rule or a matching API validation error (PascalCase field names)
  protected invalid(name: 'orderNumber' | 'lineOperatorId' | 'customerId' | 'vesselVoyage'): boolean {
    const control = this.form.controls[name];
    return (control.invalid && (control.touched || this.submitted())) || !!fieldError(this.apiError(), name);
  }

  protected quantityInvalid(index: number): boolean {
    const control = this.form.controls.lines.at(index).controls.quantity;
    return control.invalid && (control.touched || this.submitted());
  }

  private newLine(category: ContainerCategory, size: ContainerSize) {
    return this.fb.group({
      category: [category],
      size: [size],
      quantity: [1, [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)]],
    });
  }

  private loadPage(page: number): void {
    const f = this.filterForm.getRawValue();
    this.api.list(page, LIST_PAGE_SIZE, { search: f.search, status: f.status || null, operatorId: f.operatorId || null }).subscribe({
      next: (result) => {
        this.orders.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.page.set(page);
        this.listError.set(null);
      },
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });
  }
}
