import type { TicketStatus } from './types'

interface Props {
  status: TicketStatus | 'Idle' | 'Purchasing' | 'Paying'
  rejectionReason: string | null
}

const STEPS = [
  { key: 'reserve', label: 'Reservando aforo' },
  { key: 'await-payment', label: 'Esperando pago' },
  { key: 'payment', label: 'Procesando pago' },
  { key: 'confirm', label: 'Confirmando' },
  { key: 'done', label: 'Entrada emitida' },
] as const

// El paso donde se marca el fallo depende del motivo de rechazo que puso la propia Saga —
// así el timeline refleja en qué punto exacto se disparó la compensación (ver TicketPurchaseSaga).
function failingStepIndex(rejectionReason: string | null): number {
  if (rejectionReason === 'ZoneFull' || rejectionReason === 'ReservationTimedOut') return 0
  if (rejectionReason === 'PaymentTimedOut') return 1
  if (rejectionReason === 'PaymentFailed') return 2
  return STEPS.length - 1
}

function activeIndexFor(status: Props['status']): number {
  switch (status) {
    case 'Idle':
      return -1
    case 'Purchasing':
    case 'Pending':
      return 0
    case 'AwaitingPayment':
      return 1
    case 'Paying':
      return 2
    default:
      return STEPS.length - 1
  }
}

export function SagaTimeline({ status, rejectionReason }: Props) {
  const isRejected = status === 'Rejected'
  const isDone = status === 'Confirmed' || status === 'CheckedIn'
  const activeIndex = isRejected ? failingStepIndex(rejectionReason) : activeIndexFor(status)

  return (
    <ol className="saga-timeline">
      {STEPS.map((step, index) => {
        let stepState: 'pending' | 'active' | 'done' | 'failed' = 'pending'
        if (isRejected && index === activeIndex) stepState = 'failed'
        else if (isRejected && index > activeIndex) stepState = 'pending'
        else if (isDone && index <= activeIndex) stepState = 'done'
        else if (!isRejected && !isDone && index < activeIndex) stepState = 'done'
        else if (!isRejected && !isDone && index === activeIndex) stepState = 'active'

        return (
          <li key={step.key} className={`saga-step saga-step--${stepState}`}>
            <span className="saga-step-dot" />
            <span>{step.label}</span>
          </li>
        )
      })}
      {isRejected && (
        <p className="saga-rejection-reason">
          Motivo:{' '}
          {rejectionReason === 'ZoneFull'
            ? 'la zona se llenó justo antes de confirmar'
            : rejectionReason === 'ReservationTimedOut'
              ? 'no se pudo reservar el aforo a tiempo'
              : rejectionReason === 'PaymentTimedOut'
                ? 'no se eligió método de pago a tiempo, el aforo se liberó'
                : 'el pago simulado ha fallado (compensación real: aforo liberado)'}
        </p>
      )}
    </ol>
  )
}
