import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, catchError, concat, debounceTime, distinctUntilChanged, map, of, startWith, switchMap, throwError } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import { checkContainerNumber, containerNumberError, normalizeContainerNumber } from '../../core/depot/container-number';
import { DeliveryOrderApi } from '../../core/depot/delivery-order-api';
import {
  CATEGORY_LABEL,
  CONDITION_LABEL,
  Container,
  ContainerCondition,
  ContainerPosition,
  DailyGateOut,
  DeliveryOrder,
  SIZE_LABEL,
  YardContainer,
  formatDate,
  formatDateTime,
  formatPosition,
  orderStatus,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { YardApi } from '../../core/depot/yard-api';
import { findContainerAbove, toPlaced } from '../../core/depot/yard-rules';
import { ToastService } from '../../shared/toast/toast-service';

// Result of checking the typed container number (locally, then against the API)
type Lookup =
  | { kind: 'idle' }
  | { kind: 'checking' }
  | { kind: 'invalid'; message: string }
  | { kind: 'notFound' }
  | { kind: 'notInYard'; container: Container }
  | { kind: 'ready'; container: Container; position: ContainerPosition }
  | { kind: 'error'; message: string };

// One line of the "check before gate-out" panel (mockup 04); pending = no delivery order chosen yet
interface GateOutCheck {
  title: string;
  detail: string;
  state: 'ok' | 'fail' | 'pending';
}

// The newest orders are the ones still in use; a depot has far fewer open orders than this
const ORDER_LOOKUP_SIZE = 100;

/*
  Spec section 3: gate-out releases a container under a delivery order of its line operator.
  Mockup 04: the form, a panel with every rule the API checks (GateOutCommandHandler order),
  the container condition (a damaged container must be repaired and set back to Normal first),
  and today's gate-outs.
  The rules are shown before submitting so the clerk knows why a container cannot leave;
  the API still checks all of them.
*/
@Component({
  selector: 'app-gate-out-page',
  imports: [ReactiveFormsModule, DatePipe, RouterLink],
  templateUrl: './gate-out-page.html',
  styleUrl: './gate-out-page.css',
})
export class GateOutPage {
  private readonly masterData = inject(MasterDataApi);
  private readonly yard = inject(YardApi);
  private readonly orderApi = inject(DeliveryOrderApi);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly conditionLabel = CONDITION_LABEL;
  protected readonly formatDate = formatDate;

  // Same fields as GateOutCommand (containerId comes from the container found)
  protected readonly form = this.fb.group({
    containerNumber: ['', Validators.required],
    deliveryOrderId: ['', Validators.required],
    vehicleNo: ['', [Validators.required, Validators.maxLength(20)]],
  });

  // Same fields as UpdateContainerConditionCommand (the id is the container found)
  protected readonly conditionForm = this.fb.group({
    condition: ['Normal' as ContainerCondition],
    conditionNote: ['', Validators.maxLength(500)],
  });

  protected readonly lookup = signal<Lookup>({ kind: 'idle' });
  protected readonly orders = signal<DeliveryOrder[]>([]);
  protected readonly rowContainers = signal<YardContainer[]>([]);
  protected readonly todayGateOuts = signal<DailyGateOut[]>([]);
  protected readonly loadError = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);
  protected readonly savingCondition = signal(false);
  protected readonly conditionError = signal<ApiError | null>(null);

  // Selected order id as a signal, so the checks follow the dropdown
  private readonly orderId = toSignal(this.form.controls.deliveryOrderId.valueChanges.pipe(startWith('')), {
    initialValue: '',
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
      case 'notFound':
        return 'Container chưa có trong danh mục.';
      case 'notInYard':
        return `${state.container.containerNumber} không ở trong bãi (chưa nhập hoặc đã xuất).`;
      case 'idle':
        return this.submitted() ? 'Nhập đủ 11 ký tự số container.' : null;
      default:
        return null;
    }
  });

  // Orders that can release this container: same line operator, still valid, quantity left for its type.
  // Others are left out of the dropdown, so the clerk cannot pick an order the API would reject.
  protected readonly usableOrders = computed(() => {
    const state = this.ready();
    if (!state) return [];
    return this.orders().filter(
      (o) =>
        o.lineOperatorCode === state.position.lineOperatorCode &&
        orderStatus(o) === 'open' &&
        o.lines.some((l) => l.category === state.container.category && l.remainingQuantity > 0),
    );
  });

  protected readonly selectedOrder = computed(() => this.usableOrders().find((o) => o.id === this.orderId()) ?? null);

  // Same check as YardPlacement.CheckNothingAboveAsync (409 ContainerAbove)
  protected readonly containerAbove = computed(() => {
    const p = this.ready()?.position;
    if (!p || p.bay === null || p.tier === null) return null;
    return findContainerAbove(p.bay, p.tier, p.size, toPlaced(this.rowContainers(), p.visitId)) ?? null;
  });

  // The rules of GateOutCommandHandler, in the same order, with what they found for this container
  protected readonly checks = computed<GateOutCheck[]>(() => {
    const state = this.ready();
    if (!state) return [];
    const { container, position } = state;
    const order = this.selectedOrder();
    const above = this.containerAbove();
    const line = order?.lines.find((l) => l.category === container.category);
    const category = CATEGORY_LABEL[container.category];

    return [
      {
        title: 'Container đang ở trong bãi',
        detail: `${this.positionText(position)}, vào ${formatDateTime(position.gateInAt)}`,
        state: 'ok',
      },
      {
        title: 'Không bị container khác đè lên',
        detail: above
          ? `${above.containerNumber} đang xếp phía trên (tier ${above.tier}). Đảo chuyển container đó trước.`
          : position.tier === null
            ? 'Block ảo, không xếp chồng'
            : `Tier ${position.tier + 1} phía trên trống`,
        state: above ? 'fail' : 'ok',
      },
      {
        title: 'Tình trạng bình thường',
        detail:
          container.condition === 'Damaged'
            ? `Đang hư hỏng${container.conditionNote ? ': ' + container.conditionNote : ''}. Sửa xong, cập nhật tình trạng bên dưới.`
            : 'Container hư hỏng phải sửa xong mới được giao',
        state: container.condition === 'Damaged' ? 'fail' : 'ok',
      },
      {
        title: 'Lệnh giao đúng hãng',
        detail: order
          ? `${order.orderNumber} của ${order.lineOperatorCode}, container của ${position.lineOperatorCode}`
          : `Chọn lệnh của ${position.lineOperatorCode}`,
        state: order ? 'ok' : 'pending',
      },
      {
        title: 'Lệnh còn hạn',
        detail: order ? `Hạn ${formatDate(order.expiryDate)}, tính theo giờ Việt Nam` : 'Chưa chọn lệnh',
        state: order ? 'ok' : 'pending',
      },
      {
        title: `Lệnh còn số lượng cho loại ${category}`,
        detail: line
          ? `Còn ${line.remainingQuantity}/${line.quantity}, sau khi xuất còn ${line.remainingQuantity - 1}`
          : 'Chưa chọn lệnh',
        state: line ? 'ok' : 'pending',
      },
    ];
  });

  // Every rule must pass; the API would reject the gate-out otherwise
  protected readonly canGateOut = computed(() => {
    const checks = this.checks();
    return checks.length > 0 && checks.every((c) => c.state === 'ok');
  });

  constructor() {
    this.loadOrders();
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

    // Row of the container, to know whether another container rests on it
    toObservable(this.ready)
      .pipe(
        switchMap((state) => {
          const p = state?.position;
          return p && p.row !== null ? this.yard.inYard(p.blockId, p.row).pipe(catchError(() => of([]))) : of([]);
        }),
        takeUntilDestroyed(),
      )
      .subscribe((containers) => this.rowContainers.set(containers));
  }

  protected submit(): void {
    this.submitted.set(true);
    this.apiError.set(null);
    this.form.markAllAsTouched();

    const state = this.ready();
    if (!state || this.form.invalid || !this.canGateOut()) return;

    const v = this.form.getRawValue();
    this.submitting.set(true);
    this.yard
      .gateOut({ containerId: state.container.id, deliveryOrderId: v.deliveryOrderId, vehicleNo: v.vehicleNo.trim() })
      .subscribe({
        next: (result) => {
          this.toast.success(
            `Đã xuất ${result.containerNumber} theo lệnh ${result.orderNumber} · ` +
            `${CATEGORY_LABEL[result.category]} còn ${result.remainingQuantity}`,
          );
          this.resetForNext();
          // Quantities on the orders changed, and the container is in today's gate-outs now
          this.loadOrders();
          this.loadToday();
          this.submitting.set(false);
        },
        error: (err: unknown) => {
          this.apiError.set(toApiError(err));
          this.submitting.set(false);
        },
      });
  }

  // Records the condition, e.g. back to Normal after repair; the gate-out checks update right away
  protected saveCondition(): void {
    const state = this.ready();
    if (!state || this.conditionForm.invalid) return;

    const { condition, conditionNote } = this.conditionForm.getRawValue();
    // The API keeps a note only for a damaged container (Container.UpdateCondition)
    const note = condition === 'Damaged' ? conditionNote.trim() || null : null;

    this.savingCondition.set(true);
    this.conditionError.set(null);
    this.masterData.updateCondition(state.container.id, { condition, conditionNote: note }).subscribe({
      next: () => {
        this.lookup.set({ ...state, container: { ...state.container, condition, conditionNote: note } });
        this.toast.success(`Đã cập nhật ${state.container.containerNumber}: ${CONDITION_LABEL[condition]}`);
        this.savingCondition.set(false);
      },
      error: (err: unknown) => {
        this.conditionError.set(toApiError(err));
        this.savingCondition.set(false);
      },
    });
  }

  // "Làm lại": clear everything for the next truck
  protected resetForNext(): void {
    this.form.reset();
    this.submitted.set(false);
    this.apiError.set(null);
    this.conditionError.set(null);
    this.lookup.set({ kind: 'idle' });
  }

  protected invalid(name: 'deliveryOrderId' | 'vehicleNo'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || this.submitted());
  }

  protected positionText(position: { blockCode: string; bay: number | null; row: number | null; tier: number | null }): string {
    return formatPosition(position.blockCode, position.bay, position.row, position.tier);
  }

  // "DO-001 · Minh An · hạn 31/10/2026 · Dry còn 2/2" (option text of mockup 04)
  protected orderOption(order: DeliveryOrder): string {
    const category = this.ready()?.container.category;
    const line = order.lines.find((l) => l.category === category);
    const left = line ? ` · ${CATEGORY_LABEL[line.category]} còn ${line.remainingQuantity}/${line.quantity}` : '';
    return `${order.orderNumber} · ${order.customerName} · hạn ${formatDate(order.expiryDate)}${left}`;
  }

  // "Dry 0/1" = what is left on the order line / what was ordered
  protected remainingText(row: DailyGateOut): string {
    return row.orderLineQuantity === null
      ? '—'
      : `${CATEGORY_LABEL[row.category]} ${row.orderLineRemaining}/${row.orderLineQuantity}`;
  }

  private checkNumber(number: string): Observable<Lookup> {
    // Wait for the full 11 characters before judging the number
    if (number.length < 11) return of<Lookup>({ kind: 'idle' });

    const check = checkContainerNumber(number);
    if (!check.valid) return of<Lookup>({ kind: 'invalid', message: containerNumberError(check)! });

    const lookup$ = this.masterData.findContainer(number).pipe(
      switchMap((container): Observable<Lookup> => {
        if (!container) return of<Lookup>({ kind: 'notFound' });
        // In the yard -> ready to check the order; 404 NotInYard -> nothing to release
        return this.yard.position(number).pipe(
          map((position): Lookup => ({ kind: 'ready', container, position })),
          catchError((err: unknown) =>
            toApiError(err).code === 'ContainerVisit.NotInYard'
              ? of<Lookup>({ kind: 'notInYard', container })
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
    this.apiError.set(null);
    this.conditionError.set(null);
    this.form.controls.deliveryOrderId.setValue('');
    if (state.kind !== 'ready') return;

    // Start the condition form from what is recorded on the container
    this.conditionForm.reset({ condition: state.container.condition, conditionNote: state.container.conditionNote ?? '' });

    // Only one order fits: pick it, the clerk can still see it in the dropdown
    const usable = this.usableOrders();
    if (usable.length === 1) this.form.controls.deliveryOrderId.setValue(usable[0].id);
  }

  private loadOrders(): void {
    this.orderApi.list(1, ORDER_LOOKUP_SIZE).subscribe({
      next: (page) => this.orders.set(page.items),
      error: (err: unknown) => this.loadError.set(toApiError(err).message),
    });
  }

  private loadToday(): void {
    this.yard.daily().subscribe({
      next: (daily) => this.todayGateOuts.set(daily.gateOuts),
      error: () => this.todayGateOuts.set([]),
    });
  }
}
