import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  EstimateDecisionRequest,
  ExternalRepair,
  ExternalRepairStatus,
  LinePayerRequest,
  PagedResult,
  RecordedDamage,
  RepairAction,
  RepairCaseDetail,
  RepairCaseFilter,
  RepairCaseSummary,
  RepairPhoto,
  ReportDamageRequest,
  RequestExternalRepairRequest,
  UpdateEstimateRequest,
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
    if (filter.external !== null && filter.external !== undefined) params['external'] = filter.external;
    if (filter.facility?.trim()) params['facility'] = filter.facility.trim();
    if (filter.payer) params['payer'] = filter.payer;
    if (filter.payerStatus) params['payerStatus'] = filter.payerStatus;
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

  // Returns the transfer id; 409 ExternalRepair.CaseNotApproved / AlreadyActive / NotInYard
  requestExternal(caseId: string, request: RequestExternalRepairRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/${caseId}/external-repair`, request);
  }

  // Newest request first; the gate-out page asks for status Requested (waiting to leave)
  externalRepairs(status: ExternalRepairStatus | null, search = '', page = 1, pageSize = 20): Observable<PagedResult<ExternalRepair>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (status) params['status'] = status;
    if (search.trim()) params['search'] = search.trim();
    return this.http.get<PagedResult<ExternalRepair>>('/api/external-repairs', { params });
  }

  // Only before the container leaves: 409 ExternalRepair.NotRequested otherwise
  cancelExternal(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`/api/external-repairs/${id}/cancel`, { reason });
  }

  // ---------- repair costs (feedback 4) ----------

  /*
    New estimate version (draft) of a case; a later version copies the items of the previous one.
    409 RepairEstimate.NotInspected / OpenVersion, RepairCase.Closed
  */
  createEstimate(caseId: string, currency: string, note: string | null, evidence: string | null): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.baseUrl}/${caseId}/estimates`, { currency, note, evidence });
  }

  // Saves a draft (header + the whole item table); 409 RepairEstimate.NotDraft once submitted
  updateEstimate(estimateId: string, request: UpdateEstimateRequest): Observable<void> {
    return this.http.put<void>(`/api/repair-estimates/${estimateId}`, request);
  }

  // 400 RepairEstimate.NoLines for an empty draft
  submitEstimate(estimateId: string): Observable<void> {
    return this.http.post<void>(`/api/repair-estimates/${estimateId}/submit`, {});
  }

  // Answer of the approving party; approving the first version approves the repair
  decideEstimate(estimateId: string, request: EstimateDecisionRequest): Observable<void> {
    return this.http.post<void>(`/api/repair-estimates/${estimateId}/decision`, request);
  }

  // Who pays one item; allowed in every status (amounts do not change)
  setLinePayer(estimateId: string, lineId: string, request: LinePayerRequest): Observable<void> {
    return this.http.put<void>(`/api/repair-estimates/${estimateId}/lines/${lineId}/payer`, request);
  }

  /*
    The photo as a Blob: an <img src="/api/..."> cannot send the bearer token, so the page fetches
    the bytes through HttpClient (with the auth interceptor) and shows them with an object URL.
  */
  photo(id: string): Observable<Blob> {
    return this.http.get(`/api/repair-photos/${id}`, { responseType: 'blob' });
  }
}
