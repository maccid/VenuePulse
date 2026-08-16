import { useEffect, useState } from 'react'
import type { BuyerData, CartLine, DraftLine, OrderStatus, PaymentMethod, TicketType } from '@venue/core/domain'
import { formatCountdown, useCountdown } from '../../shared/useCountdown'
import { ReloadIcon, TicketReservationIcon, TrashIcon } from '../../shared/icons'
import DraftCart from './DraftCart'
import ReservedCartSummary from './ReservedCartSummary'

interface Props {
  ticketTypes: TicketType[]
  draft: DraftLine[]
  cart: CartLine[]
  buyer: BuyerData
  isReserving: boolean
  isPaying: boolean
  isCancelling: boolean
  orderStatus: OrderStatus | null
  orderRejectionReason: string | null
  onBuyerChange: (buyer: BuyerData) => void
  onRemoveDraftLine: (zoneId: string, ticketTypeId: string) => void
  onResetPurchase: () => void
  onReservar: () => void
  onSubmitPayment: (method: PaymentMethod) => void
  onCancelOrder: () => void
}

function earliestActiveExpiry(lines: CartLine[]): string | null {
  const deadlines = lines.filter((l) => l.status !== 'Rejected' && l.expiresAtUtc).map((l) => l.expiresAtUtc as string)
  if (deadlines.length === 0) return null
  return deadlines.reduce((earliest, d) => (new Date(d) < new Date(earliest) ? d : earliest))
}

export default function CartPanel({
  ticketTypes,
  draft,
  cart,
  buyer,
  isReserving,
  isPaying,
  isCancelling,
  orderStatus,
  orderRejectionReason,
  onBuyerChange,
  onRemoveDraftLine,
  onResetPurchase,
  onReservar,
  onSubmitPayment,
  onCancelOrder,
}: Props) {
  const [confirmingCancel, setConfirmingCancel] = useState(false)
  const hasReservation = orderStatus !== null
  const allResolved = orderStatus === 'Completed' || orderStatus === 'Rejected'
  const countdownSeconds = useCountdown(earliestActiveExpiry(cart))
  const reservationExpired = countdownSeconds !== null && countdownSeconds <= 0
  const payable = orderStatus === 'AwaitingPayment'
  const paying = isPaying || orderStatus === 'Confirming'
  const canCancel = payable && !isPaying && !confirmingCancel
  const showCancelRow = !allResolved && !reservationExpired && (payable || paying)

  useEffect(() => {
    if (!hasReservation) setConfirmingCancel(false)
  }, [hasReservation])

  return (
    <aside className="ticket-checkout cart-panel">
      <h3 className="cart-panel__title">
        <TicketReservationIcon />
        <span>{hasReservation ? 'Tu compra' : 'Tu reserva'}</span>

        {showCancelRow && (countdownSeconds !== null && countdownSeconds > 0 || canCancel) && (
          <span className="cart-panel__title-actions">
            {countdownSeconds !== null && countdownSeconds > 0 && (
              <span className="cart-panel__countdown">Reserva expira en {formatCountdown(countdownSeconds)}</span>
            )}
            {canCancel && (
              <button
                type="button"
                className="cart-panel__remove"
                aria-label="Cancelar reserva"
                title="Cancelar reserva"
                onClick={() => setConfirmingCancel(true)}
              >
                <TrashIcon />
              </button>
            )}
          </span>
        )}

        {allResolved && (
          <span className="cart-panel__title-actions">
            <button
              type="button"
              className="cart-panel__remove"
              aria-label="Reiniciar compra"
              title="Reiniciar compra"
              onClick={onResetPurchase}
            >
              <ReloadIcon />
            </button>
          </span>
        )}
      </h3>

      {!hasReservation && (
        <DraftCart
          ticketTypes={ticketTypes}
          draft={draft}
          isReserving={isReserving}
          onRemoveDraftLine={onRemoveDraftLine}
          onReservar={onReservar}
        />
      )}

      {hasReservation && (
        <ReservedCartSummary
          cart={cart}
          buyer={buyer}
          isPaying={isPaying}
          isCancelling={isCancelling}
          orderStatus={orderStatus}
          orderRejectionReason={orderRejectionReason}
          reservationExpired={reservationExpired}
          confirmingCancel={confirmingCancel}
          onCloseCancelConfirm={() => setConfirmingCancel(false)}
          onBuyerChange={onBuyerChange}
          onSubmitPayment={onSubmitPayment}
          onCancelOrder={onCancelOrder}
        />
      )}
    </aside>
  )
}
