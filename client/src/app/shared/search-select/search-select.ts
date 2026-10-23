import {
  Component,
  DestroyRef,
  ElementRef,
  effect,
  forwardRef,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { Observable, Subject, catchError, debounceTime, map, merge, of, switchMap } from 'rxjs';
import { PagedResult, StatusBadge } from '../../core/depot/depot-models';

// Loads one page of matches for the typed text; the API ranks exact match, then prefix, then the rest
export type SearchPageLoader<T> = (term: string, page: number) => Observable<PagedResult<T>>;

// Rows visible before the list scrolls (feedback 1: "khoảng 5 dòng, cuộn bên trong")
export const VISIBLE_ROWS = 5;

// Distance from the bottom of the list (px) at which the next page starts loading
const LOAD_MORE_THRESHOLD = 24;

// Wait after the last key press before asking the API, so typing "MSCU12" sends one request, not six
const TYPING_DEBOUNCE_MS = 250;

/*
  Moves the highlighted row with the arrow keys and stays inside the list (no wrap-around,
  so holding the key does not jump from the last row back to the first). -1 = nothing highlighted.
*/
export function moveActive(current: number, delta: 1 | -1, count: number): number {
  if (count === 0) return -1;
  return Math.min(count - 1, Math.max(0, current + delta));
}

// True when the list is scrolled close enough to its end to load the next page
export function nearBottom(scrollTop: number, clientHeight: number, scrollHeight: number): boolean {
  return scrollTop + clientHeight >= scrollHeight - LOAD_MORE_THRESHOLD;
}

/*
  Page 1 replaces the list (new search). Later pages are appended, skipping rows already shown:
  a row added between two requests shifts the pages, so the same row can come back twice.
*/
export function mergePage<T>(
  current: readonly T[],
  page: PagedResult<T>,
  key: (item: T) => string,
): T[] {
  if (page.page <= 1) return [...page.items];
  const seen = new Set(current.map(key));
  return [...current, ...page.items.filter((item) => !seen.has(key(item)))];
}

type Request = { term: string; page: number };

/*
  Searchable dropdown shared by the business screens (feedback 1, general rules):
    - type to filter; the API decides the order (exact match, then prefix)
    - about 5 rows visible, scrolling inside the list; the next page loads near the end of the list
    - nothing is ever picked automatically: only a click or Enter on a highlighted row selects,
      and typing after a selection clears it, so a half-typed or "close enough" text is never kept
    - resetKey: when the page changes a filter (e.g. the line operator), the old selection and the old
      results are dropped, because they may not match the new filter any more
  It is a form control (ControlValueAccessor): the form value is the selected item, or null.
  The page still validates on submit and the backend checks again; the dropdown only helps to pick.
*/
@Component({
  selector: 'app-search-select',
  templateUrl: './search-select.html',
  styleUrl: './search-select.css',
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => SearchSelect), multi: true },
  ],
  host: { class: 'search-select' },
})
export class SearchSelect<T> implements ControlValueAccessor {
  readonly load = input.required<SearchPageLoader<T>>();
  // Unique key of a row (usually the id), used to track rows and avoid duplicates between pages
  readonly itemKey = input.required<(item: T) => string>();
  // Text shown in the box once a row is selected, and as the main line of each row
  readonly itemLabel = input.required<(item: T) => string>();
  // Optional second line of each row (e.g. "40ft · Dry · MSC")
  readonly itemDetail = input<(item: T) => string>(() => '');
  /*
    Optional state of each row (feedback 23/10, gate-in): a coloured chip next to the label and a third
    line, so the user sees before picking whether the row can be used (e.g. "Đang trong bãi · Tại A 01-1-1").
    The chip also has text, so the state is not told by colour alone.
  */
  readonly itemBadge = input<(item: T) => StatusBadge | null>(() => null);
  readonly itemNote = input<(item: T) => string>(() => '');
  // id of the <input>, so the page's <label for="..."> points at it
  readonly inputId = input.required<string>();
  readonly placeholder = input('Gõ để tìm…');
  readonly invalid = input(false);
  // Any change drops the selection and the results (see the class comment)
  readonly resetKey = input<unknown>(null);

  protected readonly text = signal('');
  protected readonly selected = signal<T | null>(null);
  protected readonly items = signal<T[]>([]);
  protected readonly open = signal(false);
  protected readonly loading = signal(false);
  protected readonly failed = signal(false);
  protected readonly activeIndex = signal(-1);
  protected readonly disabled = signal(false);
  protected readonly visibleRows = VISIBLE_ROWS;

  private readonly list = viewChild<ElementRef<HTMLElement>>('list');
  private readonly typed$ = new Subject<string>();
  private readonly pageRequests$ = new Subject<Request>();
  private lastPage: PagedResult<T> | null = null;
  private lastTerm: string | null = null;
  private onChange: (value: T | null) => void = () => {};
  private onTouched: () => void = () => {};

