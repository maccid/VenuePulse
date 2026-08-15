import type { EventSummary, EventType } from '../types'

interface Props {
  event: EventSummary
  onSelect: (eventId: string) => void
}

const TYPE_LABEL: Record<EventType, string> = {
  Match: 'Partido',
  Concert: 'Concierto',
}

export default function EventCard({ event, onSelect }: Props) {
  const date = new Date(event.startsAtUtc)
  const day = date.getDate()
  const month = date.toLocaleDateString('es-ES', { month: 'short' }).replace('.', '').toUpperCase()
  const weekday = date.toLocaleDateString('es-ES', { weekday: 'short' }).replace('.', '')
  const time = date.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })

  return (
    <li className="flex items-center gap-4 p-4">
      <div className="flex w-14 shrink-0 flex-col items-center rounded-md bg-accent-bg py-1.5 text-accent">
        <span className="text-xs font-bold uppercase">{month}</span>
        <span className="text-lg leading-none font-bold">{day}</span>
      </div>

      <div className="min-w-0 flex-1">
        <p className="text-xs text-text-muted">
          {weekday} · {time} · {TYPE_LABEL[event.type]}
        </p>
        <h3 className="truncate">{event.name}</h3>
        <p className="truncate text-sm text-text-muted">{event.venueName}</p>
      </div>

      <button
        className="flex shrink-0 items-center gap-1 px-4 py-2 text-sm"
        onClick={() => onSelect(event.id)}
      >
        Entradas
        <span aria-hidden="true">→</span>
      </button>
    </li>
  )
}
