import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { ApiError, fieldError, toApiError } from '../../core/api/api-error';
import { LineOperator, PartnerRole, partnerRolesText } from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { Pager } from '../../shared/pager/pager';
import { ToastService } from '../../shared/toast/toast-service';

const PAGE_SIZE = 20;

// Same pattern as PartnerRules.TaxCodePattern: 10 digits, optionally "-" and a 3-digit branch number
const TAX_CODE_PATTERN = /^\s*\d{10}(-\d{3})?\s*$/;

// At least one role, otherwise the partner could not be used anywhere (same rule as the API validator)
function atLeastOneRole(group: AbstractControl): ValidationErrors | null {
  const { isOwner, isOperator } = group.value as { isOwner: boolean; isOperator: boolean };
  return isOwner || isOperator ? null : { noRole: true };
}

/*
  Feedback 2 (Q2): one catalog of partner companies (Đối tác) instead of "line operators" only.
  A company is recorded once with its roles: owner (chủ sở hữu container), line operator (hãng khai thác),
  or both. The code (MSC, CMA…) is how orders, visits and reports refer to it, so it never changes;
  name, tax code and roles can. The API refuses removing a role that is still in use.
*/
@Component({
  selector: 'app-line-operators-page',
  imports: [ReactiveFormsModule, DatePipe, Pager],
  templateUrl: './line-operators-page.html',
})
export class LineOperatorsPage {
  private readonly api = inject(MasterDataApi);
  private readonly toast = inject(ToastService);

  protected readonly rolesText = partnerRolesText;

  // Same rules as CreateLineOperatorValidator / UpdateLineOperatorValidator
  protected readonly form = inject(FormBuilder).nonNullable.group(
    {
      code: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9]{2,10}$/)]],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      taxCode: ['', [Validators.pattern(TAX_CODE_PATTERN)]],
      isOwner: [false],
      isOperator: [true],
    },
    { validators: atLeastOneRole },
  );

  // List filter by role; '' = every partner
  protected readonly roleFilter = signal<PartnerRole | ''>('');
  protected readonly items = signal<LineOperator[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly listError = signal<string | null>(null);
  // Partner loaded in the form; null = the form creates a new one
  protected readonly editing = signal<LineOperator | null>(null);
  protected readonly submitting = signal(false);
  protected readonly submitted = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  constructor() {
    this.load(1);
  }

  protected load(page: number): void {
    this.api.lineOperatorPage(page, PAGE_SIZE, '', this.roleFilter() || undefined).subscribe({
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

  protected filterByRole(role: string): void {
    this.roleFilter.set(role as PartnerRole | '');
    this.load(1);
  }

  protected edit(item: LineOperator): void {
    this.editing.set(item);
    this.form.reset({
      code: item.code,
      name: item.name,
      taxCode: item.taxCode ?? '',
      isOwner: item.isOwner,
      isOperator: item.isOperator,
    });
    // The code cannot change (PUT does not take it)
    this.form.controls.code.disable();
    this.submitted.set(false);
    this.apiError.set(null);
  }

  // Back to "create" mode with an empty form (a new partner is a line operator by default)
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

    const { code, name, taxCode, isOwner, isOperator } = this.form.getRawValue();
    const profile = { name: name.trim(), taxCode: taxCode.trim() || null, isOwner, isOperator };
    const current = this.editing();
    const request$: Observable<unknown> = current
      ? this.api.updateLineOperator(current.id, profile)
      : this.api.createLineOperator({ code: code.trim(), ...profile });

    this.submitting.set(true);
    request$.subscribe({
      next: () => {
        this.toast.success(current ? `Đã cập nhật đối tác ${current.code}` : `Đã thêm đối tác ${code.trim().toUpperCase()}`);
        this.cancel();
        // A new partner may land on any page (the API sorts the list), so stay on the page shown
        this.load(this.page());
        this.submitting.set(false);
      },
      error: (err: unknown) => {
        this.apiError.set(toApiError(err));
        this.submitting.set(false);
      },
    });
  }

  protected invalid(name: 'code' | 'name' | 'taxCode'): boolean {
    const control = this.form.controls[name];
    return (control.invalid && (control.touched || this.submitted())) || !!fieldError(this.apiError(), name);
  }

  // Shown after the first submit, like the other form errors
  protected noRole(): boolean {
    return this.form.hasError('noRole') && this.submitted();
  }
}
