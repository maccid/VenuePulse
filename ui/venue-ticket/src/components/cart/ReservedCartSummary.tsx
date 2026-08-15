import { useState } from 'react'
import type { BuyerData, CartLine, OrderStatus, PaymentMethod } from '@venue/core/domain'
import { SagaTimeline } from './steps/SagaTimeline'
import { groupCartLines } from './cartGrouping'
import { CartBlock } from './steps/CartBlock'
import BusyBlock from './steps/BusyBlock'
import ResultBlock from './steps/ResultBlock'
import CancelCountdownRow from './steps/CancelCountdownRow'
import CartSummaryLines from './steps/CartSummaryLines'
import PaymentForm from '../PaymentForm'
import CancelConfirmDialog from './steps/CancelConfirmDialog'
import { formatCountdown, useCountdown } from '../../shared/useCountdown'

interface Props {
  cart: CartLine[]
  buyer: BuyerData
  isPaying: boolean
  isCancelling: boolean
  orderStatus: OrderStatus | null
  orderRejectionReason: string | null
  onBuyerChange: (buyer: BuyerData) => void
  onSubmitPayment: (method: PaymentMethod) => void
  onResetPurchase: () => void
  onCancelOrder: () => void
}

function earliestActiveExpiry(lines: CartLine[]): string | null {
  const deadlines = lines.filter((l) => l.status !== 'Rejected' && l.expiresAtUtc).map((l) => l.expiresAtUtc as string)
  if (deadlines.length === 0) return null
  return deadlines.reduce((earliest, d) => (new Date(d) < new Date(earliest) ? d : earliest))
}

export default function ReservedCartSummary({
  cart,
  buyer,
  isPaying,
  isCancelling,
  orderStatus,
  orderRejectionReason,
  onBuyerChange,
  onSubmitPayment,
  onResetPurchase,
  onCancelOrder,
}: Props) {
  const [summaryOpen, setSummaryOpen] = useState(false)
  const [confirmingCancel, setConfirmingCancel] = useState(false)
  const countdown = useCountdown(earliestActiveExpiry(cart))
  const reservationExpired = countdown !== null && countdown <= 0

  const activeLines = cart.filter((l) => l.status !== 'Rejected')
  const cartTotal = activeLines.reduce((sum, l) => sum + l.unitPrice, 0)
  const confirmedTickets = cart.filter((l) => l.status === 'Confirmed' || l.status === 'CheckedIn')
  const groups = groupCartLines(cart)
  const rejectedLine = cart.find((l) => l.status === 'Rejected')
  const timelineReason = orderRejectionReason ?? rejectedLine?.rejectionReason ?? null
  const allResolved = orderStatus === 'Completed' || orderStatus === 'Rejected'
  const paying = isPaying || orderStatus === 'Confirming'
  const payable = orderStatus === 'AwaitingPayment'
  const canCancel = payable && !isPaying
  const completed = orderStatus === 'Completed'
  const failed = orderStatus === 'Rejected' || reservationExpired

  return (
    <>
      {!allResolved && !reservationExpired && (payable || paying) && (
        <CancelCountdownRow
          countdown={countdown !== null && countdown > 0 ? formatCountdown(countdown) : null}
          canCancel={canCancel}
          onCancel={() => setConfirmingCancel(true)}
        />
      )}

      {orderStatus === 'Reserving' ? (
        <BusyBlock label="Reservando" />
      ) : completed ? (
        <>
          <CartSummaryLines
            groups={groups}
            activeCount={activeLines.length}
            cartTotal={cartTotal}
            label="entradas compradas"
            summaryOpen={summaryOpen}
            onToggle={() => setSummaryOpen((open) => !open)}
            confirmedTickets={confirmedTickets}
          />
          <ResultBlock kind="success" title="Tu compra ha sido completada." onResetPurchase={onResetPurchase} />
        </>
      ) : failed ? (
        <ResultBlock kind="error" title="Tu compra ha sido rechazada." onResetPurchase={onResetPurchase}>
          <SagaTimeline rejectionReason={timelineReason} />
        </ResultBlock>
      ) : (
        <>
          <CartSummaryLines
            groups={groups}
            activeCount={activeLines.length}
            cartTotal={cartTotal}
            label="entradas reservadas"
            summaryOpen={summaryOpen}
            onToggle={() => setSummaryOpen((open) => !open)}
            confirmedTickets={confirmedTickets}
          />

          {paying ? (
            <BusyBlock label="Pagando" />
          ) : (
            <CartBlock>
              <PaymentForm
                total={cartTotal}
                disabled={isPaying}
                buyer={buyer}
                onBuyerChange={onBuyerChange}
                onSubmit={onSubmitPayment}
              />
            </CartBlock>
          )}

          <SagaTimeline rejectionReason={timelineReason} />
        </>
      )}

      {confirmingCancel && !allResolved && !reservationExpired && (
        <CancelConfirmDialog
          isCancelling={isCancelling}
          onDismiss={() => setConfirmingCancel(false)}
          onConfirm={() => {
            setConfirmingCancel(false)
            onCancelOrder()
          }}
        />
      )}
    </>
  )
}
