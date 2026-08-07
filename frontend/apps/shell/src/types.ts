export interface EventSummary {
  id: string
  name: string
  venueName: string
  startsAtUtc: string
}

export interface ZoneOccupancy {
  zoneId: string
  name: string
  capacity: number
  confirmedSold: number
  checkedIn: number
}