  constructor() {
    // Typing waits for a pause; scrolling for more loads at once. switchMap drops a request that is
    // still running when a newer one starts, so results of an old text never replace newer ones.
    merge(
      this.typed$.pipe(
        debounceTime(TYPING_DEBOUNCE_MS),
        map((term) => ({ term, page: 1 })),
      ),
      this.pageRequests$,
    )
      .pipe(
        switchMap((request) => {
          this.loading.set(true);
          this.failed.set(false);
          return this.load()(request.term, request.page).pipe(
            map((page) => ({ request, page })),
            catchError(() => of({ request, page: null })),
          );
        }),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe(({ request, page }) => this.applyPage(request, page));

    // A filter of the page changed: drop the selection and the old results (skip the first run)
    let firstRun = true;
    effect(() => {
      this.resetKey();
      if (firstRun) {
        firstRun = false;
        return;
      }
      untracked(() => this.clear());
    });
  }

  // ---------- ControlValueAccessor ----------

  writeValue(value: T | null): void {
    this.selected.set(value);
    this.text.set(value ? this.itemLabel()(value) : '');
    this.resetResults();
  }

  registerOnChange(fn: (value: T | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.disabled.set(disabled);
    if (disabled) this.open.set(false);
  }

  // ---------- template events ----------

  protected onFocus(): void {
    this.open.set(true);
    // First focus (or after a reset): show the first page so the user can also just scroll and pick
    if (this.lastTerm === null) this.search(this.text().trim(), true);
  }

  protected onInput(value: string): void {
    this.text.set(value);
    this.open.set(true);
    this.activeIndex.set(-1);
    // The text no longer is the selected row: drop the selection instead of keeping a mismatch
    if (this.selected() !== null) this.setSelected(null);
    this.search(value.trim(), false);
  }

  protected onKeydown(event: KeyboardEvent): void {
    switch (event.key) {
      case 'ArrowDown':
      case 'ArrowUp':
        event.preventDefault();
        this.open.set(true);
        this.activeIndex.set(
          moveActive(this.activeIndex(), event.key === 'ArrowDown' ? 1 : -1, this.items().length),
        );
        this.scrollActiveIntoView();
        break;
      case 'Enter': {
        // Only a highlighted row is picked; Enter on plain text does nothing (no "closest match")
        const item = this.open() ? this.items()[this.activeIndex()] : undefined;
        if (item !== undefined) {
          event.preventDefault();
          this.pick(item);
        }
        break;
      }
      case 'Escape':
        if (this.open()) {
          event.preventDefault();
          this.open.set(false);
        }
        break;
    }
  }

  protected onBlur(): void {
    this.open.set(false);
    this.onTouched();
  }

  protected onScroll(list: HTMLElement): void {
    const page = this.lastPage;
    if (!page?.hasNextPage || this.loading()) return;
    if (nearBottom(list.scrollTop, list.clientHeight, list.scrollHeight)) {
      this.pageRequests$.next({ term: this.lastTerm ?? '', page: page.page + 1 });
    }
  }

  // mousedown (not click) so the input does not lose focus and close the list before the pick
  protected onOptionMouseDown(event: MouseEvent, item: T): void {
    event.preventDefault();
    this.pick(item);
  }

  protected optionId(index: number): string {
    return `${this.inputId()}-option-${index}`;
  }

  // ---------- internals ----------

  private pick(item: T): void {
    this.setSelected(item);
    this.text.set(this.itemLabel()(item));
    this.open.set(false);
  }

  private setSelected(item: T | null): void {
    this.selected.set(item);
    this.onChange(item);
  }

  private clear(): void {
    if (this.selected() !== null) this.setSelected(null);
    this.text.set('');
    this.resetResults();
  }

  private resetResults(): void {
    this.items.set([]);
    this.lastPage = null;
    this.lastTerm = null;
    this.activeIndex.set(-1);
  }

  // Page 1 of a new text; immediate on focus, debounced while typing
  private search(term: string, immediate: boolean): void {
    if (immediate) this.pageRequests$.next({ term, page: 1 });
    else this.typed$.next(term);
  }

  private applyPage(request: Request, page: PagedResult<T> | null): void {
    this.loading.set(false);
    if (!page) {
      this.failed.set(true);
      return;
    }
    this.items.set(mergePage(this.items(), page, this.itemKey()));
    this.lastPage = page;
    this.lastTerm = request.term;
    if (request.page <= 1) {
      this.activeIndex.set(-1);
      const list = this.list()?.nativeElement;
      if (list) list.scrollTop = 0;
    }
  }

  // Keeps the highlighted row inside the visible part of the list (the list is the option's offsetParent)
  private scrollActiveIntoView(): void {
    const index = this.activeIndex();
    if (index < 0) return;
    // After Angular has rendered the new highlight
    queueMicrotask(() => {
      const list = this.list()?.nativeElement;
      const option = list?.querySelector<HTMLElement>(`#${CSS.escape(this.optionId(index))}`);
      if (!list || !option) return;
      if (option.offsetTop < list.scrollTop) {
        list.scrollTop = option.offsetTop;
      } else if (option.offsetTop + option.offsetHeight > list.scrollTop + list.clientHeight) {
        list.scrollTop = option.offsetTop + option.offsetHeight - list.clientHeight;
      }
    });
  }
}
