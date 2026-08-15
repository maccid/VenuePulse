import type { SimulationMode } from '@venue/core/domain'

export const MODE_LABELS: Record<SimulationMode, string> = {
  Sales: 'Ventas (antes del evento)',
  Arrivals: 'Llegadas (horas previas)',
}
