import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PagedResult,
  Throughput,
  ThroughputEventKind,
  ThroughputFilter,
  VisitHistoryRow,
  YardInventory,
  YardInventoryFilter,
  compactParams,
} from './depot-models';

// Depot statistics: /api/statistics (all dates are Vietnam dates "yyyy-MM-dd")
@Service()
export class StatisticsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/statistics';

  /*
    Containers in the yard per line operator, split at longStayDays (1..365, API default 10).
    The filter is the same one the detail rows use (YardApi.search), so totals and rows agree.
  */
  yardInventory(longStayDays: number, filter: YardInventoryFilter = {}): Observable<YardInventory> {
    const params = compactParams({ longStayDays, ...filter });
    return this.http.get<YardInventory>(`${this.baseUrl}/yard-inventory`, { params });
  }

  // Gate-ins and gate-outs per day and line operator; every filter is optional except the dates
  throughput(from: string, to: string, filter: ThroughputFilter = {}): Observable<Throughput> {
    return this.http.get<Throughput>(`${this.baseUrl}/throughput`, { params: compactParams({ from, to, ...filter }) });
  }

  // Rows behind the throughput totals (one tab per kind), same filters, newest event first
  throughputEvents(
    kind: ThroughputEventKind,
    from: string,
    to: string,
    filter: ThroughputFilter,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<VisitHistoryRow>> {
    const params = compactParams({ kind, from, to, ...filter, page, pageSize });
    return this.http.get<PagedResult<VisitHistoryRow>>(`${this.baseUrl}/throughput/events`, { params });
  }
}
