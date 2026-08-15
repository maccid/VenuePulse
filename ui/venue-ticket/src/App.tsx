import PurchaseFlow from './components/PurchaseFlow'
import CheckoutHeader from './components/CheckoutHeader'
import { useEventCatalog } from './hooks/useEventCatalog'
import { useCheckoutCart } from './hooks/useCheckoutCart'
import './App.css'

interface Props {
  eventId: string
  holder: string
}

export default function App({ eventId, holder }: Props) {
  const { event, ticketTypes, zones } = useEventCatalog(eventId)
  const {
    draft,
    cart,
    buyer,
    isReserving,
    isPaying,
    orderStatus,
    orderRejectionReason,
    setBuyer,
    addToDraft,
    removeDraftLine,
    resetPurchase,
    reservar,
    submitPayment,
  } = useCheckoutCart(eventId, holder, ticketTypes, zones)

  return (
    <div className="ticket-checkout">
      <CheckoutHeader event={event} />

      {event && event.status !== 'OnSale' ? (
        <p className="ticket-checkout__notice">Este evento ya no está a la venta.</p>
      ) : (
        <PurchaseFlow
          ticketTypes={ticketTypes}
          zones={zones}
          draft={draft}
          cart={cart}
          buyer={buyer}
          isReserving={isReserving}
          isPaying={isPaying}
          orderStatus={orderStatus}
          orderRejectionReason={orderRejectionReason}
          onBuyerChange={setBuyer}
          onAddToDraft={addToDraft}
          onRemoveDraftLine={removeDraftLine}
          onResetPurchase={resetPurchase}
          onReservar={reservar}
          onSubmitPayment={submitPayment}
        />
      )}
    </div>
  )
}
