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
