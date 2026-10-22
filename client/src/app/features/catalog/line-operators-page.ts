import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, fieldError, toApiError } from '../../core/api/api-error';
import { LineOperator } from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { Pager } from '../../shared/pager/pager';
import { ToastService } from '../../shared/toast/toast-service';

const PAGE_SIZE = 20;

/*
  Spec section 2: line operators (hãng khai thác) — code + name.
  The code (MSC, CMA…) is how orders, visits and reports refer to an operator, so the API only
  lets the name change; in edit mode the code field is read-only.
*/
@Component({
  selector: 'app-line-operators-page',
  imports: [ReactiveFormsModule, DatePipe, Pager],
  templateUrl: './line-operators-page.html',
})
export class LineOperatorsPage {
  private readonly api = inject(MasterDataApi);
  private readonly toast = inject(ToastService);

  // Same rules as CreateLineOperatorValidator
  protected readonly form = inject(FormBuilder).nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9]{2,10}$/)]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
  });

  protected readonly items = signal<LineOperator[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly listError = signal<string | null>(null);
  // Operator loaded in the form; null = the form creates a new one
  protected readonly editing = signal<LineOperator | null>(null);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  constructor() {
    this.load(1);
  }

  protected load(page: number): void {
    this.api.lineOperatorPage(page, PAGE_SIZE).subscribe({
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

  protected edit(item: LineOperator): void {
    this.editing.set(item);
    this.form.reset({ code: item.code, name: item.name });
    // The code cannot change (PUT only takes the name)
    this.form.controls.code.disable();
    this.submitted.set(false);
    this.apiError.set(null);
  }

  // Back to "create" mode with an empty form
  protected cancel(): void {
    this.editing.set(null);
    this.form.controls.code.enable();
    this.form.reset();
    this.submitted.set(false);
    this.apiError.set(null);
  }

  protected submit(): void {
    this.submitted.set(true);
    this.apiError.set(null);
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const { code, name } = this.form.getRawValue();
    const current = this.editing();
    const request$: Observable<unknown> = current
      ? this.api.renameLineOperator(current.id, name.trim())
      : this.api.createLineOperator({ code: code.trim(), name: name.trim() });

    this.submitting.set(true);
    request$.subscribe({
      next: () => {
        this.toast.success(current ? `Đã đổi tên hãng ${current.code}` : `Đã thêm hãng ${code.trim().toUpperCase()}`);
        this.cancel();
        // A new operator may land on any page (the API sorts the list), so stay on the page shown
        this.load(this.page());
        this.submitting.set(false);
      },
      error: (err: unknown) => {
        this.apiError.set(toApiError(err));
        this.submitting.set(false);
      },
    });
  }

  protected invalid(name: 'code' | 'name'): boolean {
    const control = this.form.controls[name];
    return (control.invalid && (control.touched || this.submitted())) || !!fieldError(this.apiError(), name);
  }
}
