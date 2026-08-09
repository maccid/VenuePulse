import type { TicketStatus } from './types'

interface Props {
  status: TicketStatus | 'Idle' | 'Purchasing'
  rejectionReason: string | null
}

const STEPS = [
  { key: 'reserve', label: 'Reservando aforo' },
  { key: 'payment', label: 'Procesando pago' },
  { key: 'confirm', label: 'Confirmando' },
  { key: 'done', label: 'Entrada emitida' },
] as const

// El paso donde se marca el fallo depende del motivo de rechazo que puso la propia Saga —
// así el timeline refleja en qué punto exacto se disparó la compensación (ver TicketPurchaseSaga).
function failingStepIndex(rejectionReason: string | null): number {
  if (rejectionReason === 'ZoneFull') return 0
  if (rejectionReason === 'PaymentFailed') return 1
  return STEPS.length - 1
}

export function SagaTimeline({ status, rejectionReason }: Props) {
  const isRejected = status === 'Rejected'
  const isDone = status === 'Confirmed' || status === 'CheckedIn'
  const activeIndex = isRejected ? failingStepIndex(rejectionReason) : isDone ? STEPS.length - 1 : status === 'Purchasing' ? 0 : -1

  return (
    <ol className="saga-timeline">
      {STEPS.map((step, index) => {
        let stepState: 'pending' | 'active' | 'done' | 'failed' = 'pending'
        if (isRejected && index === activeIndex) stepState = 'failed'
        else if (isRejected && index > activeIndex) stepState = 'pending'
        else if (isDone && index <= activeIndex) stepState = 'done'
        else if (!isRejected && !isDone && index <= activeIndex) stepState = 'active'

        return (
          <li key={step.key} className={`saga-step saga-step--${stepState}`}>
            <span className="saga-step-dot" />
            <span>{step.label}</span>
          </li>
        )
      })}
      {isRejected && (
        <p className="saga-rejection-reason">
          Motivo: {rejectionReason === 'ZoneFull' ? 'la zona se llenó justo antes de confirmar' : 'el pago simulado ha fallado (compensación real: aforo liberado)'}
        </p>
      )}
    </ol>
  )
}
