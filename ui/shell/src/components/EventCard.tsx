import type { EventSummary } from '../types'

interface Props {
  event: EventSummary
  onSelect: (eventId: string) => void
}

export default function EventCard({ event, onSelect }: Props) {
  return (
    <li key={event.id} className="event-card card">
      <div>
        <h3>{event.name}</h3>
        <p>
          {event.venueName} · {new Date(event.startsAtUtc).toLocaleString('es-ES')}
        </p>
      </div>
      <button onClick={() => onSelect(event.id)}>Comprar entrada</button>
    </li>
  )
}
