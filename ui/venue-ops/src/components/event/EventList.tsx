import type { EventSummary } from '@venue/core/domain'
import { EventSummaryCard } from '@venue/core/components'

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
            const badge = STATUS_BADGE[ev.status]
            const active = ev.id === activeId

            return (
              <li key={ev.id}>
                <button
                  type="button"
                  className={`flex w-full items-center gap-3 rounded-md border p-3 text-left text-text ${active ? 'border-border-strong bg-surface shadow-sm' : 'border-border bg-bg hover:bg-accent-bg'}`}
                  onClick={() => onSelect(ev.id)}
                >
                  <EventSummaryCard event={ev} />
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
