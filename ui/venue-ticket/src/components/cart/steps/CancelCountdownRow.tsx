import { TrashIcon } from '../../../shared/icons'

interface Props {
  countdown: string | null
  canCancel: boolean
  onCancel: () => void
}

export default function CancelCountdownRow({ countdown, canCancel, onCancel }: Props) {
  if (!countdown && !canCancel) return null

  return (
    <div className="cart-panel__cancel-row">
      {countdown && <p className="cart-panel__countdown">Reserva expira en {countdown}</p>}
      {canCancel && (
        <button
          type="button"
          className="cart-panel__remove"
          aria-label="Cancelar reserva"
          title="Cancelar reserva"
          onClick={onCancel}
        >
          <TrashIcon />
        </button>
      )}
    </div>
  )
}
