import type { BuyerData, CartLine, DraftLine, OrderStatus, PaymentMethod, TicketType } from '@venue/core/domain'
import { formatCountdown, useCountdown } from '../../shared/useCountdown'
import { CartIcon, ReloadIcon } from '../../shared/icons'
import DraftCart from './DraftCart'
import ReservedCartSummary from './ReservedCartSummary'

interface Props {
  ticketTypes: TicketType[]
  draft: DraftLine[]
  cart: CartLine[]
  buyer: BuyerData
  isReserving: boolean
  isPaying: boolean
  orderStatus: OrderStatus | null
  orderRejectionReason: string | null
  onBuyerChange: (buyer: BuyerData) => void
  onRemoveDraftLine: (zoneId: string, ticketTypeId: string) => void
  onResetPurchase: () => void
  onReservar: () => void
  onSubmitPayment: (method: PaymentMethod) => void
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
  orderStatus,
  orderRejectionReason,
  onBuyerChange,
  onRemoveDraftLine,
  onResetPurchase,
  onReservar,
  onSubmitPayment,
}: Props) {
  const hasReservation = orderStatus !== null
  const allResolved = orderStatus === 'Completed' || orderStatus === 'Rejected'
  const countdown = useCountdown(earliestActiveExpiry(cart))
  const reservationExpired = countdown !== null && countdown <= 0

  return (
    <aside className="ticket-checkout cart-panel">
      <h3 className="cart-panel__title">
        <CartIcon />
        <span>{hasReservation ? 'Tu compra' : 'Tus Reservas'}</span>
        {hasReservation && countdown !== null && countdown > 0 && (
          <span className="cart-panel__countdown-inline">Reserva expira en {formatCountdown(countdown)}</span>
        )}
        {allResolved && (
          <button
            type="button"
            className="cart-panel__reload"
            onClick={onResetPurchase}
            aria-label="Reiniciar compra"
            title="Reiniciar compra"
          >
            <ReloadIcon />
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
          allResolved={allResolved}
          reservationExpired={reservationExpired}
          buyer={buyer}
          isPaying={isPaying}
          orderStatus={orderStatus}
          orderRejectionReason={orderRejectionReason}
          onBuyerChange={onBuyerChange}
          onSubmitPayment={onSubmitPayment}
        />
      )}
    </aside>
  )
}
