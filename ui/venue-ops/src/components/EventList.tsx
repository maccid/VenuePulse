import type { EventSummary, EventType } from '../types'

interface Props {
  events: EventSummary[]
  activeId: string | null
  onSelect: (id: string) => void
}

const STATUS_BADGE: Partial<Record<EventSummary['status'], string>> = {
  SalesClosed: 'Venta cerrada',
  Cancelled: 'Cancelado',
}

const TYPE_LABEL: Record<EventType, string> = {
  Match: 'Partido',
  Concert: 'Concierto',
}

// Listado por fechas al estilo del catálogo de `shell` (ADR 0022: Operaciones ve siempre todo,
// sin filtro de "pasados") — sustituye a las pestañas estilo Chrome del Bloque 18.
export default function EventList({ events, activeId, onSelect }: Props) {
  const sorted = [...events].sort((a, b) => a.startsAtUtc.localeCompare(b.startsAtUtc))

  return (
    <section className="event-list">
      <h2 className="event-list__title">Eventos</h2>

      {sorted.length === 0 ? (
        <p className="event-list__empty">No hay eventos todavía.</p>
      ) : (
        <ul className="event-list__items">
          {sorted.map((ev) => {
            const date = new Date(ev.startsAtUtc)
            const day = date.getDate()
            const month = date.toLocaleDateString('es-ES', { month: 'short' }).replace('.', '').toUpperCase()
            const weekday = date.toLocaleDateString('es-ES', { weekday: 'short' }).replace('.', '')
            const time = date.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })
            const badge = STATUS_BADGE[ev.status]
            const active = ev.id === activeId

            return (
              <li key={ev.id}>
                <button
                  type="button"
                  className={`event-list__item${active ? ' event-list__item--active' : ''}`}
                  onClick={() => onSelect(ev.id)}
                >
                  <span className="event-list__date">
                    <span className="event-list__month">{month}</span>
                    <span className="event-list__day">{day}</span>
                  </span>
                  <span className="event-list__body">
                    <span className="event-list__meta">
                      {weekday} · {time} · {TYPE_LABEL[ev.type]}
                    </span>
                    <span className="event-list__name">{ev.name}</span>
                    <span className="event-list__venue">{ev.venueName}</span>
                  </span>
                  {badge && <span className={`event-list__badge event-list__badge--${ev.status.toLowerCase()}`}>{badge}</span>}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
