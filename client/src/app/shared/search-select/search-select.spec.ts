import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Observable, of } from 'rxjs';
import { PagedResult } from '../../core/depot/depot-models';
import { SearchSelect, mergePage, moveActive, nearBottom } from './search-select';

interface Row {
  id: string;
  name: string;
}

// PagedResult as the API returns it, for one page of `items`
function pageOf(items: Row[], page: number, totalCount: number, pageSize = 2): PagedResult<Row> {
  const totalPages = Math.ceil(totalCount / pageSize);
  return {
    items,
    totalCount,
    page,
    pageSize,
    totalPages,
    hasNextPage: page < totalPages,
    hasPreviousPage: page > 1,
  };
}

describe('search select helpers', () => {
  it('moves the highlight inside the list without wrapping', () => {
    expect(moveActive(-1, 1, 3)).toBe(0);
    expect(moveActive(2, 1, 3)).toBe(2);
    expect(moveActive(0, -1, 3)).toBe(0);
    expect(moveActive(-1, 1, 0)).toBe(-1);
  });

  it('loads more only near the end of the list', () => {
    expect(nearBottom(0, 240, 480)).toBe(false);
    expect(nearBottom(220, 240, 480)).toBe(true);
  });

  it('replaces the list on page 1 and appends later pages without duplicates', () => {
    const a = { id: 'a', name: 'A' };
    const b = { id: 'b', name: 'B' };
    const c = { id: 'c', name: 'C' };
    const key = (row: Row) => row.id;

    expect(mergePage([a, b], pageOf([c], 1, 1), key)).toEqual([c]);
    expect(mergePage([a, b], pageOf([b, c], 2, 4), key)).toEqual([a, b, c]);
  });
});

// Host page: a form control bound to the dropdown, and a filter that can change
@Component({
  imports: [ReactiveFormsModule, SearchSelect],
  template: `
    <app-search-select
      inputId="pick"
      [formControl]="control"
      [load]="load"
      [itemKey]="key"
      [itemLabel]="label"
      [resetKey]="filter()"
    />
  `,
})
class Host {
  readonly control = new FormControl<Row | null>(null);
  readonly filter = signal('MSC');
  readonly calls: { term: string; page: number }[] = [];
  readonly rows: Row[] = [
    { id: '1', name: 'MSCU1234566' },
    { id: '2', name: 'MSCU5551234' },
  ];
  readonly load = (term: string, page: number): Observable<PagedResult<Row>> => {
    this.calls.push({ term, page });
    return of(
      pageOf(
        this.rows.filter((r) => r.name.includes(term.toUpperCase())),
        page,
        this.rows.length,
      ),
    );
  };
  readonly key = (row: Row) => row.id;
  readonly label = (row: Row) => row.name;
}

describe('SearchSelect', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  // Flushes timers, effects and rendering (fake timers: whenStable would wait forever)
  async function settle(fixture: { detectChanges(): void }) {
    await vi.advanceTimersByTimeAsync(0);
    fixture.detectChanges();
  }

  async function setup() {
    const fixture = TestBed.createComponent(Host);
    await settle(fixture);
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    return { fixture, host: fixture.componentInstance, input };
  }

  async function type(fixture: { detectChanges(): void }, input: HTMLInputElement, text: string) {
    input.value = text;
    input.dispatchEvent(new Event('input'));
    // Past the typing debounce
    await vi.advanceTimersByTimeAsync(300);
    fixture.detectChanges();
  }

  function press(input: HTMLInputElement, key: string) {
    input.dispatchEvent(new KeyboardEvent('keydown', { key }));
  }

  it('waits for a pause in typing, then searches page 1 of the typed text', async () => {
    const { fixture, host, input } = await setup();

    input.value = 'ms';
    input.dispatchEvent(new Event('input'));
    await type(fixture, input, 'mscu5');

    expect(host.calls).toEqual([{ term: 'mscu5', page: 1 }]);
    expect(fixture.nativeElement.querySelectorAll('[role=option]').length).toBe(1);
  });

  it('never picks a row by itself: only Enter on a highlighted row selects', async () => {
    const { fixture, host, input } = await setup();
    await type(fixture, input, 'MSCU5551234');

    // Exact text but nothing highlighted: Enter keeps the form value empty
    press(input, 'Enter');
    expect(host.control.value).toBeNull();

    press(input, 'ArrowDown');
    press(input, 'Enter');
    expect(host.control.value?.id).toBe('2');
  });

  it('drops the selection as soon as the text changes', async () => {
    const { fixture, host, input } = await setup();
    host.control.setValue(host.rows[0]);
    await settle(fixture);
    expect(input.value).toBe('MSCU1234566');

    await type(fixture, input, 'MSCU123456');

    expect(host.control.value).toBeNull();
  });

  it('clears the selection and the old results when the filter changes', async () => {
    const { fixture, host, input } = await setup();
    host.control.setValue(host.rows[0]);
    await settle(fixture);

    host.filter.set('CMA');
    await settle(fixture);

    expect(host.control.value).toBeNull();
    expect(input.value).toBe('');
  });
});
