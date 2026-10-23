import { Component, input, output } from '@angular/core';

/*
  Rows per page of every paged list (feedback 23/10: "trang nào có list hiển thị thì hiện 15").
  One constant so all lists stay the same; search dropdowns keep their own page size for scrolling.
*/
export const LIST_PAGE_SIZE = 15;

/*
  "Trang trước · Trang x / y · Trang sau" under a paged table (PagedResult of the API).
  Hidden when everything fits on one page. The page decides what to load; this only emits the page wanted.
*/
@Component({
  selector: 'app-pager',
  template: `
    @if (totalPages() > 1) {
      <nav class="pager" [attr.aria-label]="label()">
        <button class="btn btn-secondary btn-small" type="button" [disabled]="page() <= 1" (click)="pageChange.emit(page() - 1)">
          Trang trước
        </button>
        <span class="muted">Trang {{ page() }} / {{ totalPages() }}</span>
        <button
          class="btn btn-secondary btn-small"
          type="button"
          [disabled]="page() >= totalPages()"
          (click)="pageChange.emit(page() + 1)"
        >
          Trang sau
        </button>
      </nav>
    }
  `,
  styles: `
    .pager {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 12px;
      font-size: 13px;
    }
  `,
})
export class Pager {
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  // Names the list for screen readers, e.g. "Phân trang danh sách lệnh"
  readonly label = input('Phân trang');
  readonly pageChange = output<number>();
}
