interface Props {
  eventId: string
  onBack: () => void
}

export default function CheckoutPanel({ eventId, onBack }: Props) {
  return (
    <section className="checkout-panel">
      <button className="back-link" onClick={onBack}>
        ← Volver al catálogo
      </button>
      {/* Micro frontend independiente: su propio bundle, su propio fetch de tipos de entrada
          y zonas, montado aquí como Web Component (ver ADR 0004). El nombre del comprador se
          captura dentro del propio wizard de compra (paso final), no aquí (ver ADR 0017). */}
      <ticket-checkout-app event-id={eventId} />
    </section>
  )
}
