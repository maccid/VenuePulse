interface Summary {
  ticketTypeName: string
  price: number
  zoneName: string
}

interface Props {
  holder: string
  simulateFailure: boolean
  disabled: boolean
  summary: Summary
  onHolderChange: (holder: string) => void
  onSimulateFailureChange: (checked: boolean) => void
  onSubmit: () => void
}

export default function StepConfirm({
  holder,
  simulateFailure,
  disabled,
  summary,
  onHolderChange,
  onSimulateFailureChange,
  onSubmit,
}: Props) {
  const canSubmit = !disabled && holder.trim().length > 0

  return (
    <form
      className="wizard-step"
      onSubmit={(e) => {
        e.preventDefault()
        if (canSubmit) onSubmit()
      }}
    >
      <label className="field">
        Tu nombre
        <input
          type="text"
          value={holder}
          onChange={(e) => onHolderChange(e.target.value)}
          disabled={disabled}
          required
        />
      </label>

      <dl className="purchase-summary">
        <div>
          <dt>Entrada</dt>
          <dd>
            {summary.ticketTypeName} — {summary.price.toFixed(2)} €
          </dd>
        </div>
        <div>
          <dt>Zona</dt>
          <dd>{summary.zoneName}</dd>
        </div>
      </dl>

      <label className="checkbox-field">
        <input
          type="checkbox"
          checked={simulateFailure}
          onChange={(e) => onSimulateFailureChange(e.target.checked)}
          disabled={disabled}
        />
        Simular fallo de pago (dispara la compensación real de la Saga)
      </label>

      <button type="submit" disabled={!canSubmit}>
        {disabled ? 'Procesando…' : 'Comprar entrada'}
      </button>
    </form>
  )
}
