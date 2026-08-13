import { useState } from 'react'
import type { BuyerData, CartLine, OrderStatus, PaymentMethod } from '../../shared/types'
import { SagaTimeline } from './SagaTimeline'
import { ChevronDownIcon } from '../../shared/icons'
import PaymentForm from '../PaymentForm'
import { CartGroupRow } from './CartGroupRow'
import { groupCartLines } from './cartGrouping'

interface Props {
  cart: CartLine[]
  allResolved: boolean
  reservationExpired: boolean
  buyer: BuyerData
  isPaying: boolean
  orderStatus: OrderStatus | null
  orderRejectionReason: string | null
  onBuyerChange: (buyer: BuyerData) => void
  onSubmitPayment: (method: PaymentMethod) => void
}

export default function ReservedCartSummary({
  cart,
  allResolved,
  reservationExpired,
  buyer,
  isPaying,
  orderStatus,
  orderRejectionReason,
  onBuyerChange,
  onSubmitPayment,
}: Props) {
  const [summaryOpen, setSummaryOpen] = useState(false)
  const activeLines = cart.filter((l) => l.status !== 'Rejected')
  const cartTotal = activeLines.reduce((sum, l) => sum + l.unitPrice, 0)
  const awaitingPayment = orderStatus === 'AwaitingPayment'
  const confirmedTickets = cart.filter((l) => l.status === 'Confirmed' || l.status === 'CheckedIn')
  const groups = groupCartLines(cart)
  const rejectedLine = cart.find((l) => l.status === 'Rejected')
  const timelineReason = orderRejectionReason ?? rejectedLine?.rejectionReason ?? null

  return (
    <>
      {summaryOpen && (
        <ul className="cart-panel__lines">
          {groups.map((group) => (
            <CartGroupRow key={group.key} group={group} />
          ))}
        </ul>
      )}

      {activeLines.length > 0 && !reservationExpired && (
        <>
          <p className="cart-panel__total">
            <span className="cart-panel__quota">
              {activeLines.length} {awaitingPayment ? 'entradas reservadas' : 'entradas compradas'}
            </span>
            <span className="cart-panel__total-actions">
              <strong>{cartTotal.toFixed(2)} €</strong>
              <button
                type="button"
                className={`cart-panel__toggle${summaryOpen ? ' cart-panel__toggle--open' : ''}`}
                onClick={() => setSummaryOpen((open) => !open)}
                aria-expanded={summaryOpen}
                aria-label={summaryOpen ? 'Ocultar resumen de entradas' : 'Ver resumen de entradas'}
              >
                <ChevronDownIcon />
              </button>
            </span>
          </p>

          {confirmedTickets.length > 0 && (
            <div className="ticket-id-hint">
              <p>
                ID (para escanear en <em>venue-ops</em>):
              </p>
              <ul>
                {confirmedTickets.map((t) => (
                  <li key={t.ticketId}>
                    <code>{t.ticketId}</code>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </>
      )}

      {awaitingPayment && (
        <PaymentForm total={cartTotal} disabled={isPaying} buyer={buyer} onBuyerChange={onBuyerChange} onSubmit={onSubmitPayment} />
      )}

      {allResolved && !awaitingPayment && (
        <p className={`cart-panel__done${orderStatus === 'Rejected' ? ' cart-panel__done--error' : ''}`}>
          {orderStatus === 'Rejected' ? 'La compra no se pudo completar.' : '✔ Compra completada.'}
        </p>
      )}

      <SagaTimeline rejectionReason={timelineReason} />
    </>
  )
}
