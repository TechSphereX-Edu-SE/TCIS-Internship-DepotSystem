// Types matching the API responses (enums are sent as strings by JsonStringEnumConverter)

export type ContainerSize = 'Ft20' | 'Ft40';
export type ContainerCategory = 'Dry' | 'Reefer' | 'OpenTop' | 'FlatRack' | 'Bunker' | 'Ventilated' | 'Specialized';
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

// Partner (Đối tác): one catalog for container owners and line operators; the API path stays /api/line-operators
export type PartnerRole = 'Owner' | 'Operator';

export interface LineOperator {
  id: string;
  code: string;
  name: string;
  // Optional tax code (MST)
  taxCode: string | null;
  // Can own containers / can operate containers (assignments, visits, delivery orders)
  isOwner: boolean;
  isOperator: boolean;
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
  // Container types the block takes (feedback 3); gate-in and move only offer blocks that take the type
  allowedCategories: ContainerCategory[];
  // Archived (soft-deleted) block: kept for history, never offered for a new position
  archivedAt: string | null;
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
  // Owner text as first recorded (before partners existed), kept so old data can be reconciled
  ownerName: string;
  condition: ContainerCondition;
  conditionNote: string | null;
  createdAt: string;
  // Owner partner; null until someone picks it in the catalog (shown as "chưa đối chiếu")
  ownerId: string | null;
  ownerCode: string | null;
  ownerPartnerName: string | null;
  // Line operator of the confirmed operating assignment covering today; null = gate-in is refused
  currentOperatorId: string | null;
  currentOperatorCode: string | null;
}

// ---------- operating assignments (feedback 2: quan hệ khai thác) ----------

export type OperatingAssignmentStatus = 'Pending' | 'Confirmed' | 'Cancelled';

// Which line operator operates a container, from validFrom to validTo (Vietnam dates, both included)
export interface OperatingAssignment {
  id: string;
  containerId: string;
  containerNumber: string;
  operatorId: string;
  operatorCode: string;
  operatorName: string;
  validFrom: string;
  // null = open-ended
  validTo: string | null;
  status: OperatingAssignmentStatus;
  reference: string | null;
  note: string | null;
  createdAt: string;
  confirmedAt: string | null;
  endedAt: string | null;
  cancelledAt: string | null;
  cancelReason: string | null;
  // Visits that took their line operator from it: such an assignment can be ended but not cancelled
  visitCount: number;
}

// POST /api/containers/{id}/operating-assignments; confirm = already checked against the contract
export interface CreateOperatingAssignmentRequest {
  operatorId: string;
  validFrom: string;
  validTo: string | null;
  reference: string | null;
  note: string | null;
  confirm: boolean;
}

export const ASSIGNMENT_STATUS_LABEL: Record<OperatingAssignmentStatus, string> = {
  Pending: 'Chờ xác nhận',
  Confirmed: 'Đã xác nhận',
  Cancelled: 'Đã huỷ',
};

// "Chủ sở hữu, Hãng khai thác" for the partner list
export function partnerRolesText(partner: Pick<LineOperator, 'isOwner' | 'isOperator'>): string {
  const roles = [partner.isOwner ? 'Chủ sở hữu' : '', partner.isOperator ? 'Hãng khai thác' : ''];
  return roles.filter((r) => r).join(', ');
}

