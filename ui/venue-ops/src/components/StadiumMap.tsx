import type { VenueOpsMode, ZoneOccupancy } from '../types'
import ZoneShape, { type StandShape, type ZoneLayout } from './ZoneShape'

// Gradas con contorno curvo hacia el óvalo exterior — copia adaptada de `ticket-checkout`
// (mismas curvas, sin código compartido entre micro frontends, ver ADR 0004). Este/Oeste
// reutilizan la curva de Norte/Sur girada con `transform` porque el exterior (8,8,324,264,
// rx 85) no es cuadrado. A diferencia de `ticket-checkout` no hay semáforo aparte ni azul fijo:
// el bloque entero se tiñe por tier (ver ZoneShape), que es lo que necesita operaciones para
// escanear ocupación de un vistazo (ver ADR 0024, sin tocar ese criterio).
const ESTE_OESTE_TRANSFORM = 'matrix(-0.00261, -0.70282102, 0.98233294, 0, 27.46486001, -0.33711338)'
const ESTE_OESTE_ORIGIN = '170.725px 140.696px'

const MATCH_STANDS: Record<string, StandShape> = {
  'Grada VIP Norte': {
    path: 'M 87.59 82.282 L 46.307 29.872 C 87.035 7.34 254.481 6.173 293.641 30.59 L 251.641 82.641 L 87.59 82.282',
    labelX: 170,
    labelY: 44,
    labelLines: ['Grada VIP', 'Norte'],
  },
  'Grada VIP Sur': {
    path: 'M 88.602 199.11 L 47.592 249.066 C 81.504 271.326 256.502 272.935 294.653 250.802 L 252.653 198.751 L 88.602 199.11',
    labelX: 170,
    labelY: 229,
    labelLines: ['Grada VIP', 'Sur'],
  },
  'Grada Este': {
    path: 'M 90.816 198.403 L 17.184 241.222 C 32.548 260.331 313.993 261.982 324.189 241.239 L 249.458 198.044 L 90.816 198.403',
    transform: ESTE_OESTE_TRANSFORM,
    transformOrigin: ESTE_OESTE_ORIGIN,
    labelX: 286,
    labelY: 134,
    labelLines: ['Grada', 'Este'],
  },
  'Grada Oeste': {
    path: 'M 249.914 26.556 L 325.097 -15.981 C 308.911 -37.15 49.131 -42.901 18.481 -15.72 L 91.272 26.915 L 249.914 26.556',
    transform: ESTE_OESTE_TRANSFORM,
    transformOrigin: ESTE_OESTE_ORIGIN,
    labelX: 54,
    labelY: 134,
    labelLines: ['Grada', 'Oeste'],
  },
}

const CONCERT_LAYOUT: Record<string, ZoneLayout> = {
  Pista: { x: 110, y: 46, width: 120, height: 170 },
  'Grada Norte': { x: 40, y: 46, width: 58, height: 170 },
  'Grada Sur': { x: 242, y: 46, width: 58, height: 170 },
}

// Mismo campo que ticket-checkout (88,84,164,112) para que las curvas de las gradas encajen con
// el óvalo exterior — con las líneas realistas del campo de fútbol (banda, círculo central, áreas
// y penaltis) para diferenciar visualmente un partido de un concierto.
const FIELD = { x: 88, y: 84, width: 164, height: 112 }
const FIELD_CENTER_X = FIELD.x + FIELD.width / 2
const FIELD_CENTER_Y = FIELD.y + FIELD.height / 2

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
  const laidOut = zones.filter((z) => (isConcert ? CONCERT_LAYOUT[z.name] : MATCH_STANDS[z.name]))
  const unmapped = zones.filter((z) => !(isConcert ? CONCERT_LAYOUT[z.name] : MATCH_STANDS[z.name]))

  return (
    <div className="stadium-map">
      <svg viewBox="0 0 340 280" role="img" aria-label="Mapa del evento" className="stadium-map__svg">
        {isConcert ? (
          <>
            <rect x="20" y="20" width="300" height="240" rx="6" className="stadium-map__pitch" />
            <rect x="90" y="12" width="160" height="22" rx="4" className="stadium-map__stage" />
            <text x="170" y="23" textAnchor="middle" dominantBaseline="middle" className="stadium-map__stage-label">
              Escenario
            </text>
          </>
        ) : (
          <>
            <rect x="8" y="8" width="324" height="264" rx="85" className="stadium-map__exterior" />

            {/* Campo: rectángulo verde con líneas de un campo de fútbol real (banda central,
                círculo central, áreas y puntos de penalti) — mismo dibujo que ticket-checkout,
                para diferenciar un partido de un concierto. */}
            <g className="stadium-map__field">
              <rect
                x={FIELD.x}
                y={FIELD.y}
                width={FIELD.width}
                height={FIELD.height}
                rx={4}
                className="stadium-map__field-fill"
              />
              <line
                x1={FIELD_CENTER_X}
                y1={FIELD.y}
                x2={FIELD_CENTER_X}
                y2={FIELD.y + FIELD.height}
                className="stadium-map__field-line"
              />
              <circle cx={FIELD_CENTER_X} cy={FIELD_CENTER_Y} r={26} className="stadium-map__field-line" />
              <circle cx={FIELD_CENTER_X} cy={FIELD_CENTER_Y} r={2} className="stadium-map__field-dot" />
              <rect x={FIELD.x} y={FIELD_CENTER_Y - 30} width={30} height={60} className="stadium-map__field-line" />
              <rect x={FIELD.x} y={FIELD_CENTER_Y - 16} width={14} height={32} className="stadium-map__field-line" />
              <circle cx={FIELD.x + 22} cy={FIELD_CENTER_Y} r={2} className="stadium-map__field-dot" />
              <rect
                x={FIELD.x + FIELD.width - 30}
                y={FIELD_CENTER_Y - 30}
                width={30}
                height={60}
                className="stadium-map__field-line"
              />
              <rect
                x={FIELD.x + FIELD.width - 14}
                y={FIELD_CENTER_Y - 16}
                width={14}
                height={32}
                className="stadium-map__field-line"
              />
              <circle cx={FIELD.x + FIELD.width - 22} cy={FIELD_CENTER_Y} r={2} className="stadium-map__field-dot" />
            </g>
          </>
        )}

        {laidOut.map((zone) => {
          const tier = occupancyTier(occupancyRatio(zone, mode))

          if (isConcert) {
            return (
              <ZoneShape
                key={zone.zoneId}
                name={zone.name}
                layout={CONCERT_LAYOUT[zone.name]}
                tier={tier}
                variant="tiered"
              />
            )
          }

          return (
            <ZoneShape
              key={zone.zoneId}
              name={zone.name}
              shape={MATCH_STANDS[zone.name]}
              tier={tier}
              variant="stand"
            />
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
