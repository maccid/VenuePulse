import { useState } from 'react'
import type { CartLine, PaymentMethod, TicketType, ZoneOccupancy } from '../types'
import { MAX_TICKETS_PER_PURCHASE } from '../types'
import { matchTicketType, pricingByZoneId } from '../zoneTicketType'
import StadiumMap from './StadiumMap'
import ZonePopup from './ZonePopup'
import CartPanel from './CartPanel'

interface Props {
  ticketTypes: TicketType[]
  zones: ZoneOccupancy[]
  holder: string
  onHolderChange: (holder: string) => void
  cart: CartLine[]
  isPaying: boolean
  onAddToCart: (zoneId: string, ticketTypeId: string, quantity: number) => void
  onSubmitPayment: (method: PaymentMethod) => void
}

export default function PurchaseFlow({
  ticketTypes,
  zones,
  holder,
  onHolderChange,
  cart,
  isPaying,
  onAddToCart,
  onSubmitPayment,
}: Props) {
  const [selectedZoneId, setSelectedZoneId] = useState<string | null>(null)

  const holderReady = holder.trim().length > 0
  const activeCartCount = cart.filter((l) => l.status !== 'Rejected').length
  const remainingQuota = Math.max(0, MAX_TICKETS_PER_PURCHASE - activeCartCount)

  const selectedZone = zones.find((z) => z.zoneId === selectedZoneId)
  const selectedTicketType = selectedZone && matchTicketType(selectedZone.name, ticketTypes)
  const pricing = pricingByZoneId(zones, ticketTypes)

  return (
    <div className="purchase-flow">
      <div className="purchase-flow__map-column">
        <label className="field">
          Tu nombre
          <input
            type="text"
            value={holder}
            onChange={(e) => onHolderChange(e.target.value)}
            placeholder="¿A nombre de quién compras?"
          />
        </label>

        <p className="field-label">Zona / puerta de acceso</p>
        <StadiumMap
          zones={zones}
          selectedZoneId={selectedZoneId ?? undefined}
          onSelect={setSelectedZoneId}
          pricingByZoneId={pricing}
        />

        {selectedZone && selectedTicketType && (
          <ZonePopup
            zone={selectedZone}
            ticketType={selectedTicketType}
            holderReady={holderReady}
            remainingQuota={remainingQuota}
            onAdd={(quantity) => {
              onAddToCart(selectedZone.zoneId, selectedTicketType.id, quantity)
              setSelectedZoneId(null)
            }}
            onClose={() => setSelectedZoneId(null)}
          />
        )}
      </div>

      <CartPanel cart={cart} holder={holder} isPaying={isPaying} onSubmitPayment={onSubmitPayment} />
    </div>
  )
}
