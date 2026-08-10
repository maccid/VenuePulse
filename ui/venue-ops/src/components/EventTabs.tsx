import type { EventSummary } from '../types'

export const NEW_EVENT_TAB_ID = '__new__'

const STATUS_BADGE: Partial<Record<EventSummary['status'], string>> = {
  SalesClosed: 'Venta cerrada',
  Cancelled: 'Cancelado',
}

interface Props {
  events: EventSummary[]
  activeId: string
  onSelect: (id: string) => void
}

// Estilo tabs de Chrome: una pestaña por evento + una pestaña "+" fija al final para el alta. Sin
// filtro de "pasados" — Operaciones ve siempre todo (a diferencia del catálogo de shell), ver ADR
// 0022.
export default function EventTabs({ events, activeId, onSelect }: Props) {
  return (
    <div className="event-tabs" role="tablist" aria-label="Eventos">
      {events.map((ev) => {
        const badge = STATUS_BADGE[ev.status]
        return (
          <button
            key={ev.id}
            role="tab"
            aria-selected={ev.id === activeId}
            className={`event-tabs__tab${ev.id === activeId ? ' event-tabs__tab--active' : ''}`}
            onClick={() => onSelect(ev.id)}
          >
            <span className="event-tabs__name">{ev.name}</span>
            {badge && <span className="event-tabs__badge">{badge}</span>}
          </button>
        )
      })}

      <button
        role="tab"
        aria-selected={activeId === NEW_EVENT_TAB_ID}
        className={`event-tabs__tab event-tabs__tab--new${activeId === NEW_EVENT_TAB_ID ? ' event-tabs__tab--active' : ''}`}
        onClick={() => onSelect(NEW_EVENT_TAB_ID)}
        aria-label="Nuevo evento"
        title="Nuevo evento"
      >
        +
      </button>
    </div>
  )
}
