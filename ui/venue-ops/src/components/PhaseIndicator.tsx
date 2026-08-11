import type { EventStatus } from '../types'

interface Props {
  status: EventStatus
}

// Las fases de la vida de un evento, no modos intercambiables (ver decisión de la sesión): primero
// se vende y, al terminar esa etapa, el evento se cierra (ocupación) o se cancela. Es un indicador
// solo lectura derivado de `status` — la vista del workspace sigue al estado, no al clic.
const STEPS = [
  { key: 'OnSale', label: 'Venta' },
  { key: 'SalesClosed', label: 'Ocupación' },
] as const

export default function PhaseIndicator({ status }: Props) {
  if (status === 'Cancelled') {
    return (
      <div className="phase-indicator" role="status" aria-label="Etapa del evento: cancelado">
        <span className="phase-indicator__label">Etapa</span>
        <span className="phase-indicator__value phase-indicator__value--cancelled">Cancelado</span>
      </div>
    )
  }

  return (
    <div className="phase-indicator" role="status" aria-label="Etapa del evento">
      <span className="phase-indicator__label">Etapa</span>
      <ol className="phase-indicator__steps">
        {STEPS.map((step) => (
          <li
            key={step.key}
            aria-current={status === step.key ? 'step' : undefined}
            className={`phase-indicator__step${status === step.key ? ' phase-indicator__step--active' : ''}`}
          >
            {step.label}
          </li>
        ))}
      </ol>
    </div>
  )
}
