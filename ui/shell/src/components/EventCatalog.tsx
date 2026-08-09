import { useMemo, useState } from 'react'
import type { EventSummary } from '../types'
import EventCard from './EventCard'

interface Props {
  events: EventSummary[]
  onSelectEvent: (eventId: string) => void
}

function dateKey(iso: string): string {
  return new Date(iso).toISOString().slice(0, 10)
}

export default function EventCatalog({ events, onSelectEvent }: Props) {
  const [dateFilter, setDateFilter] = useState('all')

  const dateOptions = useMemo(() => {
    const unique = Array.from(new Set(events.map((ev) => dateKey(ev.startsAtUtc)))).sort()
    return unique.map((key) => ({
      key,
      label: new Date(key).toLocaleDateString('es-ES', { day: 'numeric', month: 'long' }),
    }))
  }, [events])

  const filtered = dateFilter === 'all' ? events : events.filter((ev) => dateKey(ev.startsAtUtc) === dateFilter)

  return (
    <section className="mt-8 flex flex-col gap-6">
      <label className="flex max-w-xs flex-col gap-1 text-sm font-semibold text-text-muted">
        Fechas
        <select
          className="rounded-md border border-border-strong bg-surface px-3 py-2 font-normal text-text"
          value={dateFilter}
          onChange={(e) => setDateFilter(e.target.value)}
        >
          <option value="all">Todas las fechas</option>
          {dateOptions.map((opt) => (
            <option key={opt.key} value={opt.key}>
              {opt.label}
            </option>
          ))}
        </select>
      </label>

      <div>
        <h2 className="mb-3">Próximos eventos</h2>

        {filtered.length === 0 ? (
          <p className="p-5 text-center text-sm text-text-muted">No hay eventos para esa fecha.</p>
        ) : (
          <ul className="divide-y divide-border overflow-hidden rounded-lg border border-border bg-surface">
            {filtered.map((ev) => (
              <EventCard key={ev.id} event={ev} onSelect={onSelectEvent} />
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
