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

// Days in the yard counted in Vietnam calendar days, gate-in day = 0 (same as DepotTime.DwellDays)
export function dwellDays(gateInAt: string, now: Date = new Date()): number {
  // Shift both instants to Vietnam time, then drop the time of day: whole days between the two dates
  const vietnamDay = (ms: number) => Math.floor((ms + VIETNAM_OFFSET_MS) / DAY_MS);
  return vietnamDay(now.getTime()) - vietnamDay(new Date(gateInAt).getTime());
}
