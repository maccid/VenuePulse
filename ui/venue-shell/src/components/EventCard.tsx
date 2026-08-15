import type { EventSummary } from '@venue/core/domain'
import { EventSummaryCard } from '@venue/core/components'

interface Props {
  event: EventSummary
  onSelect: (eventId: string) => void
}

export default function EventCard({ event, onSelect }: Props) {
  return (
    <li className="flex items-center gap-4 p-4">
      <EventSummaryCard event={event} />

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
