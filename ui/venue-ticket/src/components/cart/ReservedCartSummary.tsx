import { useState } from 'react'
import type { BuyerData, CartLine, OrderStatus, PaymentMethod } from '@venue/core/domain'
import { SagaTimeline } from './SagaTimeline'
import { ChevronDownIcon } from '../../shared/icons'
import PaymentForm from '../PaymentForm'
import { CartGroupRow } from './CartGroupRow'
import { groupCartLines } from './cartGrouping'
import { CartBlock } from './CartBlock'
import { formatCountdown, useCountdown } from '../../shared/useCountdown'

interface Props {
  cart: CartLine[]
  buyer: BuyerData
  isPaying: boolean
  isCancelling: boolean
  orderStatus: OrderStatus | null
  orderRejectionReason: string | null
  confirmingCancel: boolean
  onCloseCancelConfirm: () => void
  onBuyerChange: (buyer: BuyerData) => void
  onSubmitPayment: (method: PaymentMethod) => void
  onCancelOrder: () => void
}

function earliestActiveExpiry(lines: CartLine[]): string | null {
  const deadlines = lines.filter((l) => l.status !== 'Rejected' && l.expiresAtUtc).map((l) => l.expiresAtUtc as string)
  if (deadlines.length === 0) return null
  return deadlines.reduce((earliest, d) => (new Date(d) < new Date(earliest) ? d : earliest))
}

type Phase = 'confirming' | 'awaiting-payment' | 'cancel-confirm' | 'completed' | 'rejected' | 'expired'

export default function ReservedCartSummary({
  cart,
  buyer,
  isPaying,
  isCancelling,
  orderStatus,
  orderRejectionReason,
  confirmingCancel,
  onCloseCancelConfirm,
  onBuyerChange,
  onSubmitPayment,
  onCancelOrder,
}: Props) {
  const [summaryOpen, setSummaryOpen] = useState(false)
  const countdown = useCountdown(earliestActiveExpiry(cart))
  const reservationExpired = countdown !== null && countdown <= 0

  const activeLines = cart.filter((l) => l.status !== 'Rejected')
  const cartTotal = activeLines.reduce((sum, l) => sum + l.unitPrice, 0)
  const confirmedTickets = cart.filter((l) => l.status === 'Confirmed' || l.status === 'CheckedIn')
  const groups = groupCartLines(cart)
  const rejectedLine = cart.find((l) => l.status === 'Rejected')
  const timelineReason = orderRejectionReason ?? rejectedLine?.rejectionReason ?? null

  // Un único bloque activo a la vez — la verdad del backend (Completed/Rejected) manda siempre
  // sobre el heurístico local de expiración y sobre estar confirmando la cancelación.
  const phase: Phase =
    orderStatus === 'Completed'
      ? 'completed'
      : orderStatus === 'Rejected'
        ? 'rejected'
        : confirmingCancel
          ? 'cancel-confirm'
          : reservationExpired
            ? 'expired'
            : orderStatus === 'AwaitingPayment'
              ? 'awaiting-payment'
              : 'confirming'

  return (
    <>
      {summaryOpen && (
        <ul className="cart-panel__lines">
          {groups.map((group) => (
            <CartGroupRow key={group.key} group={group} />
          ))}
        </ul>
      )}

      {activeLines.length > 0 && phase !== 'expired' && (
        <>
          <p className="cart-panel__total">
            <span className="cart-panel__quota">
              {activeLines.length} {phase === 'awaiting-payment' ? 'entradas reservadas' : 'entradas compradas'}
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

      {(phase === 'confirming' || phase === 'awaiting-payment') && countdown !== null && countdown > 0 && (
        <p className="cart-panel__countdown">Reserva expira en {formatCountdown(countdown)}</p>
      )}

      {phase === 'confirming' && (
        <CartBlock>
          <p className="cart-panel__block-busy">
            Confirmando disponibilidad
            <span className="cart-panel__reserve-dots" aria-hidden="true">
              <span>.</span>
              <span>.</span>
              <span>.</span>
            </span>
          </p>
        </CartBlock>
      )}

      {phase === 'awaiting-payment' && (
        <CartBlock>
          <PaymentForm total={cartTotal} disabled={isPaying} buyer={buyer} onBuyerChange={onBuyerChange} onSubmit={onSubmitPayment} />
        </CartBlock>
      )}

      {phase === 'cancel-confirm' && (
        <CartBlock>
          <p className="cart-panel__block-title">¿Cancelar la reserva?</p>
          <p className="cart-panel__block-hint">Se liberará el aforo reservado y no podrás deshacerlo.</p>
          <div className="cart-panel__block-actions">
            <button type="button" disabled={isCancelling} onClick={onCloseCancelConfirm}>
              Volver
            </button>
            <button type="button" className="cart-panel__cancel" disabled={isCancelling} onClick={onCancelOrder}>
              {isCancelling ? 'Cancelando…' : 'Sí, cancelar'}
            </button>
          </div>
        </CartBlock>
      )}

      {phase === 'completed' && (
        <CartBlock>
          <p className="cart-panel__done">✔ Compra completada.</p>
        </CartBlock>
      )}

      {phase === 'rejected' && (
        <CartBlock>
          <p className="cart-panel__done cart-panel__done--error">La compra no se pudo completar.</p>
        </CartBlock>
      )}

      {phase === 'expired' && (
        <CartBlock>
          <p className="cart-panel__done cart-panel__done--error">La reserva expiró.</p>
        </CartBlock>
      )}

      <SagaTimeline rejectionReason={timelineReason} />
    </>
  )
}
