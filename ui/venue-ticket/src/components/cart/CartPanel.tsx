import { useEffect, useState } from 'react'
import type { BuyerData, CartLine, DraftLine, OrderStatus, PaymentMethod, TicketType } from '@venue/core/domain'
import { CancelReservationIcon, CartIcon, ReloadIcon } from '../../shared/icons'
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
  const canRequestCancel = hasReservation && !allResolved && !isPaying && !confirmingCancel

  useEffect(() => {
    if (!hasReservation) setConfirmingCancel(false)
  }, [hasReservation])

  return (
    <aside className="ticket-checkout cart-panel">
      <h3 className="cart-panel__title">
        <CartIcon />
        <span>{hasReservation ? 'Tu compra' : 'Tus Reservas'}</span>
        {allResolved && (
          <button
            type="button"
            className="cart-panel__title-action"
            onClick={onResetPurchase}
            aria-label="Reiniciar compra"
            title="Reiniciar compra"
          >
            <ReloadIcon />
          </button>
        )}
        {canRequestCancel && (
          <button
            type="button"
            className="cart-panel__title-action"
            onClick={() => setConfirmingCancel(true)}
            aria-label="Cancelar reserva"
            title="Cancelar reserva"
          >
            <CancelReservationIcon />
          </button>
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
