import { Signal, computed, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { toApiError } from '../../core/api/api-error';
import { PagedResult } from '../../core/depot/depot-models';

// One opened detail table: its current page of rows, or why it could not load
export interface DetailState<T> {
  page: number;
  totalPages: number;
  totalCount: number;
  items: T[];
  loading: boolean;
  error: string | null;
}

/*
  Rows opened under a report line with the ▸ arrow (feedback 7: "tải khi mở, phân trang").
  Each key (a line operator, or a line operator + tab) has its own page; nothing is loaded until the
  key is opened. The loader decides the filters, so a report passes the same applied filters as its totals.
  Shared by the yard inventory and throughput pages so both behave the same way.
*/
export class PagedDetails<T> {
  private readonly states = signal<Record<string, DetailState<T>>>({});
  private readonly opened = signal<ReadonlySet<string>>(new Set());

  // Keys currently open, so a page can reload them after its filters change
  readonly openKeys: Signal<string[]> = computed(() => [...this.opened()]);

  constructor(private readonly loader: (key: string, page: number) => Observable<PagedResult<T>>) {}

  isOpen(key: string): boolean {
    return this.opened().has(key);
  }

  state(key: string): DetailState<T> | null {
    return this.states()[key] ?? null;
  }

  // Opens and loads page 1 the first time; closing keeps nothing open but forgets nothing loaded
  toggle(key: string): void {
    const opened = new Set(this.opened());
    if (opened.delete(key)) {
      this.opened.set(opened);
      return;
    }
    opened.add(key);
    this.opened.set(opened);
    if (!this.states()[key]) this.load(key, 1);
  }

  // Opens a key without closing it when it is already open (e.g. switching to another tab)
  open(key: string): void {
    if (!this.isOpen(key)) this.toggle(key);
  }

  load(key: string, page: number): void {
    this.patch(key, { loading: true, error: null });
    this.loader(key, page).subscribe({
      next: (result) =>
        this.patch(key, {
          page: result.page,
          totalPages: Math.max(1, result.totalPages),
          totalCount: result.totalCount,
          items: result.items,
          loading: false,
        }),
      error: (err: unknown) => this.patch(key, { loading: false, error: toApiError(err).message }),
    });
  }

  /*
    After the report reloads with other filters: rows loaded before are dropped (they belong to the old
    filters) and the keys still open load page 1 again, so totals and rows always match.
  */
  reset(): void {
    this.states.set({});
    for (const key of this.opened()) this.load(key, 1);
  }

  // A key loaded for the first time starts empty; later changes keep the rows until new ones arrive
  private patch(key: string, change: Partial<DetailState<T>>): void {
    const empty: DetailState<T> = { page: 1, totalPages: 1, totalCount: 0, items: [], loading: false, error: null };
    this.states.update((all) => ({ ...all, [key]: { ...empty, ...all[key], ...change } }));
  }
}
