import type { EventSummary } from '../types'
import EventCard from './EventCard'

interface Props {
  events: EventSummary[]
  onSelectEvent: (eventId: string) => void
}

export default function EventCatalog({ events, onSelectEvent }: Props) {
  return (
    <section className="events">
      <div className="events__header">
        <h2>Próximos eventos</h2>
      </div>

      {events.length === 0 ? (
        <p className="empty-state">No hay eventos disponibles en este momento.</p>
      ) : (
        <ul className="event-list">
          {events.map((ev) => (
            <EventCard key={ev.id} event={ev} onSelect={onSelectEvent} />
          ))}
        </ul>
      )}
    </section>
  )
}
