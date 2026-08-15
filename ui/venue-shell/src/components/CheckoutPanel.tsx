import { useEffect } from 'react'

interface Props {
  eventId: string
  onBack: () => void
}

export default function CheckoutPanel({ eventId, onBack }: Props) {
  useEffect(() => {
    const handleBackToCatalog = () => onBack()
    window.addEventListener('back-to-catalog', handleBackToCatalog)
    return () => window.removeEventListener('back-to-catalog', handleBackToCatalog)
  }, [onBack])

  return (
    <section className="checkout-panel">
      <ticket-checkout-app event-id={eventId} />
    </section>
  )
}
