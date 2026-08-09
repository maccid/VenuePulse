import { useState } from 'react'
import type { TicketType, ZoneOccupancy } from '../types'
import StepTicketType from './StepTicketType'
import StepZone from './StepZone'
import StepConfirm from './StepConfirm'

interface Props {
  ticketTypes: TicketType[]
  zones: ZoneOccupancy[]
  ticketTypeId: string
  zoneId: string
  holder: string
  simulateFailure: boolean
  disabled: boolean
  onTicketTypeChange: (id: string) => void
  onZoneChange: (id: string) => void
  onHolderChange: (holder: string) => void
  onSimulateFailureChange: (checked: boolean) => void
  onSubmit: () => void
}

const STEP_LABELS = ['Entrada', 'Zona', 'Confirmar'] as const

export default function PurchaseWizard({
  ticketTypes,
  zones,
  ticketTypeId,
  zoneId,
  holder,
  simulateFailure,
  disabled,
  onTicketTypeChange,
  onZoneChange,
  onHolderChange,
  onSimulateFailureChange,
  onSubmit,
}: Props) {
  const [step, setStep] = useState<1 | 2 | 3>(1)

  const selectedTicketType = ticketTypes.find((t) => t.id === ticketTypeId)
  const selectedZone = zones.find((z) => z.zoneId === zoneId)

  const canAdvance = step === 1 ? Boolean(ticketTypeId) : Boolean(zoneId)

  return (
    <div className="purchase-wizard">
      <ol className="wizard-steps">
        {STEP_LABELS.map((label, index) => {
          const stepNumber = index + 1
          const state = stepNumber === step ? 'active' : stepNumber < step ? 'done' : 'pending'
          return (
            <li key={label} className={`wizard-steps__item wizard-steps__item--${state}`}>
              <span className="wizard-steps__index">{stepNumber}</span>
              {label}
            </li>
          )
        })}
      </ol>

      {step === 1 && (
        <StepTicketType
          ticketTypes={ticketTypes}
          ticketTypeId={ticketTypeId}
          disabled={disabled}
          onChange={onTicketTypeChange}
        />
      )}

      {step === 2 && <StepZone zones={zones} zoneId={zoneId} disabled={disabled} onChange={onZoneChange} />}

      {step === 3 && selectedTicketType && selectedZone && (
        <StepConfirm
          holder={holder}
          simulateFailure={simulateFailure}
          disabled={disabled}
          summary={{
            ticketTypeName: selectedTicketType.name,
            price: selectedTicketType.effectivePrice,
            zoneName: selectedZone.name,
          }}
          onHolderChange={onHolderChange}
          onSimulateFailureChange={onSimulateFailureChange}
          onSubmit={onSubmit}
        />
      )}

      <div className="wizard-nav">
        {step > 1 && (
          <button
            type="button"
            className="wizard-nav__back"
            disabled={disabled}
            onClick={() => setStep((s) => (s - 1) as 1 | 2)}
          >
            Atrás
          </button>
        )}
        {step < 3 && (
          <button type="button" disabled={disabled || !canAdvance} onClick={() => setStep((s) => (s + 1) as 2 | 3)}>
            Siguiente
          </button>
        )}
      </div>
    </div>
  )
}
