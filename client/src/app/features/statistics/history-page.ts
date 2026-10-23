import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { toApiError } from '../../core/api/api-error';
import {
  CATEGORIES,
  CATEGORY_LABEL,
  CONDITION_LABEL,
  CONTAINER_PRESENCE_LABEL,
  ContainerCategory,
  ContainerSize,
  ContainerVisitSummary,
  GATE_OUT_PURPOSES,
  GATE_OUT_PURPOSE_LABEL,
  GateOutPurpose,
  LineOperator,
  REPAIR_STATUS_LABEL,
  SIZES,
  SIZE_LABEL,
  VISIT_DOCUMENT_KIND_LABEL,
  VISIT_HISTORY_EVENT_LABEL,
  VISIT_STATE_LABEL,
  VisitHistoryDetail,
  VisitHistoryEvent,
  VisitHistoryFilter,
  VisitHistoryRow,
  VisitPosition,
  VisitState,
  exitText,
  formatPosition,
  historyFilterFromParams,
  historyLinkParams,
} from '../../core/depot/depot-models';
import { MasterDataApi } from '../../core/depot/master-data-api';
import { YardApi } from '../../core/depot/yard-api';
import { LIST_PAGE_SIZE, Pager } from '../../shared/pager/pager';

/*
  Feedback 7 + 8: Lịch sử container, one row per visit (open or closed).
    - Main filters: container number, line operator of the visit, period and the event it applies to
      (Nhập = gate-in time, Xuất = gate-out time, both = at least one of them). More: type, size,
      open / closed, exit purpose. The operator is the one of each visit, never today's operator.
    - The URL holds the filters, so reports and the container catalog link here with their filters,
      and reload / back keep them. "Xem lịch sử" of the catalog adds containerId: the container summary
      (visit counts, presence, dwell days) shows above the rows.
    - A row opens its visit: gate-in / gate-out, moves, inspection / repair cases and stored documents.
      Data that was never recorded says so; nothing is rebuilt from today's values.
*/
@Component({
  selector: 'app-history-page',
  imports: [ReactiveFormsModule, DatePipe, RouterLink, Pager],
  templateUrl: './history-page.html',
  styleUrl: './history-page.css',
})
export class HistoryPage {
  private readonly yard = inject(YardApi);
  private readonly masterData = inject(MasterDataApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly categoryLabel = CATEGORY_LABEL;
  protected readonly sizeLabel = SIZE_LABEL;
  protected readonly conditionLabel = CONDITION_LABEL;
  protected readonly purposeLabel = GATE_OUT_PURPOSE_LABEL;
  protected readonly eventLabel = VISIT_HISTORY_EVENT_LABEL;
  protected readonly stateLabel = VISIT_STATE_LABEL;
  protected readonly presenceLabel = CONTAINER_PRESENCE_LABEL;
  protected readonly repairStatusLabel = REPAIR_STATUS_LABEL;
  protected readonly documentLabel = VISIT_DOCUMENT_KIND_LABEL;
  protected readonly categories = CATEGORIES;
  protected readonly sizes = SIZES;
  protected readonly purposes = GATE_OUT_PURPOSES;
  protected readonly events: VisitHistoryEvent[] = ['Any', 'GateIn', 'GateOut'];
  protected readonly states: VisitState[] = ['Open', 'Closed'];
  protected readonly exitText = exitText;

  protected readonly filters = inject(FormBuilder).nonNullable.group({
    search: [''],
    operatorId: [''],
    from: [''],
    to: [''],
    event: ['Any' as VisitHistoryEvent],
    category: ['' as ContainerCategory | ''],
    size: ['' as ContainerSize | ''],
    state: ['' as VisitState | ''],
    purpose: ['' as GateOutPurpose | ''],
  });
  protected readonly moreFilters = signal(false);

  protected readonly operators = signal<LineOperator[]>([]);
  protected readonly rows = signal<VisitHistoryRow[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly totalPages = signal(1);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  // Container picked from the catalog (?containerId=): its summary above the rows
  protected readonly containerId = signal<string | null>(null);
  protected readonly summary = signal<ContainerVisitSummary | null>(null);

  // One visit opened under its row
  protected readonly openVisitId = signal<string | null>(null);
  protected readonly visit = signal<VisitHistoryDetail | null>(null);
  protected readonly visitError = signal<string | null>(null);

  // Filters of the rows on screen (from the URL), so paging never picks up half-edited fields
  private applied: VisitHistoryFilter = {};

  constructor() {
    this.masterData.lineOperators('Operator').subscribe({
      next: (operators) => this.operators.set(operators),
      error: (err: unknown) => this.error.set(toApiError(err).message),
    });

    // The URL is the source of the filters: links, reload and back all land here
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const filter = historyFilterFromParams((key) => params.get(key));
      this.applied = filter;
      this.fillForm(filter);
      if (filter.containerId !== this.containerId()) this.loadSummary(filter.containerId ?? null);
      this.loadPage(1);
    });
  }