// Same rule as OperatingAssignment.CoversDate: confirmed and the date inside the period ("yyyy-MM-dd" compare)
export function assignmentCovers(assignment: Pick<OperatingAssignment, 'status' | 'validFrom' | 'validTo'>, date: string): boolean {
  return (
    assignment.status === 'Confirmed' &&
    assignment.validFrom <= date &&
    (assignment.validTo === null || date <= assignment.validTo)
  );
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
  // Where the container was put at gate-in; null for visits recorded before this was stored
  gateInBlockCode: string | null;
  gateInBay: number | null;
  gateInRow: number | null;
  gateInTier: number | null;
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
  // Set when the container came in Damaged: the repair case (Chờ giám định) the damage went into
  repairCaseId: string | null;
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

// GET /api/statistics/yard-inventory: shortStay = fewer than longStayDays days, longStay = that many or more
export interface LineOperatorInventory {
  lineOperatorId: string;
  code: string;
  name: string;
  shortStay: number;
  longStay: number;
  total: number;
}

export interface YardInventory {
  // Vietnam date the dwell days were counted on
  asOf: string;
  longStayDays: number;
  lineOperators: LineOperatorInventory[];
  totalShortStay: number;
  totalLongStay: number;
  total: number;
}

// GET /api/statistics/throughput: one row per Vietnam date and line operator with at least one move
export interface DailyThroughput {
  date: string;
  lineOperatorId: string;
  lineOperatorCode: string;
  gateIn: number;
  gateOut: number;
}

export interface Throughput {
  from: string;
  to: string;
  days: DailyThroughput[];
  totalGateIn: number;
  totalGateOut: number;
}

// Same limit as GetThroughputQuery.MaxDays (both dates included)
export const THROUGHPUT_MAX_DAYS = 366;

// ---------- catalog requests (Danh mục) ----------

// PUT /api/line-operators/{id}: name, tax code and roles; the code never changes. At least one role.
export interface UpdateLineOperatorRequest {
  name: string;
  taxCode: string | null;
  isOwner: boolean;
  isOperator: boolean;
}

// POST /api/line-operators
export interface CreateLineOperatorRequest extends UpdateLineOperatorRequest {
  code: string;
}

// POST /api/customers; PUT /api/customers/{id} only renames ({ name }), the tax code never changes
export interface CreateCustomerRequest {
  taxCode: string;
  name: string;
}

// POST /api/blocks: physical block = all three sizes, virtual block = none; at least one container type
export interface CreateBlockRequest {
  code: string;
  description: string | null;
  isVirtual: boolean;
  maxBays: number | null;
  maxRows: number | null;
  maxTiers: number | null;
  allowedCategories: ContainerCategory[];
}

// PUT /api/blocks/{id}: code, description and accepted types (the size has its own endpoint)
export interface UpdateBlockRequest {
  code: string;
  description: string | null;
  allowedCategories: ContainerCategory[];
}

// DELETE /api/blocks/{id}: archived = true when the block had history and was kept (soft delete)
export interface DeleteBlockResult {
  id: string;
  code: string;
  archived: boolean;
}

// PUT /api/blocks/{id}/size (physical blocks only; 409 when a container would be left outside)
export interface ResizeBlockRequest {
  maxBays: number;
  maxRows: number;
  maxTiers: number;
}

// PUT /api/containers/{id}: every field of the master data except the container number
export interface UpdateContainerRequest {
  isoCode: string;
  size: ContainerSize;
  category: ContainerCategory;
  maxGrossWeightKg: number;
  tareWeightKg: number;
  // "yyyy-MM-dd" or null when unknown
  manufactureDate: string | null;
  // A partner with the owner role, picked from the catalog (no free text)
  ownerId: string;
}

// POST /api/containers
export interface CreateContainerRequest extends UpdateContainerRequest {
  containerNumber: string;
}

// The 7 container types of Appendix II, in the order of the API enum
export const CATEGORIES: ContainerCategory[] = ['Dry', 'Reefer', 'OpenTop', 'FlatRack', 'Bunker', 'Ventilated', 'Specialized'];

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
// Vietnamese hint next to each type, so staff who know the local name can find it
export const CATEGORY_HINT: Record<ContainerCategory, string> = {
  Dry: 'Container khô',
  Reefer: 'Container lạnh',
  OpenTop: 'Container mở nóc',
  FlatRack: 'Container mặt bằng',
  Bunker: 'Container bồn',
  Ventilated: 'Container thông gió',
  Specialized: 'Container chuyên dụng',
};

export const CONDITION_LABEL: Record<ContainerCondition, string> = { Normal: 'Bình thường', Damaged: 'Hư hỏng' };

// Same check as Block.CheckAccepts on the API: the block's list of types, never its name or description
export function blockAccepts(block: Pick<Block, 'allowedCategories'>, category: ContainerCategory): boolean {
  return block.allowedCategories.includes(category);
}

// "Dry, Open Top" in the enum order; "Tất cả loại" when the block takes every type
export function categoriesText(categories: readonly ContainerCategory[]): string {
  if (CATEGORIES.every((c) => categories.includes(c))) return 'Tất cả loại';
  return CATEGORIES.filter((c) => categories.includes(c))
    .map((c) => CATEGORY_LABEL[c])
    .join(', ');
}

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

// "2026-10-22" + 3 -> "2026-10-25"; plain calendar arithmetic on a date without time (UTC avoids DST shifts)
export function addDays(isoDate: string, days: number): string {
  return new Date(Date.parse(`${isoDate}T00:00:00Z`) + days * DAY_MS).toISOString().slice(0, 10);
}

// Number of days from `from` to `to`, both included ("2026-10-18".."2026-10-22" -> 5); 0 when to is before from
export function daysInRange(from: string, to: string): number {
  return Math.max(0, Math.round((Date.parse(to) - Date.parse(from)) / DAY_MS) + 1);
}

/*
  Gate-in / gate-out totals per date for the chart, every date of the range included (a quiet day is a 0 bar,
  not a missing one). The API only returns dates that had a move, one row per line operator.
*/
export function dailyTotals(from: string, to: string, rows: readonly DailyThroughput[]): { date: string; gateIn: number; gateOut: number }[] {
  return Array.from({ length: daysInRange(from, to) }, (_, i) => {
    const date = addDays(from, i);
    const day = rows.filter((r) => r.date === date);
    return {
      date,
      gateIn: day.reduce((sum, r) => sum + r.gateIn, 0),
      gateOut: day.reduce((sum, r) => sum + r.gateOut, 0),
    };
  });
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

// ---------- inspection & repair (feedback 3, 4, 8; Q4, Q6) ----------

/*
  Q6 flow: Chờ giám định -> (Đạt | Chờ duyệt phương án) -> Đã duyệt sửa -> Đang sửa -> Chờ nghiệm thu
  -> (Đạt | back to Đang sửa). Only Passed (Đạt) closes the case and lets the container leave.
*/
export type RepairCaseStatus =
  | 'AwaitingInspection'
  | 'AwaitingEstimateApproval'
  | 'RepairApproved'
  | 'InRepair'
  | 'AwaitingAcceptance'
  | 'Passed';

export type RepairAction =
  | 'InspectPassed'
  | 'InspectNeedsRepair'
  | 'ApproveRepair'
  | 'StartRepair'
  | 'CompleteRepair'
  | 'AcceptPassed'
  | 'AcceptRejected';

// Where a damage was seen: gate-in, the yard / move screen, the container catalog, or an inspection
export type RepairFindingSource = 'GateIn' | 'Yard' | 'Catalog' | 'Inspection';

export type RepairEventKind = 'Opened' | 'FindingAdded' | RepairAction;

export const REPAIR_STATUS_LABEL: Record<RepairCaseStatus, string> = {
  AwaitingInspection: 'Chờ giám định',
  AwaitingEstimateApproval: 'Chờ duyệt phương án',
  RepairApproved: 'Đã duyệt sửa',
  InRepair: 'Đang sửa',
  AwaitingAcceptance: 'Chờ nghiệm thu',
  Passed: 'Đạt',
};

// Statuses in flow order, for the status filter
export const REPAIR_STATUSES: RepairCaseStatus[] = [
  'AwaitingInspection',
  'AwaitingEstimateApproval',
  'RepairApproved',
  'InRepair',
  'AwaitingAcceptance',
  'Passed',
];

// Button text of each action on the detail panel
export const REPAIR_ACTION_LABEL: Record<RepairAction, string> = {
  InspectPassed: 'Giám định: Đạt, không cần sửa',
  InspectNeedsRepair: 'Giám định: Cần sửa',
  ApproveRepair: 'Duyệt phương án sửa',
  StartRepair: 'Bắt đầu sửa',
  CompleteRepair: 'Sửa xong, chờ nghiệm thu',
  AcceptPassed: 'Nghiệm thu: Đạt',
  AcceptRejected: 'Nghiệm thu: Sửa lại',
};

// History line text; an action event reads as what was done
export const REPAIR_EVENT_LABEL: Record<RepairEventKind, string> = {
  Opened: 'Mở hồ sơ',
  FindingAdded: 'Ghi nhận thêm hư hỏng',
  InspectPassed: 'Giám định đạt, không cần sửa',
  InspectNeedsRepair: 'Giám định: cần sửa',
  ApproveRepair: 'Duyệt phương án sửa',
  StartRepair: 'Bắt đầu sửa',
  CompleteRepair: 'Sửa xong',
  AcceptPassed: 'Nghiệm thu đạt',
  AcceptRejected: 'Nghiệm thu không đạt, sửa lại',
};

export const FINDING_SOURCE_LABEL: Record<RepairFindingSource, string> = {
  GateIn: 'Nhập bãi',
  Yard: 'Trong bãi',
  Catalog: 'Danh mục',
  Inspection: 'Giám định',
};

// Actions that need a note (the API answers 400 RepairCase.ReasonRequired without one)
export const REPAIR_ACTIONS_NEEDING_NOTE: readonly RepairAction[] = ['AcceptRejected'];

// One row of GET /api/repair-cases; blockCode..tier = where the container is now (null outside the yard)
export interface RepairCaseSummary {
  id: string;
  containerId: string;
  containerNumber: string;
  size: ContainerSize;
  category: ContainerCategory;
  status: RepairCaseStatus;
  operatorCode: string | null;
  openedAt: string;
  closedAt: string | null;
  latestFinding: string;
  findingCount: number;
  photoCount: number;
  blockCode: string | null;
  bay: number | null;
  row: number | null;
  tier: number | null;
}

export interface RepairFinding {
  id: string;
  description: string;
  source: RepairFindingSource;
  visitId: string | null;
  reportedAt: string;
  reportedBy: string | null;
}

export interface RepairPhoto {
  id: string;
  findingId: string | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
  uploadedBy: string | null;
}

export interface RepairCaseEvent {
  id: string;
  kind: RepairEventKind;
  fromStatus: RepairCaseStatus | null;
  toStatus: RepairCaseStatus;
  note: string | null;
  at: string;
  by: string | null;
}

// GET /api/repair-cases/{id}; allowedActions = what the API accepts from the current status
export interface RepairCaseDetail {
  id: string;
  containerId: string;
  containerNumber: string;
  isoCode: string;
  size: ContainerSize;
  category: ContainerCategory;
  ownerCode: string | null;
  status: RepairCaseStatus;
  repairNeeded: boolean | null;
  openedAt: string;
  closedAt: string | null;
  visitId: string | null;
  operatorCode: string | null;
  conditionAtGateIn: ContainerCondition | null;
  inYard: boolean;
  blockCode: string | null;
  bay: number | null;
  row: number | null;
  tier: number | null;
  allowedActions: RepairAction[];
  findings: RepairFinding[];
  photos: RepairPhoto[];
  history: RepairCaseEvent[];
}

// Filters of GET /api/repair-cases; open: true = not Passed yet, false = Passed only
export interface RepairCaseFilter {
  search?: string;
  status?: RepairCaseStatus | null;
  open?: boolean | null;
  operatorId?: string | null;
  containerId?: string | null;
  reportedFrom?: string | null;
  reportedTo?: string | null;
}

// POST /api/repair-cases/report (source GateIn is refused: gate-in reports with the gate-in itself)
export interface ReportDamageRequest {
  containerId: string;
  description: string;
  source: Exclude<RepairFindingSource, 'GateIn'>;
}

// created = this report opened the case (false: it was added to the open case)
export interface RecordedDamage {
  caseId: string;
  created: boolean;
  findingId: string;
}

// Q4: JPG / PNG of at most 5 MB, checked before upload (the API checks the bytes again)
export const PHOTO_MAX_BYTES = 5 * 1024 * 1024;
export const PHOTO_TYPES: readonly string[] = ['image/jpeg', 'image/png'];

// null when the file can be uploaded, otherwise the reason in Vietnamese
export function photoFileError(file: Pick<File, 'type' | 'size' | 'name'>): string | null {
  if (!PHOTO_TYPES.includes(file.type)) return `${file.name}: chỉ nhận ảnh JPG hoặc PNG.`;
  if (file.size > PHOTO_MAX_BYTES) return `${file.name}: ảnh tối đa 5 MB.`;
  if (file.size === 0) return `${file.name}: file rỗng.`;
  return null;
}
