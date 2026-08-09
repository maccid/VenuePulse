import type { TicketType } from '../types'

interface Props {
  ticketTypes: TicketType[]
  ticketTypeId: string
  disabled: boolean
  onChange: (id: string) => void
}

export default function StepTicketType({ ticketTypes, ticketTypeId, disabled, onChange }: Props) {
  return (
    <div className="wizard-step">
      <label className="field">
        Tipo de entrada
        <select value={ticketTypeId} onChange={(e) => onChange(e.target.value)} disabled={disabled}>
          {ticketTypes.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name} — {t.effectivePrice.toFixed(2)} €
              {t.priceTier !== 'Normal' ? ` (${t.priceTier}, antes ${t.price.toFixed(2)} €)` : ''}
            </option>
          ))}
        </select>
      </label>
    </div>
  )
}
