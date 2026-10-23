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
  CreateOperatingAssignmentRequest,
  Customer,
  DeleteBlockResult,
  LineOperator,
  OperatingAssignment,
  PagedResult,
  PartnerRole,
  ResizeBlockRequest,
  UpdateBlockRequest,
  UpdateContainerRequest,
  UpdateLineOperatorRequest,
} from './depot-models';

// Create responses only carry the new id (plus the key fields, not needed by the screens)
type CreatedResult = { id: string };

// Master data (Danh mục): dropdowns and lookups for the business screens, and the catalog screens' CRUD

// Master data used by the business screens (dropdowns, container lookup)
@Service()
export class MasterDataApi {
  private readonly http = inject(HttpClient);

  // A depot has a handful of partners and blocks, so one large page is enough for a dropdown.
  // role: only owners (container form) or only line operators (orders, statistics); every partner when left out
  lineOperators(role?: PartnerRole): Observable<LineOperator[]> {
    const params: Record<string, string | number> = { pageSize: 100 };
    if (role) params['role'] = role;
    return this.http
      .get<PagedResult<LineOperator>>('/api/line-operators', { params })
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

  // Exact match on a full container number, using the API's ?search= (part of the number)
  findContainer(containerNumber: string): Observable<Container | null> {
    return this.http
      .get<PagedResult<Container>>('/api/containers', { params: { search: containerNumber, pageSize: 5 } })
      .pipe(map((page) => page.items.find((c) => c.containerNumber === containerNumber) ?? null));
  }

  // ---------- catalog screens: one page of a list, create / update ----------

  // search: part of the code or the name (exact code first, then codes starting with the text); role: see lineOperators
  lineOperatorPage(page: number, pageSize: number, search = '', role?: PartnerRole): Observable<PagedResult<LineOperator>> {
    return this.page<LineOperator>('/api/line-operators', page, pageSize, search, role ? { role } : {});
  }

  // 201 with { id, code, name }; 409 LineOperator.Duplicate when the code exists
  createLineOperator(request: CreateLineOperatorRequest): Observable<CreatedResult> {
    return this.http.post<CreatedResult>('/api/line-operators', request);
  }

  // The code is the partner's identity on orders and reports, so it never changes.
  // 409 Partner.OwnerRoleInUse / Partner.OperatorRoleInUse when removing a role still in use
  updateLineOperator(id: string, request: UpdateLineOperatorRequest): Observable<void> {
    return this.http.put<void>(`/api/line-operators/${id}`, request);
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

  // search: part of the container number; operatorId: only containers that line operator operates today
  // filters: extra query parameters of the catalog screen (see containerFilterParams)
  containerPage(
    search: string,
    page: number,
    pageSize: number,
    operatorId?: string | null,
    filters: Record<string, string> = {},
  ): Observable<PagedResult<Container>> {
    return this.page<Container>('/api/containers', page, pageSize, search, operatorId ? { operatorId, ...filters } : filters);
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

  // ---------- operating assignments (quan hệ khai thác) ----------

  // Every assignment of a container, newest period first (cancelled ones included, as history)
  containerAssignments(containerId: string): Observable<OperatingAssignment[]> {
    return this.http.get<OperatingAssignment[]>(`/api/containers/${containerId}/operating-assignments`);
  }

  // 409 Partner.NotOperator, OperatingAssignment.Overlap (when confirm = true)
  createAssignment(containerId: string, request: CreateOperatingAssignmentRequest): Observable<CreatedResult> {
    return this.http.post<CreatedResult>(`/api/containers/${containerId}/operating-assignments`, request);
  }

  // 409 OperatingAssignment.Overlap when another confirmed period covers some of the same days
  confirmAssignment(id: string): Observable<void> {
    return this.http.post<void>(`/api/operating-assignments/${id}/confirm`, {});
  }

  // validTo = last day included; 409 OperatingAssignment.UsedAfterEnd when a later gate-in used it
  endAssignment(id: string, validTo: string): Observable<void> {
    return this.http.post<void>(`/api/operating-assignments/${id}/end`, { validTo });
  }

  // 409 OperatingAssignment.InUse once a visit took its operator from it
  cancelAssignment(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`/api/operating-assignments/${id}/cancel`, { reason });
  }

  // GET a list endpoint with page / pageSize, an optional ?search= (left out when empty) and extra filters
  private page<T>(
    url: string,
    page: number,
    pageSize: number,
    search = '',
    filters: Record<string, string> = {},
  ): Observable<PagedResult<T>> {
    const params: Record<string, string | number> = { page, pageSize, ...filters };
    if (search.trim()) params['search'] = search.trim();
    return this.http.get<PagedResult<T>>(url, { params });
  }

}
