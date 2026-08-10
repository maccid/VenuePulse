import type { ZoneOccupancy } from '../types'

interface ZoneLayout {
  x: number
  y: number
  width: number
  height: number
}

interface ZonePricing {
  price: number
  effectivePrice: number
  priceTier: string
}

// Las zonas son fijas por tipo de evento (ver ZoneService en AccessControl: partido vs.
// concierto), así que un mapeo estático por nombre cubre el 100% del caso de uso actual sin
// añadir geometría al backend — ver ADR 0017 y el ADR de EventType/carrito. Partido: 4 gradas
// alrededor del campo (Norte/Sur más anchas, Este/Oeste VIP más estrechas), con hueco en las
// esquinas para que se vea el gris del estadio detrás.
const MATCH_LAYOUT: Record<string, ZoneLayout> = {
  'Puerta Norte': { x: 76, y: 26, width: 188, height: 50 },
  'Puerta Sur': { x: 76, y: 204, width: 188, height: 50 },
  'Grada Oeste VIP': { x: 30, y: 88, width: 46, height: 104 },
  'Grada Este VIP': { x: 264, y: 88, width: 46, height: 104 },
}

// Concierto: la zona central (el campo, en un partido) pasa a ser "Pista" (de pie, cerca del
// escenario); las gradas laterales flanquean. "Escenario" es solo decorativo, no es una zona
// real — no aparece en `zones` ni se puede seleccionar.
const CONCERT_LAYOUT: Record<string, ZoneLayout> = {
  Pista: { x: 110, y: 46, width: 120, height: 170 },
  'Grada Norte': { x: 40, y: 46, width: 58, height: 170 },
  'Grada Sur': { x: 242, y: 46, width: 58, height: 170 },
}

// Campo de partido: rectángulo central con líneas de un campo de fútbol real (banda central,
// círculo central, áreas y punto de penalti) — puramente decorativo, no es una zona seleccionable.
const FIELD = { x: 88, y: 84, width: 164, height: 112 }
const FIELD_CENTER_X = FIELD.x + FIELD.width / 2
const FIELD_CENTER_Y = FIELD.y + FIELD.height / 2

interface Props {
  zones: ZoneOccupancy[]
  selectedZoneId?: string
  onSelect?: (zoneId: string) => void
  pricingByZoneId?: Record<string, ZonePricing>
}

function occupancyRatio(zone: ZoneOccupancy): number {
  if (zone.capacity === 0) return 0
  return Math.min(1, Math.max(0, (zone.capacity - zone.availableToSell) / zone.capacity))
}

function occupancyTier(ratio: number): 'low' | 'mid' | 'high' {
  if (ratio >= 0.9) return 'high'
  if (ratio >= 0.7) return 'mid'
  return 'low'
}

export default function StadiumMap({ zones, selectedZoneId, onSelect, pricingByZoneId }: Props) {
  const interactive = Boolean(onSelect)
  const isConcert = zones.some((z) => z.name === 'Pista')
  const layout = isConcert ? CONCERT_LAYOUT : MATCH_LAYOUT
  const laidOut = zones.filter((z) => layout[z.name])
  const unmapped = zones.filter((z) => !layout[z.name])

  return (
    <div className="stadium-map">
      <svg viewBox="0 0 340 280" role="img" aria-label="Plano del recinto" className="stadium-map__svg">
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
            {/* Estadio: fondo gris de todo el recinto, con hueco en las esquinas donde no llegan
                las gradas (ver MATCH_LAYOUT). */}
            <rect x="8" y="8" width="324" height="264" rx="56" className="stadium-map__exterior" />

            {/* Campo: rectángulo verde con líneas de un campo de fútbol real, decorativo. */}
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
          const zoneLayout = layout[zone.name]
          const tier = occupancyTier(occupancyRatio(zone))
          const isSelected = zone.zoneId === selectedZoneId
          const pricing = pricingByZoneId?.[zone.zoneId]
          const classes = [
            'stadium-map__zone',
            isConcert ? `stadium-map__zone--${tier}` : 'stadium-map__zone--stand',
            isSelected ? 'stadium-map__zone--selected' : '',
            interactive ? 'stadium-map__zone--interactive' : '',
          ]
            .filter(Boolean)
            .join(' ')

          return (
            <g
              key={zone.zoneId}
              className={classes}
              onClick={interactive ? () => onSelect?.(zone.zoneId) : undefined}
              role={interactive ? 'button' : undefined}
              aria-pressed={interactive ? isSelected : undefined}
              tabIndex={interactive ? 0 : undefined}
              onKeyDown={
                interactive
                  ? (e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault()
                        onSelect?.(zone.zoneId)
                      }
                    }
                  : undefined
              }
            >
              <rect x={zoneLayout.x} y={zoneLayout.y} width={zoneLayout.width} height={zoneLayout.height} rx={4} />
              <text
                x={zoneLayout.x + zoneLayout.width / 2}
                y={zoneLayout.y + zoneLayout.height / 2 - (pricing ? 7 : 0)}
                textAnchor="middle"
                dominantBaseline="middle"
              >
                {zone.name}
              </text>
              {pricing && (
                <text
                  x={zoneLayout.x + zoneLayout.width / 2}
                  y={zoneLayout.y + zoneLayout.height / 2 + 10}
                  textAnchor="middle"
                  dominantBaseline="middle"
                  className="stadium-map__zone-price"
                >
                  {pricing.effectivePrice.toFixed(2)} €
                </text>
              )}
              {/* Grada: color azul fijo, la ocupación se indica con una luz sutil parpadeante en
                  vez de teñir todo el bloque (semáforo verde/naranja/rojo del concierto se queda
                  igual). */}
              {!isConcert && (
                <g transform={`translate(${zoneLayout.x + zoneLayout.width - 10}, ${zoneLayout.y + 10})`}>
                  <circle r={7} className={`stadium-map__zone-light-halo stadium-map__zone-light--${tier}`} />
                  <circle r={3} className={`stadium-map__zone-light-core stadium-map__zone-light--${tier}`} />
                </g>
              )}
            </g>
          )
        })}
      </svg>

      {unmapped.length > 0 && (
        <ul className="stadium-map__fallback">
          {unmapped.map((zone) => (
            <li key={zone.zoneId}>
              <button type="button" disabled={!interactive} onClick={() => onSelect?.(zone.zoneId)}>
                {zone.name} ({zone.availableToSell} libres)
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
