import { useState } from 'react'
import type { BuyerData, CartLine, OrderStatus, PaymentMethod } from '@venue/core/domain'
import { SagaTimeline } from './steps/SagaTimeline'
import { groupCartLines } from './cartGrouping'
import { CartBlock } from './steps/CartBlock'
import BusyBlock from './steps/BusyBlock'
import ResultBlock from './steps/ResultBlock'
import CartSummaryLines from './steps/CartSummaryLines'
import ConfirmedTicketsSummary from './steps/ConfirmedTicketsSummary'
import PaymentForm from '../PaymentForm'
import CancelConfirmDialog from './steps/CancelConfirmDialog'

interface Props {
  cart: CartLine[]
  buyer: BuyerData
  isPaying: boolean
  isCancelling: boolean
  orderId: string | null
  orderStatus: OrderStatus | null
  orderRejectionReason: string | null
  reservationExpired: boolean
  confirmingCancel: boolean
  onCloseCancelConfirm: () => void
  onBuyerChange: (buyer: BuyerData) => void
  onSubmitPayment: (method: PaymentMethod) => void
  onCancelOrder: () => void
}

export default function ReservedCartSummary({
  cart,
  buyer,
  isPaying,
  isCancelling,
  orderId,
  orderStatus,
  orderRejectionReason,
  reservationExpired,
  confirmingCancel,
  onCloseCancelConfirm,
  onBuyerChange,
  onSubmitPayment,
  onCancelOrder,
}: Props) {
  const [summaryOpen, setSummaryOpen] = useState(false)

  const activeLines = cart.filter((l) => l.status !== 'Rejected')
  const cartTotal = activeLines.reduce((sum, l) => sum + l.unitPrice, 0)
  const groups = groupCartLines(cart)
  const rejectedLine = cart.find((l) => l.status === 'Rejected')
  const timelineReason = orderRejectionReason ?? rejectedLine?.rejectionReason ?? null
  const allResolved = orderStatus === 'Completed' || orderStatus === 'Rejected'
  const paying = isPaying || orderStatus === 'Confirming'
  const completed = orderStatus === 'Completed'
  const failed = orderStatus === 'Rejected' || reservationExpired

  return (
    <>
      {orderStatus === 'Reserving' ? (
        <BusyBlock label="Reservando" />
      ) : completed ? (
        <ConfirmedTicketsSummary
          groups={groups}
          activeCount={activeLines.length}
          cartTotal={cartTotal}
          groupId={orderId}
        />
      ) : failed ? (
        <ResultBlock kind="error" title="Tu compra ha sido rechazada.">
          <SagaTimeline rejectionReason={timelineReason} />
        </ResultBlock>
      ) : paying ? (
        <BusyBlock label="Pagando" />
      ) : (
        <>
          <CartSummaryLines
            groups={groups}
            activeCount={activeLines.length}
            cartTotal={cartTotal}
            label="entradas reservadas"
            summaryOpen={summaryOpen}
            onToggle={() => setSummaryOpen((open) => !open)}
          />

          <CartBlock>
            <PaymentForm
              total={cartTotal}
              disabled={isPaying}
              buyer={buyer}
              onBuyerChange={onBuyerChange}
              onSubmit={onSubmitPayment}
            />
          </CartBlock>

          <SagaTimeline rejectionReason={timelineReason} />
        </>
      )}

      {confirmingCancel && !allResolved && !reservationExpired && (
        <CancelConfirmDialog
          isCancelling={isCancelling}
          onDismiss={onCloseCancelConfirm}
          onConfirm={() => {
            onCloseCancelConfirm()
            onCancelOrder()
          }}
        />
      )}
    </>
  )
}
