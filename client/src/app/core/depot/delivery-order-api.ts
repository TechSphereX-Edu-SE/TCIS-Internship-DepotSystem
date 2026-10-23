import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateDeliveryOrderRequest, CreateDeliveryOrderResult, DeliveryOrder, DeliveryOrderFilter, PagedResult } from './depot-models';

// Delivery orders of line operators: /api/delivery-orders (newest first, or best number match first when searching)
@Service()
export class DeliveryOrderApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/delivery-orders';

  // Empty filters are left out so the API applies no condition for them
  list(page = 1, pageSize = 20, filter: DeliveryOrderFilter = {}): Observable<PagedResult<DeliveryOrder>> {
    const params: Record<string, string | number | boolean> = { page, pageSize };
    if (filter.search?.trim()) params['search'] = filter.search.trim();
    if (filter.status) params['status'] = filter.status;
    if (filter.usable) params['usable'] = true;
    if (filter.operatorId) params['operatorId'] = filter.operatorId;
    return this.http.get<PagedResult<DeliveryOrder>>(this.baseUrl, { params });
  }

  get(id: string): Observable<DeliveryOrder> {
    return this.http.get<DeliveryOrder>(`${this.baseUrl}/${id}`);
  }

  // 400 AlreadyExpired / DuplicateLine, 404 LineOperator / Customer, 409 Duplicate (order number)
  create(request: CreateDeliveryOrderRequest): Observable<CreateDeliveryOrderResult> {
    return this.http.post<CreateDeliveryOrderResult>(this.baseUrl, request);
  }

  // Q7: 409 HasReleases once a container left under the order, 409 AlreadyCancelled
  cancel(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/cancel`, { reason });
  }
}
