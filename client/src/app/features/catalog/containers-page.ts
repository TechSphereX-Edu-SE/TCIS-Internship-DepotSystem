import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, map, startWith } from 'rxjs';
import { ApiError, fieldError, toApiError } from '../../core/api/api-error';
import { checkContainerNumber, containerNumberError, isoCodeError, suggestIsoCode } from '../../core/depot/container-number';
import {
  ASSIGNMENT_STATUS_LABEL,
  CATEGORIES,
  CATEGORY_HINT,
  CATEGORY_LABEL,
  CONDITION_LABEL,
  Container,
  ContainerCategory,
  ContainerCondition,
  ContainerListFilters,
  ContainerSize,
  LineOperator,
  OperatingAssignment,
  SIZE_LABEL,
  UpdateContainerRequest,
  assignmentCovers,
  confirmedOverlap,
  containerFilterParams,
  formatDate,
  handoverEndDate,
  vietnamToday,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { RepairApi } from '../../core/depot/repair-api';
import { LIST_PAGE_SIZE, Pager } from '../../shared/pager/pager';
import { SearchPageLoader, SearchSelect } from '../../shared/search-select/search-select';
import { ToastService } from '../../shared/toast/toast-service';

// Rows asked per page by the search dropdowns (about 5 are visible, the rest load while scrolling)
const SEARCH_PAGE_SIZE = 20;

/*
  The owner partner of a container as a dropdown row, so editing shows the current owner.
  Only the fields the dropdown displays are known from the container; the rest are placeholders.
*/
function ownerOf(container: Container): LineOperator | null {
  if (!container.ownerId) return null;
  return {
    id: container.ownerId,
    code: container.ownerCode ?? '',
    name: container.ownerPartnerName ?? '',
    taxCode: null,
    isOwner: true,
    isOperator: false,
    createdAt: '',
  };
}

// Whole kilograms above 0
const weightValidators = [Validators.required, Validators.min(1), Validators.pattern(/^\d+$/)];

/*
  Spec section 2: container master data (Appendix I number, ISO size-type code, size, type of
  Appendix II, weights, manufacture date, owner). The container number is the identity of the box,
  so it can only be typed when creating; editing changes the other fields (PUT keeps the number).
  The number and the ISO code are checked while typing with the same rules as the API validators,
  so the clerk sees the mistake in Vietnamese before saving.
  The condition is not edited here. Feedback 4: "Báo hư" records a damage found on a container
  (source Catalog); it opens a repair case or joins the open one, and the repair page takes over.

  Feedback 2 (23/10/2026):
    - the owner is picked from the partner catalog (owner role), never typed; containers recorded
      before that keep their old owner text and show "chưa đối chiếu" until someone picks the owner
    - "Khai thác" opens the operating relations of a container: which line operator operates it,
      for which period. Gate-in only uses a confirmed relation covering the gate-in date.
*/
@Component({
  selector: 'app-containers-page',
  imports: [ReactiveFormsModule, RouterLink, Pager, SearchSelect],
  templateUrl: './containers-page.html',
  styleUrl: './containers-page.css',
})
export class ContainersPage {
  private readonly api = inject(MasterDataApi);
  private readonly repairs = inject(RepairApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
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
    owner: new FormControl<LineOperator | null>(null, Validators.required),
  });

  /*
    List filters (feedback 23/10: the catalog had only the number search).
    Applied with "Lọc" (or Enter), not on every change, so typing a number does not reload each key.
  */
  protected readonly conditions: ContainerCondition[] = ['Normal', 'Damaged'];
  protected readonly filters = inject(FormBuilder).nonNullable.group({
    search: [''],
    ownerId: [''],
    operator: [''],
    category: ['' as ContainerListFilters['category']],
    size: ['' as ContainerListFilters['size']],
    condition: ['' as ContainerListFilters['condition']],
    location: ['' as ContainerListFilters['location']],
  });
  protected readonly search = this.filters.controls.search;
  // Partners for the owner / operator filters (a short list: every partner of the depot)
  protected readonly owners = signal<LineOperator[]>([]);
  protected readonly operators = signal<LineOperator[]>([]);
  // True once a filter other than the number is set: the empty list then says "no match"
  protected readonly filtered = signal(false);

  // Partner dropdowns: owners for the container form, line operators for an operating relation
  protected readonly loadOwners: SearchPageLoader<LineOperator> = (term, page) =>
    this.api.lineOperatorPage(page, SEARCH_PAGE_SIZE, term, 'Owner');
  protected readonly loadOperators: SearchPageLoader<LineOperator> = (term, page) =>
    this.api.lineOperatorPage(page, SEARCH_PAGE_SIZE, term, 'Operator');
  protected readonly partnerKey = (p: LineOperator) => p.id;
  protected readonly partnerLabel = (p: LineOperator) => p.code;
  protected readonly partnerDetail = (p: LineOperator) => (p.taxCode ? `${p.name} · MST ${p.taxCode}` : p.name);

  // ---------- operating relations (quan hệ khai thác) of one container ----------
  protected readonly statusLabel = ASSIGNMENT_STATUS_LABEL;
  protected readonly covers = assignmentCovers;
  protected readonly relationsOf = signal<Container | null>(null);
  protected readonly assignments = signal<OperatingAssignment[]>([]);
  protected readonly assignmentsError = signal<string | null>(null);
  protected readonly assignmentSubmitted = signal(false);
  protected readonly assignmentSaving = signal(false);
  protected readonly assignmentApiError = signal<ApiError | null>(null);
  // Opened from the gate-in page (?returnTo=gate-in): the relations card links back with the container picked
  protected readonly returnToGateIn = signal(false);

  // Same rules as CreateOperatingAssignmentValidator; confirm = already checked against the contract
  protected readonly assignmentForm = inject(FormBuilder).nonNullable.group({
    operator: new FormControl<LineOperator | null>(null, Validators.required),
    validFrom: [vietnamToday(), Validators.required],
    validTo: [''],
    reference: ['', Validators.maxLength(50)],
    note: ['', Validators.maxLength(500)],
    confirm: [true],
  });
  private readonly assignmentValue = toSignal(
    this.assignmentForm.valueChanges.pipe(
      startWith(null),
      map(() => this.assignmentForm.getRawValue()),
    ),
    { initialValue: this.assignmentForm.getRawValue() },
  );

  /*
    Feedback 23/10: a confirmed relation that the new period would clash with (the API refuses it with
    OperatingAssignment.Overlap). Shown before saving, with the one-step fix "end it the day before".
    A pending relation (confirm unticked) is not checked: it is only checked when it gets confirmed.
  */
  protected readonly overlap = computed(() => {
    const v = this.assignmentValue();
    return v.confirm && v.validFrom ? confirmedOverlap(this.assignments(), v.validFrom, v.validTo || null) : null;
  });
  protected readonly handoverDate = computed(() => {
    const clash = this.overlap();
    const from = this.assignmentValue().validFrom;
    return clash && from ? handoverEndDate(clash, from) : null;
  });

  // "Kết thúc" opens a date field in the row (was a prompt asking to type yyyy-MM-dd)
  protected readonly endingId = signal<string | null>(null);
  protected readonly endDate = new FormControl(vietnamToday(), { nonNullable: true, validators: Validators.required });

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
    this.openFromLink();
    this.api.lineOperators('Owner').subscribe({ next: (list) => this.owners.set(list) });
    this.api.lineOperators('Operator').subscribe({ next: (list) => this.operators.set(list) });
  }

  // openRelationsFor: container number whose relations card opens once the page is loaded
  protected load(page: number, openRelationsFor?: string): void {
    const { search, ...rest } = this.filters.getRawValue();
    const params = containerFilterParams(rest);
    this.filtered.set(Object.keys(params).length > 0);
    this.api.containerPage(search, page, LIST_PAGE_SIZE, null, params).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.page.set(page);
        this.listError.set(null);
        const target = openRelationsFor && result.items.find((c) => c.containerNumber === openRelationsFor);
        if (target) this.openRelations(target);
      },
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });
  }

  /*
    Feedback 23/10: gate-in links here with "?relations=<number>&returnTo=gate-in" when a container has no
    confirmed operator today. The list is searched for that number and its relations card opens, so the
    clerk records and confirms the relation without looking for the row. Without the parameter: plain list.
  */
  private openFromLink(): void {
    const params = this.route.snapshot.queryParamMap;
    const number = params.get('relations')?.trim().toUpperCase();
    this.returnToGateIn.set(!!number && params.get('returnTo') === 'gate-in');
    if (!number) {
      this.load(1);
      return;
    }
    this.search.setValue(number);
    this.load(1, number);
  }

  protected clearFilters(): void {
    this.filters.reset();
    this.load(1);
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
      // Not reconciled yet: the old text is shown next to the field, the user picks the partner
      owner: ownerOf(item),
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

  // A short description is enough here; photos and the next steps are added on the repair page
  protected reportDamage(item: Container): void {
    const description = prompt(`Mô tả hư hỏng của ${item.containerNumber} (móp, méo, cong, thủng…):`)?.trim();
    if (!description) return;
    if (description.length > 500) {
      this.toast.error('Mô tả hư hỏng tối đa 500 ký tự.');
      return;
    }
    this.repairs.report({ containerId: item.id, description, source: 'Catalog' }).subscribe({
      next: (recorded) => {
        this.toast.success(
          recorded.created
            ? `Đã mở hồ sơ giám định cho ${item.containerNumber}`
            : `Đã thêm hư hỏng vào hồ sơ đang mở của ${item.containerNumber}`,
        );
        void this.router.navigate(['/repairs'], { queryParams: { caseId: recorded.caseId } });
      },
      error: (err: unknown) => this.toast.error(`Không ghi nhận được: ${toApiError(err).message}`),
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
      ownerId: v.owner!.id,
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

  protected invalid(name: 'tareWeightKg' | 'maxGrossWeightKg' | 'owner'): boolean {
    const control = this.form.controls[name];
    const apiField = name === 'owner' ? 'ownerId' : name;
    return (control.invalid && (control.touched || this.submitted())) || !!fieldError(this.apiError(), apiField);
  }

  // Owner code when reconciled, otherwise the text recorded before partners existed
  protected ownerText(item: Container): string {
    return item.ownerCode ?? item.ownerName;
  }

  // ---------- operating relations ----------

  protected openRelations(item: Container): void {
    this.relationsOf.set(item);
    this.assignmentForm.reset({ validFrom: vietnamToday(), confirm: true });
    this.assignmentSubmitted.set(false);
    this.assignmentApiError.set(null);
    this.loadAssignments(item);
    // The card is below the list and the form: bring it into view, or the button seems to do nothing
    setTimeout(() => document.getElementById('relations-title')?.scrollIntoView?.({ behavior: 'smooth', block: 'start' }));
  }

  protected closeRelations(): void {
    this.relationsOf.set(null);
    this.endingId.set(null);
    this.assignments.set([]);
  }

  // The period of a relation as "01/10/2026 → 31/12/2026" or "01/10/2026 → (không thời hạn)"
  protected periodText(a: OperatingAssignment): string {
    return `${formatDate(a.validFrom)} → ${a.validTo ? formatDate(a.validTo) : '(không thời hạn)'}`;
  }

  protected addAssignment(): void {
    const container = this.relationsOf();
    this.assignmentSubmitted.set(true);
    this.assignmentApiError.set(null);
    this.assignmentForm.markAllAsTouched();
    if (!container || this.assignmentForm.invalid || this.periodError()) return;

    const v = this.assignmentForm.getRawValue();
    this.assignmentSaving.set(true);
    this.api
      .createAssignment(container.id, {
        operatorId: v.operator!.id,
        validFrom: v.validFrom,
        validTo: v.validTo || null,
        reference: v.reference.trim() || null,
        note: v.note.trim() || null,
        confirm: v.confirm,
      })
      .subscribe({
        next: () => {
          this.toast.success(`Đã ghi nhận ${v.operator!.code} khai thác ${container.containerNumber}`);
          this.assignmentForm.reset({ validFrom: vietnamToday(), confirm: true });
          this.assignmentSubmitted.set(false);
          this.assignmentSaving.set(false);
          this.afterAssignmentChange(container);
        },
        error: (err: unknown) => {
          this.assignmentApiError.set(toApiError(err));
          this.assignmentSaving.set(false);
        },
      });
  }

  // The end date cannot be before the start date (same rule as the API)
  protected periodError(): boolean {
    const { validFrom, validTo } = this.assignmentForm.getRawValue();
    return !!validTo && !!validFrom && validTo < validFrom;
  }

  protected confirmAssignment(a: OperatingAssignment): void {
    this.runAssignmentAction(this.api.confirmAssignment(a.id), `Đã xác nhận ${a.operatorCode}`);
  }

  // Opens the end date field of one row; today by default, never before the start of the relation
  protected startEnding(a: OperatingAssignment): void {
    const today = vietnamToday();
    this.endDate.setValue(today >= a.validFrom ? today : a.validFrom);
    this.endingId.set(a.id);
  }

  // Last day included; a visit that already came in later keeps its basis, so the API may refuse
  protected endAssignment(a: OperatingAssignment): void {
    const validTo = this.endDate.value;
    if (!validTo) return;
    this.runAssignmentAction(this.api.endAssignment(a.id, validTo), `Đã kết thúc ${a.operatorCode}`, () =>
      this.endingId.set(null),
    );
  }

  /*
    Change of operator in one step: end the clashing relation the day before the new period starts,
    then save the new one. If the end is refused (e.g. a gate-in after that day used it), nothing is added.
  */
  protected endClashAndAdd(): void {
    const clash = this.overlap();
    const lastDay = this.handoverDate();
    if (!clash || !lastDay) return;
    this.api.endAssignment(clash.id, lastDay).subscribe({
      next: () => {
        this.toast.success(`Đã kết thúc ${clash.operatorCode} ngày ${formatDate(lastDay)}`);
        // The list now has the shortened period, so the form no longer shows the clash
        this.assignments.update((list) => list.map((a) => (a.id === clash.id ? { ...a, validTo: lastDay } : a)));
        this.addAssignment();
      },
      error: (err: unknown) => this.assignmentApiError.set(toApiError(err)),
    });
  }

  // Only for a relation recorded by mistake (none of its visits); the reason is kept
  protected cancelAssignment(a: OperatingAssignment): void {
    const reason = prompt(`Lý do huỷ quan hệ khai thác ${a.operatorCode}:`);
    if (!reason?.trim()) return;
    this.runAssignmentAction(this.api.cancelAssignment(a.id, reason.trim()), `Đã huỷ ${a.operatorCode}`);
  }

  private runAssignmentAction(request$: Observable<void>, message: string, done?: () => void): void {
    const container = this.relationsOf();
    if (!container) return;
    request$.subscribe({
      next: () => {
        this.toast.success(message);
        done?.();
        this.afterAssignmentChange(container);
      },
      error: (err: unknown) => this.toast.error(toApiError(err).message),
    });
  }

  // The list column "Hãng khai thác hôm nay" depends on the relations, so the page reloads too
  private afterAssignmentChange(container: Container): void {
    this.loadAssignments(container);
    this.load(this.page());
  }

  private loadAssignments(container: Container): void {
    this.api.containerAssignments(container.id).subscribe({
      next: (list) => {
        this.assignments.set(list);
        this.assignmentsError.set(null);
      },
      error: (err: unknown) => this.assignmentsError.set(toApiError(err).message),
    });
  }
}
