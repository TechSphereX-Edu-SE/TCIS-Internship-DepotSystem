import { HttpClient, HttpParams } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ContainerPosition,
  DailyVisits,
  GateInRequest,
  GateInResult,
  GateOutRequest,
  GateOutResult,
  MoveRequest,
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

  // 409 ContainerDamaged / ContainerAbove / WrongLineOperator / Expired / TypeNotAllowed / QuantityExceeded
  gateOut(request: GateOutRequest): Observable<GateOutResult> {
    return this.http.post<GateOutResult>(`${this.baseUrl}/gate-out`, request);
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
