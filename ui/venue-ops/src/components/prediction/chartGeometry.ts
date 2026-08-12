import type { PredictionPoint } from '../../shared/types'

export const CHART_WIDTH = 640
export const CHART_HEIGHT = 240
export const CHART_MARGIN = { top: 16, right: 12, bottom: 28, left: 40 }
export const PLOT_WIDTH = CHART_WIDTH - CHART_MARGIN.left - CHART_MARGIN.right
export const PLOT_HEIGHT = CHART_HEIGHT - CHART_MARGIN.top - CHART_MARGIN.bottom

export function toX(progress: number): number {
  return CHART_MARGIN.left + progress * PLOT_WIDTH
}

export function toY(value: number, maxY: number): number {
  return CHART_MARGIN.top + PLOT_HEIGHT * (1 - value / maxY)
}

export function polylinePoints(points: PredictionPoint[], maxY: number): string {
  return points
    .map((point) => `${toX(point.progress).toFixed(1)},${toY(point.arrivals, maxY).toFixed(1)}`)
    .join(' ')
}

export function ceilToTens(value: number): number {
  return Math.ceil(value / 10) * 10
}

export function progressWithinWindow(nowUtc: string, windowStartUtc: string, eventStartsAtUtc: string): number {
  const windowMillis = Date.parse(eventStartsAtUtc) - Date.parse(windowStartUtc)
  if (windowMillis <= 0) return 0
  return Math.min(1, Math.max(0, (Date.parse(nowUtc) - Date.parse(windowStartUtc)) / windowMillis))
}

export interface XTick {
  label: string
  progress: number
}

export function buildXTicks(): XTick[] {
  return [
    { label: '-3 h', progress: 0 },
    { label: '-2 h', progress: 1 / 3 },
    { label: '-1 h', progress: 2 / 3 },
    { label: 'Evento', progress: 1 },
  ]
}
