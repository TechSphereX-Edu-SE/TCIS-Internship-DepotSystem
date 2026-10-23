import { HttpClient, HttpParams } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ContainerPosition,
  DailyVisits,
  GateInRequest,
  GateInResult,
  GateOutCandidate,
  GateOutForRepairRequest,
  GateOutForRepairResult,
  GateOutRequest,
  GateOutResult,
  MoveRequest,
  PagedResult,
  YardContainer,
} from './depot-models';

// Container lifecycle in the yard: /api/container-visits
@Service()
export class YardApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/container-visits';

  gateIn(request: GateInRequest): Observable<GateInResult> {
    return this.http.post<GateInResult>(`${this.baseUrl}/gate-in`, request);
  }

  // Returns the new position; 409 ContainerAbove / SlotOccupied / NoSupportBelow, 400 SamePosition
  move(request: MoveRequest): Observable<ContainerPosition> {
    return this.http.post<ContainerPosition>(`${this.baseUrl}/move`, request);
  }

  // 409 RepairCaseOpen / ContainerAbove / WrongLineOperator / Cancelled / Expired / TypeNotAllowed / QuantityExceeded
  gateOut(request: GateOutRequest): Observable<GateOutResult> {
    return this.http.post<GateOutResult>(`${this.baseUrl}/gate-out`, request);
  }

  // Exit of a container requested for outside repair: no order; 409 ExternalRepair.NotRequested / ContainerAbove
  gateOutForRepair(request: GateOutForRepairRequest): Observable<GateOutForRepairResult> {
    return this.http.post<GateOutForRepairResult>(`${this.baseUrl}/gate-out-repair`, request);
  }

  /*
    Containers that match the order (operator, type, size still open): those that can leave first.
    409 DeliveryOrder.Cancelled / Expired when the order can no longer be used.
  */
  gateOutCandidates(deliveryOrderId: string, search: string, page = 1, pageSize = 20): Observable<PagedResult<GateOutCandidate>> {
    const params: Record<string, string | number> = { deliveryOrderId, page, pageSize };
    if (search.trim()) params['search'] = search.trim();
    return this.http.get<PagedResult<GateOutCandidate>>(`${this.baseUrl}/gate-out-candidates`, { params });
  }

  // 404 ContainerVisit.NotInYard when the container exists but is not in the yard
  position(containerNumber: string): Observable<ContainerPosition> {
    return this.http.get<ContainerPosition>(`${this.baseUrl}/position`, { params: { containerNumber } });
  }

  // Both filters optional: one row of one block for the yard map
  inYard(blockId?: string, row?: number): Observable<YardContainer[]> {
    let params = new HttpParams();
    if (blockId) params = params.set('blockId', blockId);
    if (row) params = params.set('row', row);
    return this.http.get<YardContainer[]>(`${this.baseUrl}/in-yard`, { params });
  }

  // Without a date the API uses today in Vietnam
  daily(date?: string): Observable<DailyVisits> {
    return this.http.get<DailyVisits>(`${this.baseUrl}/daily`, { params: date ? { date } : {} });
  }
}
