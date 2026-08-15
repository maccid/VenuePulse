import type { EventSummary } from '../domain'
import { eventTypeLabel, formatEventDateTime } from '../domain'

interface Props {
  event: EventSummary
}

export default function EventSummaryCard({ event }: Props) {
  const { day, month, weekday, time } = formatEventDateTime(event.startsAtUtc)

  return (
    <>
      <span className="flex w-14 shrink-0 flex-col items-center rounded-md bg-accent py-1.5 text-text-on-accent">
        <span className="text-xs font-bold uppercase">{month}</span>
        <span className="text-lg leading-none font-bold">{day}</span>
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-xs text-text-muted">
          {weekday} · {time} · {eventTypeLabel(event.type)}
        </span>
        <span className="block truncate font-semibold text-text">{event.name}</span>
        <span className="block truncate text-sm text-text-muted">{event.venueName}</span>
      </span>
    </>
  )
}
