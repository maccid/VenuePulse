import type { EventSummary } from '../types'
import EventCard from './EventCard'

interface Props {
  events: EventSummary[]
  holder: string
  onHolderChange: (holder: string) => void
  onSelectEvent: (eventId: string) => void
}

export default function EventCatalog({ events, holder, onHolderChange, onSelectEvent }: Props) {
  return (
    <section className="events">
      <div className="events__header">
        <h2>Próximos eventos</h2>
        <label className="holder-field" htmlFor="holder">
          Tu nombre
          <input id="holder" value={holder} onChange={(e) => onHolderChange(e.target.value)} />
        </label>
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
