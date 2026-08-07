export interface TicketType {
  id: string
  eventId: string
  name: string
  price: number
  description: string | null
}

export interface ZoneOccupancy {
  zoneId: string
  name: string
  capacity: number
  confirmedSold: number
  checkedIn: number
}

export type TicketStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'CheckedIn'

export interface TicketStatusUpdate {
  ticketId: string
  status: TicketStatus
  rejectionReason: string | null
}
