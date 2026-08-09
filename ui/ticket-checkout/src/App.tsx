import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { CartLine, PaymentMethod, PricingChanged, TicketStatusUpdate, TicketType, ZoneOccupancy } from './types'
import PurchaseFlow from './components/PurchaseFlow'
import './App.css'

interface Props {
  eventId: string
  holder: string
}

export default function App({ eventId, holder }: Props) {
  const [ticketTypes, setTicketTypes] = useState<TicketType[]>([])
  const [zones, setZones] = useState<ZoneOccupancy[]>([])
  const [holderName, setHolderName] = useState(holder)
  const [cart, setCart] = useState<CartLine[]>([])
  const [isPaying, setIsPaying] = useState(false)

  useEffect(() => {
    fetch(`/api/ticketing/api/events/${eventId}/ticket-types`)
      .then((r) => r.json())
      .then(setTicketTypes)
      .catch(() => setTicketTypes([]))

    fetch(`/api/access-control/api/zones?eventId=${eventId}`)
      .then((r) => r.json())
      .then(setZones)
      .catch(() => setZones([]))
  }, [eventId])

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/tickets')
      .withAutomaticReconnect()
      .build()

    connection.on('ticketStatusChanged', (update: TicketStatusUpdate) => {
      setCart((prev) =>
        prev.map((line) =>
          line.ticketId === update.ticketId
            ? { ...line, status: update.status, rejectionReason: update.rejectionReason }
            : line,
        ),
      )
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de entradas', err))

    return () => {
      connection.stop()
    }
  }, [])

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/occupancy')
      .withAutomaticReconnect()
      .build()

    // El hub emite para todas las zonas de todos los eventos; solo aplica si ya está en nuestra
    // lista (cargada con ?eventId=), así la actualización queda scoped al evento sin filtrar aquí.
    connection.on('occupancyChanged', (updated: ZoneOccupancy) => {
      setZones((prev) => prev.map((z) => (z.zoneId === updated.zoneId ? updated : z)))
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de aforo', err))

    return () => {
      connection.stop()
    }
  }, [])

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/pricing')
      .withAutomaticReconnect()
      .build()

    // El hub emite para todos los eventos; solo aplica si el mensaje es del evento que estamos
    // mostrando (mismo criterio que occupancyChanged más arriba).
    connection.on('pricingChanged', (updated: PricingChanged) => {
      if (updated.eventId === eventId) setTicketTypes(updated.ticketTypes)
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de precios', err))

    return () => {
      connection.stop()
    }
  }, [eventId])

  // Se apaga sola en cuanto ninguna línea sigue "AwaitingPayment" — ya sea porque todo se
  // confirmó/rechazó (llegado vía SignalR) o porque nunca se llegó a pagar nada.
  useEffect(() => {
    if (isPaying && !cart.some((line) => line.status === 'AwaitingPayment')) {
      setIsPaying(false)
    }
  }, [cart, isPaying])

  async function addToCart(zoneId: string, ticketTypeId: string, quantity: number) {
    const zone = zones.find((z) => z.zoneId === zoneId)
    const ticketType = ticketTypes.find((t) => t.id === ticketTypeId)
    if (!zone || !ticketType) return

    const purchases = await Promise.all(
      Array.from({ length: quantity }, async () => {
        const response = await fetch('/api/checkin/api/tickets', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ eventId, ticketTypeId, zoneId, holder: holderName }),
        })
        if (!response.ok) return null
        const { ticketId } = await response.json()
        return ticketId as string
      }),
    )

    const newLines: CartLine[] = purchases
      .filter((ticketId): ticketId is string => ticketId !== null)
      .map((ticketId) => ({
        ticketId,
        zoneId,
        zoneName: zone.name,
        ticketTypeName: ticketType.name,
        unitPrice: ticketType.effectivePrice,
        priceTier: ticketType.priceTier,
        status: 'Pending',
        rejectionReason: null,
      }))

    setCart((prev) => [...prev, ...newLines])
  }

  async function submitPayment(method: PaymentMethod) {
    setIsPaying(true)

    const awaitingLines = cart.filter((line) => line.status === 'AwaitingPayment')
    await Promise.all(
      awaitingLines.map((line) =>
        fetch(`/api/checkin/api/tickets/${line.ticketId}/pay`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ method }),
        }),
      ),
    )
  }

  return (
    <div className="ticket-checkout">
      <h3 className="ticket-checkout__title">Comprar entrada</h3>

      <PurchaseFlow
        ticketTypes={ticketTypes}
        zones={zones}
        holder={holderName}
        onHolderChange={setHolderName}
        cart={cart}
        isPaying={isPaying}
        onAddToCart={addToCart}
        onSubmitPayment={submitPayment}
      />
    </div>
  )
}
