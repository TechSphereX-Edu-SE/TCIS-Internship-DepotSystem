import { Subject, of } from 'rxjs';
import { PagedResult } from '../../core/depot/depot-models';
import { PagedDetails } from './paged-details';

function pageOf(items: string[], page = 1, totalPages = 1): PagedResult<string> {
  return { items, totalCount: items.length, page, pageSize: 20, totalPages, hasNextPage: page < totalPages, hasPreviousPage: page > 1 };
}

describe('PagedDetails', () => {
  it('loads a key only when it is opened, and once', () => {
    const calls: string[] = [];
    const details = new PagedDetails<string>((key, page) => {
      calls.push(`${key}:${page}`);
      return of(pageOf([`${key}-row`]));
    });

    expect(details.isOpen('MSC')).toBe(false);
    details.toggle('MSC');
    details.toggle('MSC');
    details.toggle('MSC');

    expect(calls).toEqual(['MSC:1']);
    expect(details.isOpen('MSC')).toBe(true);
    expect(details.state('MSC')?.items).toEqual(['MSC-row']);
  });

  it('reloads only the open keys from page 1 after a reset', () => {
    const calls: string[] = [];
    const details = new PagedDetails<string>((key, page) => {
      calls.push(`${key}:${page}`);
      return of(pageOf([key], page, 3));
    });
    details.toggle('MSC');
    details.load('MSC', 2);
    details.toggle('CMA');
    details.toggle('CMA');

    details.reset();

    expect(calls).toEqual(['MSC:1', 'MSC:2', 'CMA:1', 'MSC:1']);
    expect(details.state('MSC')?.page).toBe(1);
    expect(details.state('CMA')).toBeNull();
  });

  it('keeps the error of a failed page and shows loading meanwhile', () => {
    const response = new Subject<PagedResult<string>>();
    const details = new PagedDetails<string>(() => response);

    details.toggle('MSC');
    expect(details.state('MSC')?.loading).toBe(true);
    response.error(new Error('offline'));

    expect(details.state('MSC')?.loading).toBe(false);
    expect(details.state('MSC')?.error).toBeTruthy();
  });
});
