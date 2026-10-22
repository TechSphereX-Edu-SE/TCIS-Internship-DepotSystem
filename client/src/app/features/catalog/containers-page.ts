import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable, map, startWith } from 'rxjs';
import { ApiError, fieldError, toApiError } from '../../core/api/api-error';
import { checkContainerNumber, containerNumberError, isoCodeError, suggestIsoCode } from '../../core/depot/container-number';
import {
  CATEGORIES,
  CATEGORY_HINT,
  CATEGORY_LABEL,
  CONDITION_LABEL,
  Container,
  ContainerCategory,
  ContainerSize,
  SIZE_LABEL,
  UpdateContainerRequest,
  formatDate,
  vietnamToday,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { Pager } from '../../shared/pager/pager';
import { ToastService } from '../../shared/toast/toast-service';

const PAGE_SIZE = 20;

// Whole kilograms above 0
const weightValidators = [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)];

/*
  Spec section 2: container master data (Appendix I number, ISO size-type code, size, type of
  Appendix II, weights, manufacture date, owner). The container number is the identity of the box,
  so it can only be typed when creating; editing changes the other fields (PUT keeps the number).
  The number and the ISO code are checked while typing with the same rules as the API validators,
  so the clerk sees the mistake in Vietnamese before saving.
  The condition is changed on the gate-out screen (repair flow), not here.
*/
@Component({
  selector: 'app-containers-page',
  imports: [ReactiveFormsModule, Pager],
  templateUrl: './containers-page.html',
  styleUrl: './containers-page.css',
})
export class ContainersPage {
  private readonly api = inject(MasterDataApi);
  private readonly toast = inject(ToastService);

  protected readonly sizes: ContainerSize[] = ['Ft20', 'Ft40'];
  protected readonly categories = CATEGORIES;
  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly categoryHint = CATEGORY_HINT;
  protected readonly conditionLabel = CONDITION_LABEL;
  protected readonly formatDate = formatDate;
  // A manufacture date in the future is rejected by the API
  protected readonly today = vietnamToday();

  // Same fields as CreateContainerCommand; the number is disabled when editing
  protected readonly form = inject(FormBuilder).nonNullable.group({
    containerNumber: ['', Validators.required],
    size: ['Ft20' as ContainerSize],
    category: ['Dry' as ContainerCategory],
    isoCode: ['', Validators.required],
    tareWeightKg: new FormControl<number | null>(null, weightValidators),
    maxGrossWeightKg: new FormControl<number | null>(null, weightValidators),
    manufactureDate: [''],
    ownerName: ['', [Validators.required, Validators.maxLength(100)]],
  });

  protected readonly search = new FormControl('', { nonNullable: true });

