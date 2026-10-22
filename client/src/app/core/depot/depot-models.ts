// Types matching the API responses (enums are sent as strings by JsonStringEnumConverter)

export type ContainerSize = 'Ft20' | 'Ft40';
export type ContainerCategory = 'Dry' | 'Reefer' | 'OpenTop' | 'FlatRack' | 'Bunker' | 'Ventilated' | 'Specialized';
export type ContainerGrade = 'A' | 'B' | 'C';
export type ContainerCondition = 'Normal' | 'Damaged';

// Domain/Common/PagedResult
export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface LineOperator {
  id: string;
  code: string;
  name: string;
  createdAt: string;
}

// Bay/Row/Tier sizes are null for a virtual block
export interface Block {
  id: string;
  code: string;
  description: string | null;
  isVirtual: boolean;
  maxBays: number | null;
  maxRows: number | null;
  maxTiers: number | null;
  containersInYard: number;
  createdAt: string;
}

export interface Container {
  id: string;
  containerNumber: string;
  isoCode: string;
  size: ContainerSize;
  category: ContainerCategory;
  maxGrossWeightKg: number;
  tareWeightKg: number;
  manufactureDate: string | null;
  ownerName: string;
  condition: ContainerCondition;
  conditionNote: string | null;
  createdAt: string;
}

// GET /api/container-visits/position
export interface ContainerPosition {
  visitId: string;
  containerId: string;
  containerNumber: string;
  size: ContainerSize;
  lineOperatorCode: string;
  blockId: string;
  blockCode: string;
  isVirtualBlock: boolean;
  bay: number | null;
  row: number | null;
  tier: number | null;
  gateInAt: string;
}

// GET /api/container-visits/in-yard
export interface YardContainer {
  visitId: string;
  containerId: string;
  containerNumber: string;
  isoCode: string;
  size: ContainerSize;
  category: ContainerCategory;
  condition: ContainerCondition;
  lineOperatorCode: string;
  blockId: string;
  blockCode: string;
  bay: number | null;
  row: number | null;
  tier: number | null;
  grade: ContainerGrade;
  gateInAt: string;
}

// GET /api/container-visits/daily
export interface DailyGateIn {
  visitId: string;
  containerNumber: string;
  isoCode: string;
  size: ContainerSize;
  category: ContainerCategory;
  lineOperatorCode: string;
  blockCode: string;
  bay: number | null;
  row: number | null;
  tier: number | null;
  grade: ContainerGrade;
  vehicleNo: string;
  gateInAt: string;
}

export interface DailyGateOut {
  visitId: string;
  containerNumber: string;
  isoCode: string;
  size: ContainerSize;
  category: ContainerCategory;
  lineOperatorCode: string;
  deliveryOrderNumber: string | null;
  orderLineQuantity: number | null;
  orderLineRemaining: number | null;
  vehicleNo: string | null;
  gateOutAt: string;
}

export interface DailyVisits {
  date: string;
  gateIns: DailyGateIn[];
  gateOuts: DailyGateOut[];
}

// POST /api/container-visits/gate-in (GateInCommand)
export interface GateInRequest {
  containerId: string;
  lineOperatorId: string;
  blockId: string;
  bay: number | null;
  row: number | null;
  tier: number | null;
  grade: ContainerGrade;
  condition: ContainerCondition;
  conditionNote: string | null;
  vehicleNo: string;
}

export interface GateInResult {
  visitId: string;
  containerNumber: string;
  blockCode: string;
  bay: number | null;
  row: number | null;
  tier: number | null;
  gateInAt: string;
}

// POST /api/container-visits/move (MoveContainerCommand); the response is a ContainerPosition
export interface MoveRequest {
  containerId: string;
  blockId: string;
  bay: number | null;
  row: number | null;
  tier: number | null;
}
export interface Customer {
  id: string;
  taxCode: string;
  name: string;
  createdAt: string;
}

// GET /api/delivery-orders(/{id}): quantity ordered, released at gate-out and still available per type
export interface DeliveryOrderLine {
  category: ContainerCategory;
  quantity: number;
  releasedQuantity: number;
  remainingQuantity: number;
}

export interface DeliveryOrder {
  id: string;
  orderNumber: string;
  lineOperatorId: string;
  lineOperatorCode: string;
  customerId: string;
  customerTaxCode: string;
  customerName: string;
  // Vietnam date "yyyy-MM-dd": the last day containers may be released
  expiryDate: string;
  // Computed by the API with today's Vietnam date
  isExpired: boolean;
  vesselVoyage: string;
  lines: DeliveryOrderLine[];
  createdAt: string;
}

