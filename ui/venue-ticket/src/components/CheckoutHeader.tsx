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
    <header className="checkout-header">
      <div className="checkout-header__card">
        <EventSummaryCard event={event} />
      </div>

      <button type="button" className="checkout-header__back" onClick={handleBack}>
        ← Volver al catálogo
      </button>
    </header>
  )
}
