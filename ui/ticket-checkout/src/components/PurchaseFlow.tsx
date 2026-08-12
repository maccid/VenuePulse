import { useState } from 'react'
import type { BuyerData, CartLine, DraftLine, PaymentMethod, TicketType, ZoneOccupancy } from '../shared/types'
import { MAX_TICKETS_PER_PURCHASE } from '../shared/types'
import { matchTicketType, pricingByZoneId } from '../shared/zoneTicketType'
import StadiumMap from './map/StadiumMap'
import ZonePopup from './ZonePopup'
import CartPanel from './cart/CartPanel'
import { MapIcon } from '../shared/icons'

interface Props {
  ticketTypes: TicketType[]
  zones: ZoneOccupancy[]
  draft: DraftLine[]
  cart: CartLine[]
  buyer: BuyerData
  isReserving: boolean
  isPaying: boolean
  onBuyerChange: (buyer: BuyerData) => void
  onAddToDraft: (zoneId: string, ticketTypeId: string, quantity: number) => void
  onRemoveDraftLine: (zoneId: string, ticketTypeId: string) => void
  onResetPurchase: () => void
  onReservar: () => void
  onSubmitPayment: (method: PaymentMethod) => void
}

export default function PurchaseFlow({
  ticketTypes,
  zones,
  draft,
  cart,
  buyer,
  isReserving,
  isPaying,
  onBuyerChange,
  onAddToDraft,
  onRemoveDraftLine,
  onResetPurchase,
  onReservar,
  onSubmitPayment,
}: Props) {
  const [selectedZoneId, setSelectedZoneId] = useState<string | null>(null)

  const draftedQuantityByZone = draft.reduce<Record<string, number>>((acc, line) => {
    acc[line.zoneId] = (acc[line.zoneId] ?? 0) + line.quantity
    return acc
  }, {})

  const draftCount = draft.reduce((sum, line) => sum + line.quantity, 0)
  const activeCartCount = cart.filter((l) => l.status !== 'Rejected').length
  const remainingQuota = Math.max(0, MAX_TICKETS_PER_PURCHASE - draftCount - activeCartCount)

  const selectedZone = zones.find((z) => z.zoneId === selectedZoneId)
  const selectedZoneWithDraftedSlotsHeld =
    selectedZone &&
    ({
      ...selectedZone,
      availableToSell: Math.max(0, selectedZone.availableToSell - (draftedQuantityByZone[selectedZone.zoneId] ?? 0)),
    } satisfies ZoneOccupancy)
  const selectedTicketType = selectedZone && matchTicketType(selectedZone.name, ticketTypes)
  const pricing = pricingByZoneId(zones, ticketTypes)

  const mapInteractableForNewSelection = cart.length === 0

  return (
    <div className="purchase-flow">
      <div className="ticket-checkout purchase-flow__main">
        <div className="purchase-flow__map-card-header">
          <MapIcon />
          <span>Mapa del Evento</span>
        </div>
        <StadiumMap
          zones={zones}
          selectedZoneId={selectedZoneId ?? undefined}
          onSelect={mapInteractableForNewSelection ? setSelectedZoneId : undefined}
          pricingByZoneId={mapInteractableForNewSelection ? pricing : undefined}
        />

        {selectedZoneWithDraftedSlotsHeld && selectedTicketType && (
          <ZonePopup
            zone={selectedZoneWithDraftedSlotsHeld}
            ticketType={selectedTicketType}
            remainingQuota={remainingQuota}
            onAdd={(quantity) => {
              onAddToDraft(selectedZoneWithDraftedSlotsHeld.zoneId, selectedTicketType.id, quantity)
              setSelectedZoneId(null)
            }}
            onClose={() => setSelectedZoneId(null)}
          />
        )}
      </div>

      <CartPanel
        ticketTypes={ticketTypes}
        draft={draft}
        cart={cart}
        buyer={buyer}
        isReserving={isReserving}
        isPaying={isPaying}
        onBuyerChange={onBuyerChange}
        onRemoveDraftLine={onRemoveDraftLine}
        onResetPurchase={onResetPurchase}
        onReservar={onReservar}
        onSubmitPayment={onSubmitPayment}
      />
    </div>
  )
}
