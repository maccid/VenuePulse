import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type {
  BuyerData,
  CartLine,
  DraftLine,
  PaymentMethod,
  TicketStatus,
  TicketStatusUpdate,
  TicketType,
  ZoneOccupancy,
} from '../shared/types'

function isResolvedStatus(status: TicketStatus): boolean {
  return status === 'Confirmed' || status === 'Rejected' || status === 'CheckedIn'
}

interface CheckoutCart {
  draft: DraftLine[]
  cart: CartLine[]
  buyer: BuyerData
  isReserving: boolean
  isPaying: boolean
  setBuyer: (buyer: BuyerData) => void
  addToDraft: (zoneId: string, ticketTypeId: string, quantity: number) => void
  removeDraftLine: (zoneId: string, ticketTypeId: string) => void
  resetPurchase: () => void
  reservar: () => Promise<void>
  submitPayment: (method: PaymentMethod) => Promise<void>
}

export function useCheckoutCart(
  eventId: string,
  holder: string,
  ticketTypes: TicketType[],
  zones: ZoneOccupancy[],
): CheckoutCart {
  const [draft, setDraft] = useState<DraftLine[]>([])
  const [cart, setCart] = useState<CartLine[]>([])
  const [buyer, setBuyer] = useState<BuyerData>({ nombre: '', apellidos: '', email: '' })
  const [isReserving, setIsReserving] = useState(false)
  const [isPaying, setIsPaying] = useState(false)

  const cartRef = useRef(cart)
  useEffect(() => {
    cartRef.current = cart
  }, [cart])

  function applyStatusUpdate(update: TicketStatusUpdate) {
    setCart((prev) =>
      prev.map((line) =>
        line.ticketId === update.ticketId
          ? { ...line, status: update.status, rejectionReason: update.rejectionReason, expiresAtUtc: update.expiresAtUtc }
          : line,
      ),
    )
  }

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
          if (isResolvedStatus(update.status)) pending.delete(update.ticketId)
        }
      }
    }
    return pending.size === 0
  }

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder().withUrl('/hubs/tickets').withAutomaticReconnect().build()

    connection.on('ticketStatusChanged', (update: TicketStatusUpdate) => {
      applyStatusUpdate(update)
    })

    connection.onreconnected(() => {
      const unresolved = cartRef.current.filter((l) => !isResolvedStatus(l.status)).map((l) => l.ticketId)
      if (unresolved.length > 0) void reconcileStatuses(unresolved)
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de entradas', err))

    return () => {
      connection.stop()
    }
  }, [])

  useEffect(() => {
    if (isPaying && cart.length > 0 && cart.every((line) => isResolvedStatus(line.status))) {
      setIsPaying(false)
    }
  }, [cart, isPaying])

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
        { zoneId, zoneName: zone.name, ticketTypeId, ticketTypeName: ticketType.name, quantity },
      ]
    })
  }

  function removeDraftLine(zoneId: string, ticketTypeId: string) {
    setDraft((prev) => prev.filter((line) => !(line.zoneId === zoneId && line.ticketTypeId === ticketTypeId)))
  }

  function resetPurchase() {
    setCart([])
    setDraft([])
    setBuyer({ nombre: '', apellidos: '', email: '' })
    setIsPaying(false)
    setIsReserving(false)
  }

  async function reservar() {
    setIsReserving(true)

    const purchases = await Promise.all(
      draft.flatMap((line) => {
        const ticketType = ticketTypes.find((t) => t.id === line.ticketTypeId)
        if (!ticketType) return []

        return Array.from({ length: line.quantity }, async (): Promise<CartLine | null> => {
          const response = await fetch('/api/checkin/api/tickets', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
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

    const resolved = await reconcileStatuses(awaitingLines.map((l) => l.ticketId))
    if (!resolved) setIsPaying(false)
  }

  return { draft, cart, buyer, isReserving, isPaying, setBuyer, addToDraft, removeDraftLine, resetPurchase, reservar, submitPayment }
}
