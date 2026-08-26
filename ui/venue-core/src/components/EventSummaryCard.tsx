import type { ReactNode } from 'react'
import type { EventSummary } from '../domain'
import { eventTypeLabel, formatEventDateTime } from '../domain'

interface Badge {
  label: string
  variant?: 'default' | 'danger'
}

interface Props {
  event: EventSummary
  action?: ReactNode
  badge?: Badge
}

export default function EventSummaryCard({ event, action, badge }: Props) {
  const { day, month, weekday, time } = formatEventDateTime(event.startsAtUtc)

  return (
    <>
      <span className="flex w-14 shrink-0 flex-col items-center rounded-md bg-accent py-1.5 text-text-on-accent">
        <span className="text-xs font-bold uppercase">{month}</span>
        <span className="text-lg leading-none font-bold">{day}</span>
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex items-center gap-1.5 text-xs text-text-muted">
          <span>{weekday} · {time} · {eventTypeLabel(event.type)}</span>
          {badge && (
            <span
              className={`ml-auto flex-shrink-0 rounded-full px-2 py-0.5 text-xs ${badge.variant === 'danger' ? 'bg-danger text-text-on-accent' : 'bg-border-strong text-text'}`}
            >
              {badge.label}
            </span>
          )}
        </span>
        <span className="block truncate font-semibold text-text">{event.name}</span>
        <span className="block truncate text-sm text-text-muted">{event.venueName}</span>
      </span>
      {action}
    </>
  )
}
