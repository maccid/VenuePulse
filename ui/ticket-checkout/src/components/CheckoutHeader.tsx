import type { EventSummary, EventType } from '../types'

interface Props {
  event: EventSummary | undefined
}

const TYPE_LABEL: Record<EventType, string> = {
  Match: 'Partido',
  Concert: 'Concierto',
}

// El botón "Volver al catálogo" avisa al shell con un CustomEvent en vez de navegar por su
// cuenta: el Web Component vive en light DOM dentro de la página de shell (ver ADR 0004), así
// que no puede cambiar el estado de React del host. CheckoutPanel (shell) lo escucha y hace el
// onBack; en modo standalone (sin shell), main.tsx lo escucha para quitar el eventId de la URL.
export default function CheckoutHeader({ event }: Props) {
  const handleBack = () => {
    window.dispatchEvent(new CustomEvent('back-to-catalog', { bubbles: true, composed: true }))
  }

  if (!event) return null

  const date = new Date(event.startsAtUtc)
  const day = date.getDate()
  const month = date.toLocaleDateString('es-ES', { month: 'short' }).replace('.', '').toUpperCase()
  const weekday = date.toLocaleDateString('es-ES', { weekday: 'short' }).replace('.', '')
  const time = date.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' })

  return (
    <header className="checkout-header">
      <button type="button" className="checkout-header__back" onClick={handleBack}>
        ← Volver al catálogo
      </button>

      <div className="checkout-header__card">
        <div className="checkout-header__date">
          <span className="checkout-header__month">{month}</span>
          <span className="checkout-header__day">{day}</span>
        </div>

        <div className="checkout-header__info">
          <p className="checkout-header__meta">
            {weekday} · {time} · {TYPE_LABEL[event.type]}
          </p>
          <h3 className="checkout-header__title">{event.name}</h3>
          <p className="checkout-header__venue">{event.venueName}</p>
        </div>
      </div>
    </header>
  )
}