  // Puts the form into the URL; the URL subscription then loads the rows
  protected applyFilters(): void {
    const f = this.filters.getRawValue();
    const containerId = this.containerId();
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: historyLinkParams({
        // The container filter stays while its number is still the search text
        containerId: containerId && f.search.trim().toUpperCase() === this.summary()?.containerNumber ? containerId : null,
        search: f.search,
        operatorId: f.operatorId,
        from: f.from,
        to: f.to,
        event: f.event === 'Any' ? null : f.event,
        category: f.category || null,
        size: f.size || null,
        state: f.state || null,
        purpose: f.purpose || null,
      }),
    });
  }

  // With no filter in the URL already, only the half-edited form needs clearing
  protected clearFilters(): void {
    this.fillForm({});
    void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  protected goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) this.loadPage(page);
  }

  protected toggleVisit(row: VisitHistoryRow): void {
    if (this.openVisitId() === row.visitId) {
      this.openVisitId.set(null);
      return;
    }
    this.openVisitId.set(row.visitId);
    this.visit.set(null);
    this.visitError.set(null);
    this.yard.visitHistory(row.visitId).subscribe({
      // Ignore a late answer for a visit that is no longer open
      next: (detail) => {
        if (this.openVisitId() === row.visitId) this.visit.set(detail);
      },
      error: (err: unknown) => this.visitError.set(toApiError(err).message),
    });
  }

  // "A 04-1-1" or "WAIT (ảo)"
  protected positionText(position: VisitPosition): string {
    const text = formatPosition(position.blockCode, position.bay, position.row, position.tier);
    return position.isVirtual ? `${text} (ảo)` : text;
  }

  private loadPage(page: number): void {
    this.loading.set(true);
    this.yard.history(page, LIST_PAGE_SIZE, this.applied).subscribe({
      next: (result) => {
        this.rows.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.totalPages.set(Math.max(1, result.totalPages));
        this.error.set(null);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.error.set(toApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  private loadSummary(containerId: string | null): void {
    this.containerId.set(containerId);
    this.summary.set(null);
    if (!containerId) return;
    this.yard.containerSummary(containerId).subscribe({
      next: (summary) => this.summary.set(summary),
      error: (err: unknown) => this.error.set(toApiError(err).message),
    });
  }

  private fillForm(filter: VisitHistoryFilter): void {
    this.filters.setValue({
      search: filter.search ?? '',
      operatorId: filter.operatorId ?? '',
      from: filter.from ?? '',
      to: filter.to ?? '',
      event: filter.event ?? 'Any',
      category: filter.category ?? '',
      size: filter.size ?? '',
      state: filter.state ?? '',
      purpose: filter.purpose ?? '',
    });
    // Filters of "Lọc thêm" that came with a link stay visible, so the user sees why rows are missing
    if (filter.category || filter.size || filter.state || filter.purpose) this.moreFilters.set(true);
  }
}
