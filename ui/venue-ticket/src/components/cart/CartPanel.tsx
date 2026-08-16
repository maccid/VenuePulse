import type { BuyerData, CartLine, DraftLine, OrderStatus, PaymentMethod, TicketType } from '@venue/core/domain'
import { TicketReservationIcon } from '../../shared/icons'
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
  const hasReservation = orderStatus !== null

  return (
    <aside className="ticket-checkout cart-panel">
      <h3 className="cart-panel__title">
        <TicketReservationIcon />
        <span>{hasReservation ? 'Tu compra' : 'Tu reserva'}</span>
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
          onBuyerChange={onBuyerChange}
          onSubmitPayment={onSubmitPayment}
          onResetPurchase={onResetPurchase}
          onCancelOrder={onCancelOrder}
        />
      )}
    </aside>
  )
}
