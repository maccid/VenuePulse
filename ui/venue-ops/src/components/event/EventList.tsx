import type { EventSummary, EventType } from '../../shared/types'

interface Props {
  events: EventSummary[]
  activeId: string | null
  onSelect: (id: string) => void
  onCreate: () => void
}

const STATUS_BADGE: Partial<Record<EventSummary['status'], string>> = {
  SalesClosed: 'Venta cerrada',
  Cancelled: 'Cancelado',
}

const TYPE_LABEL: Record<EventType, string> = {
  Match: 'Partido',
  Concert: 'Concierto',
}

export default function EventList({ events, activeId, onSelect, onCreate }: Props) {
  const sorted = [...events].sort((a, b) => b.startsAtUtc.localeCompare(a.startsAtUtc))

  return (
    <section className="flex flex-col gap-3">
      <h2 className="mb-0">Eventos</h2>
      <button type="button" className="mb-1 w-full" onClick={onCreate}>
        Crear nuevo evento
      </button>

      {sorted.length === 0 ? (
        <p className="m-0 text-sm text-text-muted">No hay eventos todavía.</p>
      ) : (
        <ul className="m-0 flex list-none flex-col gap-3 p-0">
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
                  className={`flex w-full items-center gap-3 rounded-md border p-3 text-left text-text ${active ? 'border-accent bg-surface shadow-sm' : 'border-border bg-transparent hover:bg-accent-bg'}`}
                  onClick={() => onSelect(ev.id)}
                >
                  <span className="flex w-12 flex-shrink-0 flex-col items-center rounded-md bg-accent-bg py-1.5 text-accent">
                    <span className="text-xs font-bold uppercase">{month}</span>
                    <span className="text-lg font-bold leading-[1.1]">{day}</span>
                  </span>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="text-xs text-text-muted">
                      {weekday} · {time} · {TYPE_LABEL[ev.type]}
                    </span>
                    <span className="truncate font-[650] text-text">{ev.name}</span>
                    <span className="truncate text-sm text-text-muted">{ev.venueName}</span>
                  </span>
                  {badge && (
                    <span
                      className={`flex-shrink-0 rounded-full px-2 py-0.5 text-xs ${ev.status === 'Cancelled' ? 'bg-danger text-text-on-accent' : 'bg-border-strong text-text'}`}
                    >
                      {badge}
                    </span>
                  )}
                </button>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
