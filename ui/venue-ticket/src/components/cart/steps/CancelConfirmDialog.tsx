interface Props {
  isCancelling: boolean
  onConfirm: () => void
  onDismiss: () => void
}

export default function CancelConfirmDialog({ isCancelling, onConfirm, onDismiss }: Props) {
  return (
    <div className="zone-popup-backdrop" onClick={onDismiss}>
      <div className="zone-popup" onClick={(e) => e.stopPropagation()}>
        <h3 className="zone-popup__title">¿Cancelar la reserva?</h3>
        <p className="zone-popup__hint">Se liberará el aforo reservado y no podrás deshacerlo.</p>

        <div className="cancel-confirm__actions">
          <button type="button" onClick={onDismiss} disabled={isCancelling}>
            Volver
          </button>
          <button type="button" className="cart-panel__cancel" onClick={onConfirm} disabled={isCancelling}>
            {isCancelling ? 'Cancelando…' : 'Sí, cancelar'}
          </button>
        </div>
      </div>
    </div>
  )
}
