import { useState } from 'react'
import { MAX_TICKETS_PER_PURCHASE, type TicketType, type ZoneOccupancy } from '../types'

interface Props {
  zone: ZoneOccupancy
  ticketType: TicketType
  remainingQuota: number
  onAdd: (quantity: number) => void
  onClose: () => void
}

export default function ZonePopup({ zone, ticketType, remainingQuota, onAdd, onClose }: Props) {
  const [quantity, setQuantity] = useState(1)
  const maxQuantity = Math.max(0, Math.min(remainingQuota, zone.availableToSell))
  const hasDiscount = ticketType.priceTier !== 'Normal'

  return (
    <div className="zone-popup-backdrop" onClick={onClose}>
      <div className="zone-popup" onClick={(e) => e.stopPropagation()}>
        <button type="button" className="zone-popup__close" onClick={onClose} aria-label="Cerrar">
          ×
        </button>

        <h3 className="zone-popup__title">
          {zone.name}
          {zone.availableToSell > 0 && <span className="zone-popup__tier">{ticketType.priceTier}</span>}
        </h3>

        {zone.availableToSell > 0 && (
          <>
            <p className="zone-popup__meta">
              <span className="zone-popup__type">{ticketType.name}</span>
              - 
              <span className="zone-popup__availability">
                {zone.availableToSell} libres de {zone.capacity}
              </span>
            </p>
            <p className="zone-popup__price">
              {hasDiscount && <span className="zone-popup__price-old">{ticketType.basePrice.toFixed(2)} €</span>}
              <span className="zone-popup__price-new">{ticketType.effectivePrice.toFixed(2)} €</span>
            </p>
          </>
        )}

        {maxQuantity > 0 ? (
          <>
            <div className="zone-popup__quantity">
              <span>Cantidad</span>
              <div className="stepper">
                <button
                  type="button"
                  className="stepper__btn"
                  onClick={() => setQuantity((q) => Math.max(1, q - 1))}
                  disabled={quantity <= 1}
                  aria-label="Restar una entrada"
                >
                  −
                </button>
                <span className="stepper__value">{quantity}</span>
                <button
                  type="button"
                  className="stepper__btn"
                  onClick={() => setQuantity((q) => Math.min(maxQuantity, q + 1))}
                  disabled={quantity >= maxQuantity}
                  aria-label="Sumar una entrada"
                >
                  +
                </button>
              </div>
            </div>

            <button type="button" onClick={() => onAdd(quantity)}>
              Añadir {quantity > 1 ? `${quantity} entradas` : '1 entrada'}
            </button>
          </>
        ) : (
          <p className="zone-popup__hint">
            {zone.availableToSell <= 0
              ? 'Esta zona ya no tiene aforo disponible.'
              : `Ya has alcanzado el máximo de ${MAX_TICKETS_PER_PURCHASE} entradas por compra.`}
          </p>
        )}
      </div>
    </div>
  )
}
