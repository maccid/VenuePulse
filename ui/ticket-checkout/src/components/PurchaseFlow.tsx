import { useState } from 'react'
import type { BuyerData, CartLine, DraftLine, PaymentMethod, TicketType, ZoneOccupancy } from '../types'
import { MAX_TICKETS_PER_PURCHASE } from '../types'
import { matchTicketType, pricingByZoneId } from '../zoneTicketType'
import StadiumMap from './StadiumMap'
import ZonePopup from './ZonePopup'
import CartPanel from './CartPanel'

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

// Icono de plano (sin librería de iconos, mismo criterio que el resto del proyecto — ver ADR 0017).
function MapIcon() {
  return (
    <svg
      className="purchase-flow__map-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21" />
      <line x1="9" y1="3" x2="9" y2="18" />
      <line x1="15" y1="6" x2="15" y2="21" />
    </svg>
  )
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

  // Aforo restante para elegir cantidad = lo que dice el backend menos lo que ya está en el
  // borrador local para esa zona (no reservado todavía) — mitigación best-effort de sobre-venta
  // local, la validación real la hace el backend al pulsar "Reservar" (ver ADR 0025).
  const draftedByZone = draft.reduce<Record<string, number>>((acc, line) => {
    acc[line.zoneId] = (acc[line.zoneId] ?? 0) + line.quantity
    return acc
  }, {})

  const draftCount = draft.reduce((sum, line) => sum + line.quantity, 0)
  const activeCartCount = cart.filter((l) => l.status !== 'Rejected').length
  const remainingQuota = Math.max(0, MAX_TICKETS_PER_PURCHASE - draftCount - activeCartCount)

  const selectedZone = zones.find((z) => z.zoneId === selectedZoneId)
  const adjustedSelectedZone =
    selectedZone &&
    ({
      ...selectedZone,
      availableToSell: Math.max(0, selectedZone.availableToSell - (draftedByZone[selectedZone.zoneId] ?? 0)),
    } satisfies ZoneOccupancy)
  const selectedTicketType = selectedZone && matchTicketType(selectedZone.name, ticketTypes)
  const pricing = pricingByZoneId(zones, ticketTypes)

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
          onSelect={setSelectedZoneId}
          pricingByZoneId={pricing}
        />

        {adjustedSelectedZone && selectedTicketType && (
          <ZonePopup
            zone={adjustedSelectedZone}
            ticketType={selectedTicketType}
            remainingQuota={remainingQuota}
            onAdd={(quantity) => {
              onAddToDraft(adjustedSelectedZone.zoneId, selectedTicketType.id, quantity)
              setSelectedZoneId(null)
            }}
            onClose={() => setSelectedZoneId(null)}
          />
        )}
      </div>

      <CartPanel
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
