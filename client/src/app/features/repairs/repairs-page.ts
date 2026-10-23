import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, concatMap, from, map, of, toArray } from 'rxjs';
import { ApiError, toApiError } from '../../core/api/api-error';
import {
  CATEGORY_LABEL,
  CONDITION_LABEL,
  FINDING_SOURCE_LABEL,
  LineOperator,
  REPAIR_ACTIONS_NEEDING_NOTE,
  REPAIR_ACTION_LABEL,
  REPAIR_EVENT_LABEL,
  REPAIR_STATUSES,
  REPAIR_STATUS_LABEL,
  RepairAction,
  RepairCaseDetail,
  RepairCaseFilter,
  RepairCaseStatus,
  RepairCaseSummary,
  SIZE_LABEL,
  formatDateTime,
  formatPosition,
  photoFileError,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { RepairApi } from '../../core/depot/repair-api';
import { Pager } from '../../shared/pager/pager';
import { ToastService } from '../../shared/toast/toast-service';

const PAGE_SIZE = 20;

// Chip colour per status: waiting for someone = amber, repair under way = indigo, closed = green
const STATUS_CHIP: Record<RepairCaseStatus, string> = {
  AwaitingInspection: 'chip-amber',
  AwaitingEstimateApproval: 'chip-amber',
  RepairApproved: 'chip-indigo',
  InRepair: 'chip-indigo',
  AwaitingAcceptance: 'chip-amber',
  Passed: 'chip-green',
};

// Status dropdown: "open" (every status but Đạt) is the default, the work still to do
type StatusChoice = 'open' | 'all' | RepairCaseStatus;

// The positive step of a pair is the primary button; the other choices are secondary
const PRIMARY_ACTIONS: readonly RepairAction[] = [
  'InspectNeedsRepair',
  'ApproveRepair',
  'StartRepair',
  'CompleteRepair',
  'AcceptPassed',
];

/*
  Feedback 4 + 8 (Giám định & sửa chữa): one case per damage lifecycle of a container.
    - Left: the cases, filtered by container number, status, line operator; "Lọc thêm" = reported dates.
    - Right: the selected case: findings, photos, history and the next steps the API allows
      (allowedActions), so the screen never offers a step the API would refuse.
  The selected case is in the URL (?caseId=), so gate-in, gate-out and the container catalog can link
  straight to it and a reload keeps it open.
  Passed (Đạt) only lifts the repair block; the container stays where it is and leaves through gate-out.
*/
@Component({
  selector: 'app-repairs-page',
  imports: [ReactiveFormsModule, Pager],
  templateUrl: './repairs-page.html',
  styleUrl: './repairs-page.css',
})
export class RepairsPage {
  private readonly api = inject(RepairApi);
  private readonly masterData = inject(MasterDataApi);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder).nonNullable;

  protected readonly statuses = REPAIR_STATUSES;
  protected readonly statusLabel = REPAIR_STATUS_LABEL;
  protected readonly statusChip = STATUS_CHIP;
  protected readonly actionLabel = REPAIR_ACTION_LABEL;
  protected readonly eventLabel = REPAIR_EVENT_LABEL;
  protected readonly sourceLabel = FINDING_SOURCE_LABEL;
  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly conditionLabel = CONDITION_LABEL;
  protected readonly formatDateTime = formatDateTime;

  // Main filters, plus the "Lọc thêm" dates (Vietnam calendar days, both included)
  protected readonly filters = this.fb.group({
    search: [''],
    status: ['open' as StatusChoice],
    operatorId: [''],
    reportedFrom: [''],
    reportedTo: [''],
  });
  protected readonly moreFilters = signal(false);

  // Note of the next step; required for "Nghiệm thu: Sửa lại" (the reason is kept in the history)
  protected readonly actionForm = this.fb.group({ note: ['', Validators.maxLength(500)] });
  // New finding seen during the inspection
  protected readonly findingForm = this.fb.group({
    description: ['', [Validators.required, Validators.maxLength(500)]],
  });
  // Photos go to the whole case, or to one finding when picked
  protected readonly photoFindingId = this.fb.control('');

  protected readonly items = signal<RepairCaseSummary[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly listError = signal<string | null>(null);
  protected readonly operators = signal<LineOperator[]>([]);

  protected readonly selectedId = signal<string | null>(null);
  protected readonly detail = signal<RepairCaseDetail | null>(null);
  protected readonly detailError = signal<string | null>(null);
  protected readonly acting = signal(false);
  protected readonly actionError = signal<ApiError | null>(null);
  protected readonly noteMissing = signal(false);
  protected readonly addingFinding = signal(false);
  protected readonly findingError = signal<ApiError | null>(null);
  protected readonly uploading = signal(false);
  protected readonly photoErrors = signal<string[]>([]);

  // Photo id -> object URL of its bytes (fetched with the bearer token, see RepairApi.photo)
  protected readonly photoUrls = signal<Record<string, string>>({});

  protected readonly isOpen = computed(() => {
    const d = this.detail();
    return !!d && d.status !== 'Passed';
  });

  protected readonly primaryActions = computed(() =>
    (this.detail()?.allowedActions ?? []).filter((a) => PRIMARY_ACTIONS.includes(a)),
  );
  protected readonly otherActions = computed(() =>
    (this.detail()?.allowedActions ?? []).filter((a) => !PRIMARY_ACTIONS.includes(a)),
  );

  constructor() {
    this.loadPage(1);
    this.masterData.lineOperators('Operator').subscribe({
      next: (operators) => this.operators.set(operators),
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });

    // The URL decides which case is open (links from other screens, reload, back button)
    this.route.queryParamMap
      .pipe(
        map((params) => params.get('caseId')),
        takeUntilDestroyed(),
      )
      .subscribe((id) => this.openCase(id));

    inject(DestroyRef).onDestroy(() => this.revokePhotos());
  }

  protected search(): void {
    this.loadPage(1);
  }

  protected clearFilters(): void {
    this.filters.reset();
    this.loadPage(1);
  }

  protected goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) this.loadPage(page);
  }

  protected select(item: RepairCaseSummary): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: { caseId: item.id }, replaceUrl: true });
  }

  protected closeDetail(): void {
    void this.router.navigate([], { relativeTo: this.route, queryParams: {}, replaceUrl: true });
  }

  protected needsNote(action: RepairAction): boolean {
    return REPAIR_ACTIONS_NEEDING_NOTE.includes(action);
  }

  // One step of the flow; the note goes into the history line of that step
  protected act(action: RepairAction): void {
    const current = this.detail();
    if (!current || this.acting()) return;

    const note = this.actionForm.controls.note.value.trim();
    this.noteMissing.set(this.needsNote(action) && !note);
    if (this.noteMissing() || this.actionForm.invalid) return;

    this.acting.set(true);
    this.actionError.set(null);
    this.api.act(current.id, action, note || null).subscribe({
      next: () => {
        this.toast.success(`${current.containerNumber}: ${REPAIR_ACTION_LABEL[action]}`);
        this.actionForm.reset();
        this.acting.set(false);
        this.reload(current.id);
      },
      error: (err: unknown) => {
        this.actionError.set(toApiError(err));
        this.acting.set(false);
      },
    });
  }

  // A damage found during the inspection joins this case (the container has only one open case)
  protected addFinding(): void {
    const current = this.detail();
    this.findingForm.markAllAsTouched();
    if (!current || this.findingForm.invalid || this.addingFinding()) return;

    this.addingFinding.set(true);
    this.findingError.set(null);
    this.api
      .report({
        containerId: current.containerId,
        description: this.findingForm.controls.description.value.trim(),
        source: 'Inspection',
      })
      .subscribe({
        next: () => {
          this.findingForm.reset();
          this.addingFinding.set(false);
          this.reload(current.id);
        },
        error: (err: unknown) => {
          this.findingError.set(toApiError(err));
          this.addingFinding.set(false);
        },
      });
  }

  /*
    Q4: JPG / PNG of at most 5 MB. Wrong files are listed and skipped before anything is sent;
    the others are uploaded one after the other, so one failure does not hide the rest.
  */
  protected uploadPhotos(input: HTMLInputElement): void {
    const current = this.detail();
    const files = Array.from(input.files ?? []);
    input.value = '';
    if (!current || files.length === 0) return;

    const errors = files.map(photoFileError).filter((e): e is string => e !== null);
    const valid = files.filter((f) => photoFileError(f) === null);
    this.photoErrors.set(errors);
    if (valid.length === 0) return;

    const findingId = this.photoFindingId.value || null;
    this.uploading.set(true);
    from(valid)
      .pipe(
        concatMap((file) =>
          this.api.uploadPhoto(current.id, file, findingId).pipe(
            map(() => null),
            catchError((err: unknown) => of(`${file.name}: ${toApiError(err).message}`)),
          ),
        ),
        toArray(),
      )
      .subscribe((results) => {
        const failed = results.filter((r): r is string => r !== null);
        const uploaded = valid.length - failed.length;
        this.photoErrors.set([...errors, ...failed]);
        if (uploaded > 0) this.toast.success(`Đã tải lên ${uploaded} ảnh`);
        this.uploading.set(false);
        this.reload(current.id);
      });
  }

  protected positionText(d: Pick<RepairCaseSummary, 'blockCode' | 'bay' | 'row' | 'tier'>): string {
    return d.blockCode ? formatPosition(d.blockCode, d.bay, d.row, d.tier) : 'Không ở trong bãi';
  }

  // Which finding a photo belongs to, as short text under the thumbnail
  protected findingText(findingId: string | null): string {
    if (!findingId) return 'Cả hồ sơ';
    const finding = this.detail()?.findings.find((f) => f.id === findingId);
    return finding ? finding.description : '';
  }

  protected repairNeededText(d: RepairCaseDetail): string {
    if (d.repairNeeded === null) return 'Chưa giám định';
    return d.repairNeeded ? 'Cần sửa' : 'Không cần sửa';
  }

  private openCase(id: string | null): void {
    this.selectedId.set(id);
    this.actionForm.reset();
    this.findingForm.reset();
    this.photoFindingId.setValue('');
    this.actionError.set(null);
    this.findingError.set(null);
    this.noteMissing.set(false);
    this.photoErrors.set([]);
    if (!id) {
      this.detail.set(null);
      this.revokePhotos();
      return;
    }
    this.reload(id);
  }

  // Reloads the case and the list row (its status, counts and latest finding changed)
  private reload(id: string): void {
    this.detailError.set(null);
    this.api.get(id).subscribe({
      next: (detail) => {
        // Ignore an answer for a case the user has already left
        if (this.selectedId() !== id) return;
        this.detail.set(detail);
        this.loadPhotos(detail);
      },
      error: (err: unknown) => {
        this.detail.set(null);
        this.detailError.set(toApiError(err).message);
      },
    });
    this.loadPage(this.page());
  }

  // Keeps the URLs of photos already loaded, fetches the new ones and frees the ones gone
  private loadPhotos(detail: RepairCaseDetail): void {
    const known = this.photoUrls();
    const keep: Record<string, string> = {};
    for (const photo of detail.photos) {
      if (known[photo.id]) keep[photo.id] = known[photo.id];
    }
    for (const [id, url] of Object.entries(known)) {
      if (!keep[id]) URL.revokeObjectURL(url);
    }
    this.photoUrls.set(keep);

    for (const photo of detail.photos.filter((p) => !keep[p.id])) {
      this.api.photo(photo.id).subscribe({
        next: (blob) => {
          // The case may have changed while the photo was loading
          if (this.selectedId() !== detail.id) return;
          this.photoUrls.update((urls) => ({ ...urls, [photo.id]: URL.createObjectURL(blob) }));
        },
        error: () => undefined,
      });
    }
  }

  private revokePhotos(): void {
    Object.values(this.photoUrls()).forEach((url) => URL.revokeObjectURL(url));
    this.photoUrls.set({});
  }

  private currentFilter(): RepairCaseFilter {
    const v = this.filters.getRawValue();
    return {
      search: v.search,
      open: v.status === 'open' ? true : null,
      status: v.status === 'open' || v.status === 'all' ? null : v.status,
      operatorId: v.operatorId || null,
      reportedFrom: v.reportedFrom || null,
      reportedTo: v.reportedTo || null,
    };
  }

  private loadPage(page: number): void {
    this.api.list(page, PAGE_SIZE, this.currentFilter()).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.listError.set(null);
      },
      error: (err: unknown) => this.listError.set(toApiError(err).message),
    });
  }
}
