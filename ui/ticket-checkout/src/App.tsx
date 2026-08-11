import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type {
  CartLine,
  DraftLine,
  EventSummary,
  PaymentMethod,
  PricingChanged,
  TicketStatusUpdate,
  TicketType,
  ZoneOccupancy,
} from './types'
import PurchaseFlow from './components/PurchaseFlow'
import CheckoutHeader from './components/CheckoutHeader'
import './App.css'

interface Props {
  eventId: string
  holder: string
}

export default function App({ eventId, holder }: Props) {
  const [event, setEvent] = useState<EventSummary | undefined>(undefined)
  const [ticketTypes, setTicketTypes] = useState<TicketType[]>([])
  const [zones, setZones] = useState<ZoneOccupancy[]>([])
  const [draft, setDraft] = useState<DraftLine[]>([])
  const [cart, setCart] = useState<CartLine[]>([])
  const [isReserving, setIsReserving] = useState(false)
  const [isPaying, setIsPaying] = useState(false)

  useEffect(() => {
    // La cabecera necesita nombre/categoría/fecha del evento, que el Web Component no recibe por
    // atributo — se obtiene con el GET /api/events existente y se busca por id (sin endpoint
    // nuevo, ver ADR 0023). El Status decide si se muestra la compra o el aviso de "no a la venta".
    fetch('/api/ticketing/api/events')
      .then((r) => r.json())
      .then((events: EventSummary[]) => setEvent(events.find((e) => e.id === eventId)))
      .catch(() => setEvent(undefined))

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
            ? { ...line, status: update.status, rejectionReason: update.rejectionReason, expiresAtUtc: update.expiresAtUtc }
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

  // Añadir desde el plano solo acumula en el borrador local, sin llamar al backend todavía — el
  // aforo real no se toca hasta pulsar "Reservar" (ver ADR 0025, reabre ADR 0018 punto 3).
  function addToDraft(zoneId: string, ticketTypeId: string, quantity: number) {
    const zone = zones.find((z) => z.zoneId === zoneId)
    const ticketType = ticketTypes.find((t) => t.id === ticketTypeId)
    if (!zone || !ticketType) return

    setDraft((prev) => {
      const existing = prev.find((l) => l.zoneId === zoneId && l.ticketTypeId === ticketTypeId)
      if (existing) {
        return prev.map((l) =>
          l.zoneId === zoneId && l.ticketTypeId === ticketTypeId ? { ...l, quantity: l.quantity + quantity } : l,
        )
      }
      return [
        ...prev,
        {
          zoneId,
          zoneName: zone.name,
          ticketTypeId,
          ticketTypeName: ticketType.name,
          unitPrice: ticketType.effectivePrice,
          priceTier: ticketType.priceTier,
          quantity,
        },
      ]
    })
  }

  // Único momento en que se llama al backend: reserva de verdad cada unidad del borrador (mismo
  // POST que antes disparaba cada "Añadir" individual) y solo entonces aparecen en `cart` — a
  // partir de aquí `occupancyChanged` (SignalR) refleja la selección y el semáforo del plano
  // cambia de color.
  async function reservar() {
    setIsReserving(true)

    const purchases = await Promise.all(
      draft.flatMap((line) =>
        Array.from({ length: line.quantity }, async (): Promise<CartLine | null> => {
          const response = await fetch('/api/checkin/api/tickets', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            // Precio efectivo mostrado en el popup en el momento de la compra — CheckIn lo exige
            // desde el Bloque 17 y lo guarda tal cual, sin revalidar contra Ticketing (ADR 0021).
            body: JSON.stringify({
              eventId,
              ticketTypeId: line.ticketTypeId,
              zoneId: line.zoneId,
              holder,
              price: line.unitPrice,
            }),
          })
          if (!response.ok) return null
          const { ticketId } = await response.json()
          return {
            ticketId,
            zoneId: line.zoneId,
            zoneName: line.zoneName,
            ticketTypeName: line.ticketTypeName,
            unitPrice: line.unitPrice,
            priceTier: line.priceTier,
            status: 'Pending',
            rejectionReason: null,
            expiresAtUtc: null,
          }
        }),
      ),
    )

    const newLines = purchases.filter((line): line is CartLine => line !== null)
    setCart((prev) => [...prev, ...newLines])
    setDraft([])
    setIsReserving(false)
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

  // El wrapper .ticket-checkout envuelve TODO el componente y hace de contenedor flex: la cabecera
  // en su card a ancho completo (con gap de 16px con lo siguiente) y debajo el plano y el carrito
  // en dos cards iguales dentro de purchase-flow (ver App.css).
  return (
    <div className="ticket-checkout">
      <CheckoutHeader event={event} />

      {event && event.status !== 'OnSale' ? (
        <p className="ticket-checkout__notice">
          Este evento ya no está a la venta.
        </p>
      ) : (
        <PurchaseFlow
          ticketTypes={ticketTypes}
          zones={zones}
          draft={draft}
          cart={cart}
          isReserving={isReserving}
          isPaying={isPaying}
          onAddToDraft={addToDraft}
          onReservar={reservar}
          onSubmitPayment={submitPayment}
        />
      )}
    </div>
  )
}
