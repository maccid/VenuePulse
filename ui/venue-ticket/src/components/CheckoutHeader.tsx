import type { EventSummary } from '@venue/core/domain'
import { EventSummaryCard } from '@venue/core/components'

interface Props {
  event: EventSummary | undefined
}

export default function CheckoutHeader({ event }: Props) {
  const handleBack = () => {
    window.dispatchEvent(new CustomEvent('back-to-catalog', { bubbles: true, composed: true }))
  }

  if (!event) return null

  return (
    <header className="flex items-center justify-between gap-3.5">
      <div className="flex min-w-0 items-center gap-3.5">
        <EventSummaryCard event={event} />
      </div>

      <button type="button" className="shrink-0" onClick={handleBack}>
        ← Volver al catálogo
      </button>
    </header>
  )
}
