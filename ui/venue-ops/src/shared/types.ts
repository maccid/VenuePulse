export type EventType = 'Match' | 'Concert'
export type EventStatus = 'OnSale' | 'SalesClosed' | 'Cancelled'
export type EventRelevance = 'Standard' | 'Special'

export interface EventSummary {
  id: string
  name: string
  venueName: string
  startsAtUtc: string
  type: EventType
  status: EventStatus
  relevance: EventRelevance
}

export interface CreateEventRequest {
  name: string
  startsAtUtc: string
  type: EventType
  relevance: EventRelevance
}

export interface EventRevenueDto {
  eventId: string
  confirmedRevenue: number
  confirmedCount: number
  pendingRevenue: number
  pendingCount: number
}

export interface TicketType {
  id: string
  eventId: string
  name: string
  price: number
  description: string | null
  effectivePrice: number
  priceTier: string
}

export type VenueOpsMode = 'sales' | 'checkin'

export interface ZoneOccupancy {
  zoneId: string
  eventId: string
  name: string
  capacity: number
  availableToSell: number
  confirmedSold: number
  checkedIn: number
  cancelled: boolean
}

export type ScanResult = { kind: 'idle' } | { kind: 'ok' } | { kind: 'error'; message: string }

export type TicketStatus = 'Pending' | 'AwaitingPayment' | 'Confirmed' | 'Rejected' | 'CheckedIn'

export interface TicketStatusUpdate {
  ticketId: string
  eventId: string
  zoneId: string
  status: TicketStatus
  rejectionReason: string | null
}

export interface ActivityEntry {
  id: string
  zoneName: string
  status: TicketStatus
  rejectionReason: string | null
  atUtc: string
}

export type SimulationMode = 'Sales' | 'Arrivals'

export interface SimulationRunStatus {
  eventId: string
  mode: SimulationMode
  running: boolean
  progress: number
  virtualNowUtc: string
}

export interface SimulationStartRequest {
  mode: SimulationMode
  eventStartsAtUtc: string
  durationSeconds: number
  targetSoldPct: number
  noShowPct: number
}

export interface PredictionPoint {
  timeUtc: string
  progress: number
  arrivals: number
}

export interface OccupancyPrediction {
  eventId: string
  fitFromData: boolean
  windowStartUtc: string
  eventStartsAtUtc: string
  nowUtc: string
  confirmedSold: number
  checkedIn: number
  predictedTotalArrivals: number
  predictedNoShows: number
  noShowRate: number
  fitR2: number | null
  historical: PredictionPoint[]
  projected: PredictionPoint[]
}
