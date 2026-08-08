interface Props {
  eventId: string
  holder: string
  onBack: () => void
}

export default function CheckoutPanel({ eventId, holder, onBack }: Props) {
  return (
    <section className="checkout-panel">
      <button className="back-link" onClick={onBack}>
        ← Volver al catálogo
      </button>
      {/* Micro frontend independiente: su propio bundle, su propio fetch de tipos de entrada
          y zonas, montado aquí como Web Component (ver ADR 0004). */}
      <ticket-checkout-app event-id={eventId} holder={holder} />
    </section>
  )
}