// POST /api/delivery-orders (CreateDeliveryOrderCommand)
export interface CreateDeliveryOrderRequest {
  orderNumber: string;
  lineOperatorId: string;
  customerId: string;
  expiryDate: string;
  vesselVoyage: string;
  lines: { category: ContainerCategory; quantity: number }[];
}

export interface CreateDeliveryOrderResult {
  id: string;
  orderNumber: string;
}

// POST /api/container-visits/gate-out (GateOutCommand)
export interface GateOutRequest {
  containerId: string;
  deliveryOrderId: string;
  vehicleNo: string;
}

export interface GateOutResult {
  visitId: string;
  containerNumber: string;
  orderNumber: string;
  category: ContainerCategory;
  // Containers of this type still allowed under the order after this release
  remainingQuantity: number;
  vehicleNo: string;
  gateOutAt: string;
}

// PUT /api/containers/{id}/condition (the API keeps the note only for a damaged container)
export interface UpdateConditionRequest {
  condition: ContainerCondition;
  conditionNote: string | null;
}

// ---------- display helpers ----------

export const SIZE_LABEL: Record<ContainerSize, string> = { Ft20: '20ft', Ft40: '40ft' };

// Container types of Appendix II, shown with their usual English names (as in the spec)
export const CATEGORY_LABEL: Record<ContainerCategory, string> = {
  Dry: 'Dry',
  Reefer: 'Reefer',
  OpenTop: 'Open Top',
  FlatRack: 'Flat Rack',
  Bunker: 'Bunker',
  Ventilated: 'Ventilated',
  Specialized: 'Specialized',
};

export const CONDITION_LABEL: Record<ContainerCondition, string> = { Normal: 'Bình thường', Damaged: 'Hư hỏng' };

// "A 04-1-1" for a physical block, just "WAIT" for a virtual block (same style as the mockups)
export function formatPosition(blockCode: string, bay: number | null, row: number | null, tier: number | null): string {
  return bay === null ? blockCode : `${blockCode} ${String(bay).padStart(2, '0')}-${row}-${tier}`;
}

// Vietnam is UTC+7 all year (no daylight saving), same offset as Domain/Services/DepotTime
const VIETNAM_OFFSET_MS = 7 * 60 * 60 * 1000;
const DAY_MS = 24 * 60 * 60 * 1000;

// Day number of an instant in Vietnam time: shift to UTC+7, then drop the time of day
function vietnamDay(ms: number): number {
  return Math.floor((ms + VIETNAM_OFFSET_MS) / DAY_MS);
}

// Days in the yard counted in Vietnam calendar days, gate-in day = 0 (same as DepotTime.DwellDays)
export function dwellDays(gateInAt: string, now: Date = new Date()): number {
  return vietnamDay(now.getTime()) - vietnamDay(new Date(gateInAt).getTime());
}

// Today's date in Vietnam as "yyyy-MM-dd" (same as DepotTime.DateOf), for date inputs and expiry checks;
// the browser's own time zone is not used, so a laptop set to another zone still shows the depot's date
export function vietnamToday(now: Date = new Date()): string {
  return new Date(vietnamDay(now.getTime()) * DAY_MS).toISOString().slice(0, 10);
}

// "2026-10-31" -> "31/10/2026" (a DateOnly from the API has no time, so no time zone conversion)
export function formatDate(isoDate: string): string {
  const [year, month, day] = isoDate.split('-');
  return `${day}/${month}/${year}`;
}

// Instant from the API -> "22/10/2026 08:20" in Vietnam time (for text built in code; templates use DatePipe)
export function formatDateTime(iso: string): string {
  const vietnam = new Date(new Date(iso).getTime() + VIETNAM_OFFSET_MS).toISOString();
  return `${formatDate(vietnam.slice(0, 10))} ${vietnam.slice(11, 16)}`;
}

/*
  Status shown for a delivery order (mockup 05):
    - done: every line fully released (checked first: a finished order is not "expired" in practice)
    - expired: today (Vietnam) is after the expiry date
    - open: still usable at gate-out
*/
export type OrderStatus = 'open' | 'done' | 'expired';

export const ORDER_STATUS_LABEL: Record<OrderStatus, string> = { open: 'Còn hạn', done: 'Đã giao đủ', expired: 'Hết hạn' };

export function orderStatus(order: DeliveryOrder): OrderStatus {
  if (order.lines.every((l) => l.remainingQuantity === 0)) return 'done';
  return order.isExpired ? 'expired' : 'open';
}

// "Dry 0/2 · Reefer 1/1" = released / ordered per type, as in the order list
export function releasedSummary(order: DeliveryOrder): string {
  return order.lines.map((l) => `${CATEGORY_LABEL[l.category]} ${l.releasedQuantity}/${l.quantity}`).join(' · ');
}
