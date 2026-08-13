import type { EventStatus } from '../../shared/types'

interface Props {
  status: EventStatus
}

const STEPS = [
  { key: 'OnSale', label: 'Venta' },
  { key: 'SalesClosed', label: 'Ocupación' },
] as const

export default function PhaseIndicator({ status }: Props) {
  if (status === 'Cancelled') {
    return (
      <div className="inline-flex items-center gap-3 rounded-md bg-accent-bg p-1" role="status" aria-label="Etapa del evento: cancelado">
        <span className="pl-1 text-sm font-semibold text-text-muted">Etapa</span>
        <span className="rounded-sm bg-surface px-4 py-2 text-sm font-semibold text-danger shadow-sm">Cancelado</span>
      </div>
    )
  }

  return (
    <div className="inline-flex items-center gap-3 rounded-md bg-accent-bg p-1" role="status" aria-label="Etapa del evento">
      <span className="pl-1 text-sm font-semibold text-text-muted">Etapa</span>
      <ol className="m-0 flex list-none gap-1 p-0">
        {STEPS.map((step) => (
          <li
            key={step.key}
            aria-current={status === step.key ? 'step' : undefined}
            className={`rounded-sm px-4 py-2 text-sm font-semibold ${status === step.key ? 'bg-surface text-text opacity-100 shadow-sm' : 'text-text-muted opacity-75'}`}
          >
            {step.label}
          </li>
        ))}
      </ol>
    </div>
  )
}
