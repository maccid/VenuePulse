import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type {
  BuyerData,
  CartLine,
  DraftLine,
  EventSummary,
  PaymentMethod,
  PricingChanged,
  TicketStatus,
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

// Estado terminal del flujo de compra (ver types.ts): fuera de él la UI debe seguir esperando el
// resultado del pago, aunque el hub de SignalR haya perdido el mensaje final.
function isResolved(status: TicketStatus): boolean {
  return status === 'Confirmed' || status === 'Rejected' || status === 'CheckedIn'
}

export default function App({ eventId, holder }: Props) {
  const [event, setEvent] = useState<EventSummary | undefined>(undefined)
  const [ticketTypes, setTicketTypes] = useState<TicketType[]>([])
  const [zones, setZones] = useState<ZoneOccupancy[]>([])
  const [draft, setDraft] = useState<DraftLine[]>([])
  const [cart, setCart] = useState<CartLine[]>([])
  const [buyer, setBuyer] = useState<BuyerData>({ nombre: '', apellidos: '', email: '' })
  const [isReserving, setIsReserving] = useState(false)
  const [isPaying, setIsPaying] = useState(false)

  // Ref espejo de `cart` para leer su valor más reciente desde callbacks de SignalR que se
  // registran una sola vez (onreconnected) y que, de capturar `cart`, quedarían con un valor viejo.
  const cartRef = useRef(cart)
  useEffect(() => {
    cartRef.current = cart
  }, [cart])

  // Aplica el estado de una entrada a su línea del carrito. Extraído para que el handler del hub
  // y la reconciliación con GET /status compartan el mismo criterio de actualización.
  function applyStatusUpdate(update: TicketStatusUpdate) {
    setCart((prev) =>
      prev.map((line) =>
        line.ticketId === update.ticketId
          ? {
              ...line,
              status: update.status,
              rejectionReason: update.rejectionReason,
              expiresAtUtc: update.expiresAtUtc,
            }
          : line,
      ),
    )
  }

  // El resultado del pago viaja por SignalR (ProcessingPayment → Confirmed/Rejected), pero si la
  // conexión al hub cae durante el delay de la Saga, el mensaje final se pierde (SignalR no lo
  // repite) y el panel se quedaría en "Procesando pago…" para siempre — sin botón de recarga ni
  // resultado. GET /api/checkin/api/tickets/{id}/status (endpoint ya existente) es la fuente de
  // verdad: se consulta hasta resolver todas las líneas o agotar los intentos.
  async function reconcileStatuses(ticketIds: string[], attempts = 20): Promise<boolean> {
    const pending = new Set(ticketIds)
    for (let attempt = 0; attempt < attempts && pending.size > 0; attempt++) {
      await new Promise((resolve) => setTimeout(resolve, 1000))
      const updates = await Promise.all(
        [...pending].map(async (ticketId) => {
          try {
            const response = await fetch(`/api/checkin/api/tickets/${ticketId}/status`)
            if (!response.ok) return null
            return (await response.json()) as TicketStatusUpdate
          } catch {
            return null
          }
        }),
      )
      for (const update of updates) {
        if (update) {
          applyStatusUpdate(update)
          if (isResolved(update.status)) pending.delete(update.ticketId)
        }
      }
    }
    return pending.size === 0
  }

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
      applyStatusUpdate(update)
    })

    // El hub no repite mensajes tras una reconexión: los eventos emitidos durante el corte se
    // pierden. Re-sincronizar con GET /status las líneas que aún no están en un estado final.
    connection.onreconnected(() => {
      const unresolved = cartRef.current.filter((l) => !isResolved(l.status)).map((l) => l.ticketId)
      if (unresolved.length > 0) void reconcileStatuses(unresolved)
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

  // Se apaga sola cuando el pago ya se ha resuelto (todo Confirmado/Rechazado vía SignalR o la
  // reconciliación con GET /status). Hasta entonces la Saga pasa cada línea por un estado
  // intermedio ("ProcessingPayment") en el que el formulario debe seguir visible mostrando
  // "Procesando pago…" (ver comentario en types.ts).
  useEffect(() => {
    if (isPaying && cart.length > 0 && cart.every((line) => isResolved(line.status))) {
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
          quantity,
        },
      ]
    })
  }

  // Quitar una línea del borrador ("Tus Reservas") — no hay llamada al backend: todavía no se
  // ha reservado nada de verdad (el aforo real no se toca hasta "Reservar", ver ADR 0025).
  function removeDraftLine(zoneId: string, ticketTypeId: string) {
    setDraft((prev) => prev.filter((line) => !(line.zoneId === zoneId && line.ticketTypeId === ticketTypeId)))
  }

  // Reiniciar la compra desde la fila de "Tu compra": solo aparece cuando todo está resuelto
  // (compra completada o fallida — en ese caso las reservas ya se liberaron solas por la
  // compensación de la Saga), así que limpiar el estado local es seguro y deja listo un carrito
  // nuevo. El formulario de compra ("Mis Datos") también se resetea (ver feedback de usuario).
  function resetPurchase() {
    setCart([])
    setDraft([])
    setBuyer({ nombre: '', apellidos: '', email: '' })
    setIsPaying(false)
    setIsReserving(false)
  }

  // Único momento en que se llama al backend: reserva de verdad cada unidad del borrador (mismo
  // POST que antes disparaba cada "Añadir" individual) y solo entonces aparecen en `cart` — a
  // partir de aquí `occupancyChanged` (SignalR) refleja la selección y el semáforo del plano
  // cambia de color.
  async function reservar() {
    setIsReserving(true)

    const purchases = await Promise.all(
      draft.flatMap((line) => {
        // Precio efectivo vigente en el instante real de "Reservar", no el que tenía la zona
        // cuando se pulsó "Añadir" — el borrador no fija nada hasta aquí (ver types.ts).
        const ticketType = ticketTypes.find((t) => t.id === line.ticketTypeId)
        if (!ticketType) return []

        return Array.from({ length: line.quantity }, async (): Promise<CartLine | null> => {
          const response = await fetch('/api/checkin/api/tickets', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            // CheckIn exige el precio desde el Bloque 17 y lo guarda tal cual, sin revalidar
            // contra Ticketing (ADR 0021) — a partir de aquí sí queda inmutable.
            body: JSON.stringify({
              eventId,
              ticketTypeId: line.ticketTypeId,
              zoneId: line.zoneId,
              holder: buyer.nombre.trim() || holder,
              price: ticketType.effectivePrice,
            }),
          })
          if (!response.ok) return null
          const { ticketId } = await response.json()
          return {
            ticketId,
            zoneId: line.zoneId,
            zoneName: line.zoneName,
            ticketTypeName: line.ticketTypeName,
            unitPrice: ticketType.effectivePrice,
            priceTier: ticketType.priceTier,
            status: 'Pending',
            rejectionReason: null,
            expiresAtUtc: null,
          }
        })
      }),
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

    // No fiarse solo del hub: si se pierde el estado final por una reconexión, reconciliar contra
    // GET /status. Si tras los intentos sigue sin resolverse (p. ej. el /pay fue rechazado), se
    // suelta el botón para poder reintentar en vez de quedarse en "Procesando pago…" para siempre.
    const resolved = await reconcileStatuses(awaitingLines.map((l) => l.ticketId))
    if (!resolved) setIsPaying(false)
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
          buyer={buyer}
          isReserving={isReserving}
          isPaying={isPaying}
          onBuyerChange={setBuyer}
          onAddToDraft={addToDraft}
          onRemoveDraftLine={removeDraftLine}
          onResetPurchase={resetPurchase}
          onReservar={reservar}
          onSubmitPayment={submitPayment}
        />
      )}
    </div>
  )
}
