import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  PagedResult,
  RecordedDamage,
  RepairAction,
  RepairCaseDetail,
  RepairCaseFilter,
  RepairCaseSummary,
  RepairPhoto,
  ReportDamageRequest,
} from './depot-models';

// Inspection & repair cases (feedback 4): /api/repair-cases and /api/repair-photos
@Service()
export class RepairApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/repair-cases';

  // Newest report first; empty filters are left out so the API applies no condition for them
  list(page: number, pageSize: number, filter: RepairCaseFilter = {}): Observable<PagedResult<RepairCaseSummary>> {
    const params: Record<string, string | number | boolean> = { page, pageSize };
    if (filter.search?.trim()) params['search'] = filter.search.trim();
    if (filter.status) params['status'] = filter.status;
    if (filter.open !== null && filter.open !== undefined) params['open'] = filter.open;
    if (filter.operatorId) params['operatorId'] = filter.operatorId;
    if (filter.containerId) params['containerId'] = filter.containerId;
    if (filter.reportedFrom) params['reportedFrom'] = filter.reportedFrom;
    if (filter.reportedTo) params['reportedTo'] = filter.reportedTo;
    return this.http.get<PagedResult<RepairCaseSummary>>(this.baseUrl, { params });
  }

  get(id: string): Observable<RepairCaseDetail> {
    return this.http.get<RepairCaseDetail>(`${this.baseUrl}/${id}`);
  }

  // Goes into the open case of the container, or opens a new one (created = true)
  report(request: ReportDamageRequest): Observable<RecordedDamage> {
    return this.http.post<RecordedDamage>(`${this.baseUrl}/report`, request);
  }

  // 409 RepairCase.InvalidTransition when the case moved meanwhile; 400 RepairCase.ReasonRequired for AcceptRejected
  act(id: string, action: RepairAction, note: string | null): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/actions`, { action, note });
  }

  // multipart/form-data: the browser sets the boundary, so no Content-Type header is given here
  uploadPhoto(caseId: string, file: File, findingId?: string | null): Observable<RepairPhoto> {
    const body = new FormData();
    body.append('file', file, file.name);
    if (findingId) body.append('findingId', findingId);
    return this.http.post<RepairPhoto>(`${this.baseUrl}/${caseId}/photos`, body);
  }

  /*
    The photo as a Blob: an <img src="/api/..."> cannot send the bearer token, so the page fetches
    the bytes through HttpClient (with the auth interceptor) and shows them with an object URL.
  */
  photo(id: string): Observable<Blob> {
    return this.http.get(`/api/repair-photos/${id}`, { responseType: 'blob' });
  }
}
