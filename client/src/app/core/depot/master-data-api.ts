import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  Block,
  Container,
  CreateBlockRequest,
  CreateContainerRequest,
  CreateCustomerRequest,
  CreateLineOperatorRequest,
  Customer,
  DeleteBlockResult,
  LineOperator,
  PagedResult,
  ResizeBlockRequest,
  UpdateBlockRequest,
  UpdateConditionRequest,
  UpdateContainerRequest,
} from './depot-models';

// Create responses only carry the new id (plus the key fields, not needed by the screens)
type CreatedResult = { id: string };

// Master data (Danh mục): dropdowns and lookups for the business screens, and the catalog screens' CRUD

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

  // Active blocks only: an archived block is never offered for a new position
  blocks(): Observable<Block[]> {
    return this.http
      .get<PagedResult<Block>>('/api/blocks', { params: { pageSize: 100 } })
      .pipe(map((page) => [...page.items].sort((a, b) => a.code.localeCompare(b.code))));
  }

  // Customers for the delivery order form, sorted by name (a depot works with a limited list)
  customers(): Observable<Customer[]> {
    return this.http
      .get<PagedResult<Customer>>('/api/customers', { params: { pageSize: 100 } })
      .pipe(map((page) => [...page.items].sort((a, b) => a.name.localeCompare(b.name, 'vi'))));
  }

  // E.g. back to Normal after repair, so the container can be released at gate-out (204 No Content)
  updateCondition(containerId: string, request: UpdateConditionRequest): Observable<void> {
    return this.http.put<void>(`/api/containers/${containerId}/condition`, request);
  }

  // Exact match on a full container number, using the API's ?search= (part of the number)
  findContainer(containerNumber: string): Observable<Container | null> {
    return this.http
      .get<PagedResult<Container>>('/api/containers', { params: { search: containerNumber, pageSize: 5 } })
      .pipe(map((page) => page.items.find((c) => c.containerNumber === containerNumber) ?? null));
  }

  // ---------- catalog screens: one page of a list, create / update ----------

  // search: part of the code or the name (exact code first, then codes starting with the text)
  lineOperatorPage(page: number, pageSize: number, search = ''): Observable<PagedResult<LineOperator>> {
    return this.page<LineOperator>('/api/line-operators', page, pageSize, search);
  }

  // 201 with { id, code, name }; 409 LineOperator.Duplicate when the code exists
  createLineOperator(request: CreateLineOperatorRequest): Observable<CreatedResult> {
    return this.http.post<CreatedResult>('/api/line-operators', request);
  }

  // The code is the operator's identity on orders and reports, so only the name can change
  renameLineOperator(id: string, name: string): Observable<void> {
    return this.http.put<void>(`/api/line-operators/${id}`, { name });
  }

  // search: part of the tax code or the name
  customerPage(search: string, page: number, pageSize: number): Observable<PagedResult<Customer>> {
    return this.page<Customer>('/api/customers', page, pageSize, search);
  }

  // 201 with { id, taxCode, name }; 409 Customer.Duplicate when the tax code exists
  createCustomer(request: CreateCustomerRequest): Observable<CreatedResult> {
    return this.http.post<CreatedResult>('/api/customers', request);
  }

  // The tax code identifies the customer, so only the name can change
  renameCustomer(id: string, name: string): Observable<void> {
    return this.http.put<void>(`/api/customers/${id}`, { name });
  }

  // The catalog also lists archived blocks (after the active ones), so their history stays visible
  blockPage(page: number, pageSize: number): Observable<PagedResult<Block>> {
    return this.http.get<PagedResult<Block>>('/api/blocks', {
      params: { page, pageSize, includeArchived: true },
    });
  }

  // 409 Block.Duplicate (code taken) or Block.ContainersOfRemovedType (a removed type is still in the block)
  updateBlock(id: string, request: UpdateBlockRequest): Observable<void> {
    return this.http.put<void>(`/api/blocks/${id}`, request);
  }

  // 409 Block.HasContainers while a container is in the block; a block with history is archived
  deleteBlock(id: string): Observable<DeleteBlockResult> {
    return this.http.delete<DeleteBlockResult>(`/api/blocks/${id}`);
  }

  // 201 with { id, code, isVirtual }; 409 Block.Duplicate when the code exists
  createBlock(request: CreateBlockRequest): Observable<CreatedResult> {
    return this.http.post<CreatedResult>('/api/blocks', request);
  }

  // 409 Block.ContainersOutsideNewSize when shrinking would leave a container outside the block
  resizeBlock(id: string, request: ResizeBlockRequest): Observable<void> {
    return this.http.put<void>(`/api/blocks/${id}/size`, request);
  }

  // search: part of the container number
  containerPage(search: string, page: number, pageSize: number): Observable<PagedResult<Container>> {
    return this.page<Container>('/api/containers', page, pageSize, search);
  }

  // 201 with { id, containerNumber, isoCode }; 409 Container.Duplicate; 400 when the ISO code does not fit
  createContainer(request: CreateContainerRequest): Observable<CreatedResult> {
    return this.http.post<CreatedResult>('/api/containers', request);
  }

  // 409 Container.SizeChangeInYard when the size changes while the container is in the yard
  updateContainer(id: string, request: UpdateContainerRequest): Observable<void> {
    return this.http.put<void>(`/api/containers/${id}`, request);
  }

  // 409 Container.HasHistory once the container has been in the yard (204 No Content otherwise)
  deleteContainer(id: string): Observable<void> {
    return this.http.delete<void>(`/api/containers/${id}`);
  }

  // GET a list endpoint with page / pageSize and an optional ?search= (left out when empty)
  private page<T>(url: string, page: number, pageSize: number, search = ''): Observable<PagedResult<T>> {
    const params: Record<string, string | number> = { page, pageSize };
    if (search.trim()) params['search'] = search.trim();
    return this.http.get<PagedResult<T>>(url, { params });
  }

}
