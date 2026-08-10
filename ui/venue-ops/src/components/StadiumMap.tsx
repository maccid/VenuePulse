import type { VenueOpsMode, ZoneOccupancy } from '../types'

interface ZoneLayout {
  x: number
  y: number
  width: number
  height: number
}

// Las zonas son fijas por tipo de evento (partido vs. concierto, ver ZoneService en
// AccessControl), así que un mapeo estático por nombre cubre el 100% del caso de uso actual sin
// añadir geometría al backend — ver ADR 0017 y el ADR de EventType/carrito. Copia adaptada de la
// de `ticket-checkout` (sin código compartido entre micro frontends, ver ADR 0004).
const MATCH_LAYOUT: Record<string, ZoneLayout> = {
  'Puerta Norte': { x: 76, y: 26, width: 188, height: 50 },
  'Puerta Sur': { x: 76, y: 204, width: 188, height: 50 },
  'Grada Oeste VIP': { x: 30, y: 88, width: 46, height: 104 },
  'Grada Este VIP': { x: 264, y: 88, width: 46, height: 104 },
}

const CONCERT_LAYOUT: Record<string, ZoneLayout> = {
  Pista: { x: 110, y: 46, width: 120, height: 170 },
  'Grada Norte': { x: 40, y: 46, width: 58, height: 170 },
  'Grada Sur': { x: 242, y: 46, width: 58, height: 170 },
}

interface Props {
  zones: ZoneOccupancy[]
  mode?: VenueOpsMode
}

// Ocupación de VENTA (reservado + confirmado) en modo 'sales' — antes del evento es la señal que
// de verdad se mueve, checkedIn se queda a 0 hasta el día del evento. En modo 'checkin' (torniquete,
// Bloque 18) se colorea por el aforo que ya ha entrado físicamente — ver ADR 0022.
function occupancyRatio(zone: ZoneOccupancy, mode: VenueOpsMode): number {
  if (zone.capacity === 0) return 0
  if (mode === 'checkin') return Math.min(1, Math.max(0, zone.checkedIn / zone.capacity))
  return Math.min(1, Math.max(0, (zone.capacity - zone.availableToSell) / zone.capacity))
}

function occupancyTier(ratio: number): 'low' | 'mid' | 'high' {
  if (ratio >= 0.9) return 'high'
  if (ratio >= 0.7) return 'mid'
  return 'low'
}

const LEGEND_LABEL: Record<VenueOpsMode, { low: string; mid: string; high: string }> = {
  sales: { low: '< 70% vendido', mid: '70–90% vendido', high: '> 90% vendido' },
  checkin: { low: '< 70% dentro', mid: '70–90% dentro', high: '> 90% dentro' },
}

export default function StadiumMap({ zones, mode = 'sales' }: Props) {
  const isConcert = zones.some((z) => z.name === 'Pista')
  const layout = isConcert ? CONCERT_LAYOUT : MATCH_LAYOUT
  const laidOut = zones.filter((z) => layout[z.name])
  const unmapped = zones.filter((z) => !layout[z.name])

  return (
    <div className="stadium-map">
      <svg viewBox="0 0 340 280" role="img" aria-label="Plano del recinto" className="stadium-map__svg">
        <rect x="20" y="20" width="300" height="240" rx="6" className="stadium-map__pitch" />

        {isConcert ? (
          <>
            <rect x="90" y="12" width="160" height="22" rx="4" className="stadium-map__stage" />
            <text x="170" y="23" textAnchor="middle" dominantBaseline="middle" className="stadium-map__stage-label">
              Escenario
            </text>
          </>
        ) : (
          <>
            <line x1="20" y1="140" x2="320" y2="140" className="stadium-map__midline" />
            <circle cx="170" cy="140" r="28" className="stadium-map__midcircle" />
          </>
        )}

        {laidOut.map((zone) => {
          const zoneLayout = layout[zone.name]
          const tier = occupancyTier(occupancyRatio(zone, mode))

          return (
            <g key={zone.zoneId} className={`stadium-map__zone stadium-map__zone--${tier}`}>
              <rect x={zoneLayout.x} y={zoneLayout.y} width={zoneLayout.width} height={zoneLayout.height} rx={4} />
              <text
                x={zoneLayout.x + zoneLayout.width / 2}
                y={zoneLayout.y + zoneLayout.height / 2}
                textAnchor="middle"
                dominantBaseline="middle"
              >
                {zone.name}
              </text>
            </g>
          )
        })}
      </svg>

      {unmapped.length > 0 && (
        <ul className="stadium-map__fallback">
          {unmapped.map((zone) => (
            <li key={zone.zoneId}>{zone.name}</li>
          ))}
        </ul>
      )}

      <ul className="stadium-map__legend">
        <li className="stadium-map__legend-item stadium-map__legend-item--low">{LEGEND_LABEL[mode].low}</li>
        <li className="stadium-map__legend-item stadium-map__legend-item--mid">{LEGEND_LABEL[mode].mid}</li>
        <li className="stadium-map__legend-item stadium-map__legend-item--high">{LEGEND_LABEL[mode].high}</li>
      </ul>
    </div>
  )
}
