export interface TicketType {
  id: string
  eventId: string
  name: string
  price: number
  description: string | null
}

export interface ZoneOccupancy {
  zoneId: string
  eventId: string
  name: string
  capacity: number
  availableToSell: number
  confirmedSold: number
  checkedIn: number
}

export type TicketStatus = 'Pending' | 'Confirmed' | 'Rejected' | 'CheckedIn'

export interface TicketStatusUpdate {
  ticketId: string
  status: TicketStatus
  rejectionReason: string | null
}
