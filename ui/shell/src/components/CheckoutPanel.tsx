import { useEffect } from 'react'

interface Props {
  eventId: string
  onBack: () => void
}

// El botón "Volver al catálogo" vive dentro del micro frontend (CheckoutHeader en
// ticket-checkout), que no puede cambiar el estado de React del host — por eso avisa con un
// CustomEvent 'back-to-catalog' (bubbles + composed, ver ADR 0023) y aquí solo se escucha y se
// reenvía como onBack. El Web Component se monta sin Shadow DOM (ADR 0004), así que el evento
// llega hasta window sin trampa.
export default function CheckoutPanel({ eventId, onBack }: Props) {
  useEffect(() => {
    const handleBackToCatalog = () => onBack()
    window.addEventListener('back-to-catalog', handleBackToCatalog)
    return () => window.removeEventListener('back-to-catalog', handleBackToCatalog)
  }, [onBack])

  return (
    <section className="checkout-panel">
      {/* Micro frontend independiente: su propio bundle, su propio fetch de tipos de entrada
          y zonas, montado aquí como Web Component (ver ADR 0004). El nombre del comprador se
          captura dentro del propio wizard de compra (paso final), no aquí (ver ADR 0017). */}
      <ticket-checkout-app event-id={eventId} />
    </section>
  )
}
