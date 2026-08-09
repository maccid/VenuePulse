import type { TicketType, ZoneOccupancy } from '../types'

interface Props {
  ticketTypes: TicketType[]
  zones: ZoneOccupancy[]
  ticketTypeId: string
  zoneId: string
  simulateFailure: boolean
  disabled: boolean
  onTicketTypeChange: (id: string) => void
  onZoneChange: (id: string) => void
  onSimulateFailureChange: (checked: boolean) => void
  onSubmit: () => void
}

export default function PurchaseForm({
  ticketTypes,
  zones,
  ticketTypeId,
  zoneId,
  simulateFailure,
  disabled,
  onTicketTypeChange,
  onZoneChange,
  onSimulateFailureChange,
  onSubmit,
}: Props) {
  return (
    <form
      className="purchase-form"
      onSubmit={(e) => {
        e.preventDefault()
        onSubmit()
      }}
    >
      <label className="field">
        Tipo de entrada
        <select
          value={ticketTypeId}
          onChange={(e) => onTicketTypeChange(e.target.value)}
          disabled={disabled}
        >
          {ticketTypes.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name} — {t.effectivePrice.toFixed(2)} €
              {t.priceTier !== 'Normal' ? ` (${t.priceTier}, antes ${t.price.toFixed(2)} €)` : ''}
            </option>
          ))}
        </select>
      </label>

      <label className="field">
        Zona / puerta de acceso
        <select value={zoneId} onChange={(e) => onZoneChange(e.target.value)} disabled={disabled}>
          {zones.map((z) => (
            <option key={z.zoneId} value={z.zoneId}>
              {z.name} ({z.capacity - z.confirmedSold} libres)
            </option>
          ))}
        </select>
      </label>

      <label className="checkbox-field">
        <input
          type="checkbox"
          checked={simulateFailure}
          onChange={(e) => onSimulateFailureChange(e.target.checked)}
          disabled={disabled}
        />
        Simular fallo de pago (dispara la compensación real de la Saga)
      </label>

      <button type="submit" disabled={disabled || !ticketTypeId || !zoneId}>
        {disabled ? 'Procesando…' : 'Comprar entrada'}
      </button>
    </form>
  )
}
