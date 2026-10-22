import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateDeliveryOrderRequest, CreateDeliveryOrderResult, DeliveryOrder, PagedResult } from './depot-models';

// Delivery orders of line operators: /api/delivery-orders (newest first)
@Service()
export class DeliveryOrderApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/delivery-orders';

  list(page = 1, pageSize = 20): Observable<PagedResult<DeliveryOrder>> {
    return this.http.get<PagedResult<DeliveryOrder>>(this.baseUrl, { params: { page, pageSize } });
  }

  // 400 AlreadyExpired / DuplicateLine, 404 LineOperator / Customer, 409 Duplicate (order number)
  create(request: CreateDeliveryOrderRequest): Observable<CreateDeliveryOrderResult> {
    return this.http.post<CreateDeliveryOrderResult>(this.baseUrl, request);
  }
}
