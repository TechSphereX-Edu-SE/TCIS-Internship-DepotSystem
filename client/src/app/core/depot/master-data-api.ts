import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { Block, Container, LineOperator, PagedResult } from './depot-models';

// Master data used by the business screens (dropdowns, container lookup)
@Service()
export class MasterDataApi {
  private readonly http = inject(HttpClient);

  // A depot has a handful of operators and blocks, so one large page is enough for a dropdown
  lineOperators(): Observable<LineOperator[]> {
    return this.http
      .get<PagedResult<LineOperator>>('/api/line-operators', { params: { pageSize: 100 } })
      .pipe(map((page) => [...page.items].sort((a, b) => a.code.localeCompare(b.code))));
  }

  blocks(): Observable<Block[]> {
    return this.http
      .get<PagedResult<Block>>('/api/blocks', { params: { pageSize: 100 } })
      .pipe(map((page) => [...page.items].sort((a, b) => a.code.localeCompare(b.code))));
  }

  // Exact match on a full container number, using the API's ?search= (part of the number)
  findContainer(containerNumber: string): Observable<Container | null> {
    return this.http
      .get<PagedResult<Container>>('/api/containers', { params: { search: containerNumber, pageSize: 5 } })
      .pipe(map((page) => page.items.find((c) => c.containerNumber === containerNumber) ?? null));
  }
}