  protected readonly items = signal<Container[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly listError = signal<string | null>(null);
  // Container loaded in the form; null = the form creates a new one
  protected readonly editing = signal<Container | null>(null);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  private readonly value = toSignal(
    this.form.valueChanges.pipe(
      startWith(null),
      map(() => this.form.getRawValue()),
    ),
    { initialValue: this.form.getRawValue() },
  );

  // Appendix I: format + check digit; only when creating (the number of an existing container is fixed)
  protected readonly numberError = computed(() => {
    if (this.editing()) return null;
    const number = this.value().containerNumber.trim();
    // Wait for 11 characters before judging, unless the user already pressed save
    if (number.length < 11 && !this.submitted()) return null;
    return containerNumberError(checkContainerNumber(number));
  });

  protected readonly checkDigitOk = computed(() => {
    if (this.editing()) return null;
    const check = checkContainerNumber(this.value().containerNumber);
    return check.valid ? check.checkDigit : null;
  });

  // ISO 6346 code against the chosen size and type (same as IsoCodeRules)
  protected readonly isoError = computed(() => {
    const { isoCode, size, category } = this.value();
    if (!isoCode.trim() && !this.submitted()) return null;
    return isoCodeError(isoCode, size, category);
  });

  protected readonly isoSuggestion = computed(() => suggestIsoCode(this.value().size, this.value().category));

  // Same rule as the validator: max gross weight (cargo + tare) must be greater than the tare weight
  protected readonly weightError = computed(() => {
    const { tareWeightKg, maxGrossWeightKg } = this.value();
    if (tareWeightKg === null || maxGrossWeightKg === null) return null;
    return Number(maxGrossWeightKg) > Number(tareWeightKg) ? null : 'Trọng lượng tối đa phải lớn hơn trọng lượng vỏ.';
  });

  protected readonly futureDate = computed(() => {
    const date = this.value().manufactureDate;
    return !!date && date > this.today;
  });

  constructor() {
    this.load(1);
  }

  protected load(page: number): void {
    this.api.containerPage(this.search.value, page, PAGE_SIZE).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.page.set(page);
        this.listError.set(null);
      },
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });
  }

  protected useSuggestion(): void {
    this.form.controls.isoCode.setValue(this.isoSuggestion());
  }

  protected edit(item: Container): void {
    this.editing.set(item);
    this.form.reset({
      containerNumber: item.containerNumber,
      size: item.size,
      category: item.category,
      isoCode: item.isoCode,
      tareWeightKg: item.tareWeightKg,
      maxGrossWeightKg: item.maxGrossWeightKg,
      manufactureDate: item.manufactureDate ?? '',
      ownerName: item.ownerName,
    });
    // The number is the identity of the container (PUT does not take it)
    this.form.controls.containerNumber.disable();
    this.submitted.set(false);
    this.apiError.set(null);
  }

  // Back to "create" mode with an empty form
  protected cancel(): void {
    this.editing.set(null);
    this.form.controls.containerNumber.enable();
    this.form.reset();
    this.submitted.set(false);
    this.apiError.set(null);
  }

  // Only a container that has never been in the yard can be deleted (409 Container.HasHistory)
  protected remove(item: Container): void {
    if (!confirm(`Xoá container ${item.containerNumber} khỏi danh mục?`)) return;
    this.api.deleteContainer(item.id).subscribe({
      next: () => {
        this.toast.success(`Đã xoá ${item.containerNumber}`);
        if (this.editing()?.id === item.id) this.cancel();
        this.load(this.page());
      },
      error: (err: unknown) => this.toast.error(`Không xoá được ${item.containerNumber}: ${toApiError(err).message}`),
    });
  }

  protected submit(): void {
    this.submitted.set(true);
    this.apiError.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid || this.numberError() || this.isoError() || this.weightError() || this.futureDate()) return;

    const v = this.form.getRawValue();
    const data: UpdateContainerRequest = {
      isoCode: v.isoCode.trim().toUpperCase(),
      size: v.size,
      category: v.category,
      tareWeightKg: Number(v.tareWeightKg),
      maxGrossWeightKg: Number(v.maxGrossWeightKg),
      manufactureDate: v.manufactureDate || null,
      ownerName: v.ownerName.trim(),
    };
    const current = this.editing();
    const number = v.containerNumber.trim().toUpperCase();
    const request$: Observable<unknown> = current
      ? this.api.updateContainer(current.id, data)
      : this.api.createContainer({ ...data, containerNumber: number });

    this.submitting.set(true);
    request$.subscribe({
      next: () => {
        this.toast.success(current ? `Đã cập nhật ${current.containerNumber}` : `Đã thêm ${number}`);
        this.cancel();
        this.load(this.page());
        this.submitting.set(false);
      },
      error: (err: unknown) => {
        this.apiError.set(toApiError(err));
        this.submitting.set(false);
      },
    });
  }

  protected invalid(name: 'tareWeightKg' | 'maxGrossWeightKg' | 'ownerName'): boolean {
    const control = this.form.controls[name];
    return (control.invalid && (control.touched || this.submitted())) || !!fieldError(this.apiError(), name);
  }
}
