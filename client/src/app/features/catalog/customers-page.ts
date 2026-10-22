import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, fieldError, toApiError } from '../../core/api/api-error';
import { Customer } from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { Pager } from '../../shared/pager/pager';
import { ToastService } from '../../shared/toast/toast-service';

const PAGE_SIZE = 20;

/*
  Spec section 3: customers of the delivery orders (tax code + name).
  The tax code identifies the customer, so the API only lets the name change;
  in edit mode the tax code field is read-only. The list can be searched by tax code or name.
*/
@Component({
  selector: 'app-customers-page',
  imports: [ReactiveFormsModule, DatePipe, Pager],
  templateUrl: './customers-page.html',
})
export class CustomersPage {
  private readonly api = inject(MasterDataApi);
  private readonly toast = inject(ToastService);

  // Same rules as CreateCustomerValidator: 10 digits, or 10 digits + "-" + 3 digits for a branch
  protected readonly form = inject(FormBuilder).nonNullable.group({
    taxCode: ['', [Validators.required, Validators.pattern(/^\d{10}(-\d{3})?$/)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
  });

  protected readonly search = new FormControl('', { nonNullable: true });

  protected readonly items = signal<Customer[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly listError = signal<string | null>(null);
  // Customer loaded in the form; null = the form creates a new one
  protected readonly editing = signal<Customer | null>(null);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  constructor() {
    this.load(1);
  }

  // The search applies from page 1; paging keeps the same search
  protected load(page: number): void {
    this.api.customerPage(this.search.value, page, PAGE_SIZE).subscribe({
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

  protected edit(item: Customer): void {
    this.editing.set(item);
    this.form.reset({ taxCode: item.taxCode, name: item.name });
    // The tax code cannot change (PUT only takes the name)
    this.form.controls.taxCode.disable();
    this.submitted.set(false);
    this.apiError.set(null);
  }

  // Back to "create" mode with an empty form
  protected cancel(): void {
    this.editing.set(null);
    this.form.controls.taxCode.enable();
    this.form.reset();
    this.submitted.set(false);
    this.apiError.set(null);
  }

  protected submit(): void {
    this.submitted.set(true);
    this.apiError.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const { taxCode, name } = this.form.getRawValue();
    const current = this.editing();
    const request$: Observable<unknown> = current
      ? this.api.renameCustomer(current.id, name.trim())
      : this.api.createCustomer({ taxCode: taxCode.trim(), name: name.trim() });

    this.submitting.set(true);
    request$.subscribe({
      next: () => {
        this.toast.success(current ? `Đã đổi tên khách hàng ${current.taxCode}` : `Đã thêm khách hàng ${taxCode.trim()}`);
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

  protected invalid(name: 'taxCode' | 'name'): boolean {
    const control = this.form.controls[name];
    return (control.invalid && (control.touched || this.submitted())) || !!fieldError(this.apiError(), name);
  }
}
