import { useState } from 'react'
import { MAX_TICKETS_PER_PURCHASE, type TicketType, type ZoneOccupancy } from '../types'

interface Props {
  zone: ZoneOccupancy
  ticketType: TicketType
  holderReady: boolean
  remainingQuota: number
  onAdd: (quantity: number) => void
  onClose: () => void
}

export default function ZonePopup({ zone, ticketType, holderReady, remainingQuota, onAdd, onClose }: Props) {
  const [quantity, setQuantity] = useState(1)
  const maxQuantity = Math.max(0, Math.min(remainingQuota, zone.availableToSell))
  const hasDiscount = ticketType.priceTier !== 'Normal'

  return (
    <div className="zone-popup-backdrop" onClick={onClose}>
      <div className="zone-popup" onClick={(e) => e.stopPropagation()}>
        <button type="button" className="zone-popup__close" onClick={onClose} aria-label="Cerrar">
          ×
        </button>

        <h3>{zone.name}</h3>
        <p className="zone-popup__type">{ticketType.name}</p>

        <p className="zone-popup__price">
          {hasDiscount && <span className="zone-popup__price-old">{ticketType.price.toFixed(2)} €</span>}
          <span className="zone-popup__price-new">{ticketType.effectivePrice.toFixed(2)} €</span>
          <span className="zone-popup__tier">{ticketType.priceTier}</span>
        </p>

        <p className="zone-popup__availability">
          {zone.availableToSell > 0 ? `${zone.availableToSell} libres de ${zone.capacity}` : 'Sin aforo disponible'}
        </p>

        {maxQuantity > 0 ? (
          <>
            <label className="zone-popup__quantity">
              Cantidad
              <select value={quantity} onChange={(e) => setQuantity(Number(e.target.value))}>
                {Array.from({ length: maxQuantity }, (_, i) => i + 1).map((n) => (
                  <option key={n} value={n}>
                    {n}
                  </option>
                ))}
              </select>
            </label>

            {!holderReady && <p className="zone-popup__hint">Escribe tu nombre arriba para poder comprar.</p>}

            <button type="button" disabled={!holderReady} onClick={() => onAdd(quantity)}>
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
