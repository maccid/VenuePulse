import type { CartLine, PaymentMethod } from '../types'
import { MAX_TICKETS_PER_PURCHASE } from '../types'
import { SagaTimeline } from '../SagaTimeline'
import PaymentForm from './PaymentForm'

interface Props {
  cart: CartLine[]
  holder: string
  isPaying: boolean
  onSubmitPayment: (method: PaymentMethod) => void
}

export default function CartPanel({ cart, holder, isPaying, onSubmitPayment }: Props) {
  const activeLines = cart.filter((l) => l.status !== 'Rejected')
  const total = activeLines.reduce((sum, l) => sum + l.unitPrice, 0)
  const awaitingPayment = cart.some((l) => l.status === 'AwaitingPayment')
  const allResolved = cart.length > 0 && cart.every((l) => l.status === 'Confirmed' || l.status === 'Rejected' || l.status === 'CheckedIn')

  return (
    <aside className="cart-panel">
      <h3>Tu compra</h3>

      {cart.length === 0 ? (
        <p className="cart-panel__empty">Elige una zona en el plano para añadir entradas.</p>
      ) : (
        <>
          <ul className="cart-panel__lines">
            {cart.map((line) => (
              <li key={line.ticketId} className="cart-panel__line">
                <div className="cart-panel__line-header">
                  <span>
                    {line.zoneName} · {line.ticketTypeName}
                  </span>
                  <span>{line.unitPrice.toFixed(2)} €</span>
                </div>
                <SagaTimeline status={line.status} rejectionReason={line.rejectionReason} />
                {line.status === 'Confirmed' && (
                  <p className="ticket-id-hint">
                    ID (para escanear en <em>venue-ops</em>): <code>{line.ticketId}</code>
                  </p>
                )}
              </li>
            ))}
          </ul>

          <p className="cart-panel__total">
            Total <strong>{total.toFixed(2)} €</strong>
          </p>
          <p className="cart-panel__quota">
            {activeLines.length} / {MAX_TICKETS_PER_PURCHASE} entradas
          </p>

          {awaitingPayment && <PaymentForm holder={holder} disabled={isPaying} onSubmit={onSubmitPayment} />}

          {allResolved && !awaitingPayment && (
            <p className="cart-panel__done">
              {activeLines.length > 0 ? '✔ Compra completada.' : 'La compra no se pudo completar.'}
            </p>
          )}
        </>
      )}
    </aside>
  )
}
