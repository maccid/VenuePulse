import type { DraftLine, TicketType } from '../../shared/types'
import { MAX_TICKETS_PER_PURCHASE } from '../../shared/types'
import { TrashIcon } from '../../shared/icons'

interface Props {
  ticketTypes: TicketType[]
  draft: DraftLine[]
  isReserving: boolean
  onRemoveDraftLine: (zoneId: string, ticketTypeId: string) => void
  onReservar: () => void
}

export default function DraftCart({ ticketTypes, draft, isReserving, onRemoveDraftLine, onReservar }: Props) {
  const unitPriceOf = (line: DraftLine) => ticketTypes.find((t) => t.id === line.ticketTypeId)?.effectivePrice ?? 0

  if (draft.length === 0) {
    return <p className="cart-panel__empty">Elige una zona en el plano para añadir entradas.</p>
  }

  const draftTotal = draft.reduce((sum, l) => sum + unitPriceOf(l) * l.quantity, 0)
  const draftCount = draft.reduce((sum, l) => sum + l.quantity, 0)

  return (
    <>
      <ul className="cart-panel__lines">
        {draft.map((line) => (
          <li key={`${line.zoneId}-${line.ticketTypeId}`} className="cart-panel__draft-line">
            <div className="cart-panel__line-header">
              <span>
                {line.zoneName} × {line.quantity}
              </span>
              <span className="cart-panel__line-actions">
                <span className="cart-panel__subtotal">{(unitPriceOf(line) * line.quantity).toFixed(2)} €</span>
                <button
                  type="button"
                  className="cart-panel__remove"
                  aria-label={`Quitar ${line.zoneName}`}
                  onClick={() => onRemoveDraftLine(line.zoneId, line.ticketTypeId)}
                >
                  <TrashIcon />
                </button>
              </span>
            </div>
          </li>
        ))}
      </ul>

      <p className="cart-panel__total">
        <span className="cart-panel__quota">
          {draftCount} / {MAX_TICKETS_PER_PURCHASE} entradas
        </span>
        <span className="cart-panel__total-actions">
          <strong>{draftTotal.toFixed(2)} €</strong>
          <span className="cart-panel__line-slot" aria-hidden="true" />
        </span>
      </p>

      <button type="button" className="cart-panel__reserve" disabled={isReserving} onClick={onReservar}>
        {isReserving ? (
          <>
            Reservando
            <span className="cart-panel__reserve-dots" aria-hidden="true">
              <span>.</span>
              <span>.</span>
              <span>.</span>
            </span>
          </>
        ) : (
          'Reservar'
        )}
      </button>
    </>
  )
}
