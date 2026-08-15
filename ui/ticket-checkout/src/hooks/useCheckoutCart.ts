import { useEffect, useMemo, useRef, useState } from 'react'
import type {
  BuyerData,
  CartLine,
  DraftLine,
  OrderLine,
  OrderStatus,
  OrderStatusUpdate,
  OrderTicketStatus,
  PaymentMethod,
  TicketStatusUpdate,
  TicketType,
  ZoneOccupancy,
} from '../shared/types'
import { isAbortedHubStartError, createHubConnection } from 'venuepulse-core'

const GENERIC_ORDER_REJECTION_REASON = 'OrderRequestFailed'
const GENERIC_PAYMENT_REJECTION_REASON = 'PaymentRequestFailed'

function isResolvedOrderStatus(status: OrderStatus | null): boolean {
  return status === 'Completed' || status === 'Rejected'
}

function orderStorageKey(eventId: string): string {
  return `venuepulse.ticket-checkout.order.${eventId}`
}

function readStoredOrderId(eventId: string): string | null {
  try {
    return sessionStorage.getItem(orderStorageKey(eventId))
  } catch {
    return null
  }
}

function writeStoredOrderId(eventId: string, orderId: string | null) {
  try {
    if (orderId) sessionStorage.setItem(orderStorageKey(eventId), orderId)
    else sessionStorage.removeItem(orderStorageKey(eventId))
  } catch {}
}

interface CheckoutCart {
  draft: DraftLine[]
  cart: CartLine[]
  buyer: BuyerData
  isReserving: boolean
  isPaying: boolean
  orderStatus: OrderStatus | null
  orderRejectionReason: string | null
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
  const [buyer, setBuyer] = useState<BuyerData>({ nombre: '', apellidos: '', email: '' })
  const [isReserving, setIsReserving] = useState(false)
  const [isPaying, setIsPaying] = useState(false)

  const [orderId, setOrderId] = useState<string | null>(null)
  const [orderStatus, setOrderStatus] = useState<OrderStatus | null>(null)
  const [orderRejectionReason, setOrderRejectionReason] = useState<string | null>(null)
  const [orderTickets, setOrderTickets] = useState<OrderTicketStatus[]>([])

  const orderRef = useRef<{ orderId: string | null; status: OrderStatus | null }>({ orderId: null, status: null })
  useEffect(() => {
    orderRef.current = { orderId, status: orderStatus }
  }, [orderId, orderStatus])

  function claimOrder(id: string | null, status: OrderStatus | null) {
    orderRef.current = { orderId: id, status }
  }

  const cart = useMemo<CartLine[]>(
    () =>
      orderTickets.map((ticket) => {
        const zone = zones.find((z) => z.zoneId === ticket.zoneId)
        const ticketType = ticketTypes.find((t) => t.id === ticket.ticketTypeId)
        return {
          ticketId: ticket.ticketId,
          zoneId: ticket.zoneId,
          zoneName: zone?.name ?? ticket.zoneId,
          ticketTypeName: ticketType?.name ?? ticket.ticketTypeId,
          unitPrice: ticket.unitPrice,
          priceTier: ticketType?.priceTier ?? 'Normal',
          status: ticket.status,
          rejectionReason: ticket.rejectionReason,
          expiresAtUtc: ticket.expiresAtUtc,
        }
      }),
    [orderTickets, zones, ticketTypes],
  )

  function applyOrderStatusUpdate(update: OrderStatusUpdate) {
    if (orderRef.current.orderId !== update.orderId) return

    orderRef.current = { orderId: update.orderId, status: update.status }
    setOrderId(update.orderId)
    setOrderStatus(update.status)
    setOrderRejectionReason(update.rejectionReason)
    setOrderTickets(update.tickets)

    if (isResolvedOrderStatus(update.status)) writeStoredOrderId(eventId, null)
  }

  function applyTicketStatusUpdate(update: TicketStatusUpdate) {
    setOrderTickets((prev) =>
      prev.map((ticket) =>
        ticket.ticketId === update.ticketId
          ? { ...ticket, status: update.status, rejectionReason: update.rejectionReason, expiresAtUtc: update.expiresAtUtc }
          : ticket,
      ),
    )
  }

  function rejectOrder(reason: string) {
    setOrderStatus('Rejected')
    setOrderRejectionReason(reason)
  }

  async function reconcileOrderStatus(id: string, attempts = 20): Promise<boolean> {
    for (let attempt = 0; attempt < attempts; attempt++) {
      await new Promise((resolve) => setTimeout(resolve, 1000))
      try {
        const response = await fetch(`/api/checkin/api/orders/${id}/status`)
        if (response.ok) {
          const update = (await response.json()) as OrderStatusUpdate
          applyOrderStatusUpdate(update)
          if (isResolvedOrderStatus(update.status)) return true
        }
      } catch {
        // red caída o similar: se reintenta en la siguiente vuelta
      }
    }
    return isResolvedOrderStatus(orderRef.current.status)
  }

  useEffect(() => {
    const storedOrderId = readStoredOrderId(eventId)
    if (!storedOrderId) return

    let cancelled = false

    async function restoreOrder(id: string) {
      try {
        const response = await fetch(`/api/checkin/api/orders/${id}/status`)
        if (!response.ok) {
          writeStoredOrderId(eventId, null)
          return
        }

        const status = (await response.json()) as OrderStatusUpdate
        if (cancelled) return

        if (isResolvedOrderStatus(status.status)) {
          writeStoredOrderId(eventId, null)
          return
        }

        claimOrder(status.orderId, status.status)
        applyOrderStatusUpdate(status)
      } catch {}
    }

    void restoreOrder(storedOrderId)

    return () => {
      cancelled = true
    }
  }, [eventId])

  useEffect(() => {
    const connection = createHubConnection('/hubs/tickets')

    connection.on('ticketStatusChanged', (update: TicketStatusUpdate) => {
      applyTicketStatusUpdate(update)
    })

    connection.on('orderStatusChanged', (update: OrderStatusUpdate) => {
      applyOrderStatusUpdate(update)
    })

    async function joinEvent() {
      await connection.invoke('JoinEvent', eventId).catch(() => undefined)
    }

    connection.onreconnected(() => {
      void joinEvent()
      const { orderId: currentOrderId, status } = orderRef.current
      if (currentOrderId && !isResolvedOrderStatus(status)) void reconcileOrderStatus(currentOrderId)
    })

    connection.start().then(joinEvent).catch((err) => {
      if (!isAbortedHubStartError(err)) console.error('No se pudo conectar al hub de entradas', err)
    })

    return () => {
      connection.stop()
    }
  }, [eventId])

  useEffect(() => {
    if (isPaying && isResolvedOrderStatus(orderStatus)) {
      setIsPaying(false)
    }
  }, [orderStatus, isPaying])

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
    claimOrder(null, null)
    writeStoredOrderId(eventId, null)
    setOrderId(null)
    setOrderStatus(null)
    setOrderRejectionReason(null)
    setOrderTickets([])
    setDraft([])
    setBuyer({ nombre: '', apellidos: '', email: '' })
    setIsPaying(false)
    setIsReserving(false)
  }

  async function reservar() {
    setIsReserving(true)

    try {
      const lines: OrderLine[] = draft.map((line) => {
        const ticketType = ticketTypes.find((t) => t.id === line.ticketTypeId)
        return {
          ticketTypeId: line.ticketTypeId,
          zoneId: line.zoneId,
          quantity: line.quantity,
          unitPrice: ticketType?.effectivePrice ?? 0,
        }
      })

      const response = await fetch('/api/checkin/api/orders', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          eventId,
          holder: buyer.nombre.trim() || holder,
          lines,
        }),
      })

      if (!response.ok) {
        const body = await response.json().catch(() => null)
        rejectOrder(body?.error ?? GENERIC_ORDER_REJECTION_REASON)
        setDraft([])
        return
      }

      const { orderId: newOrderId } = (await response.json()) as { orderId: string }
      claimOrder(newOrderId, null)
      writeStoredOrderId(eventId, newOrderId)
      setOrderId(newOrderId)

      const statusResponse = await fetch(`/api/checkin/api/orders/${newOrderId}/status`)
      if (statusResponse.ok) {
        const status = (await statusResponse.json()) as OrderStatusUpdate
        applyOrderStatusUpdate(status)
      } else {
        setOrderStatus('Reserving')
      }
      setDraft([])
    } catch {
      rejectOrder(GENERIC_ORDER_REJECTION_REASON)
      setDraft([])
    } finally {
      setIsReserving(false)
    }
  }

  async function submitPayment(method: PaymentMethod) {
    if (!orderId) return

    setIsPaying(true)

    try {
      const response = await fetch(`/api/checkin/api/orders/${orderId}/pay`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ method }),
      })

      if (!response.ok) {
        const body = await response.json().catch(() => null)
        rejectOrder(body?.error ?? GENERIC_PAYMENT_REJECTION_REASON)
        setIsPaying(false)
        return
      }
    } catch {
      rejectOrder(GENERIC_PAYMENT_REJECTION_REASON)
      setIsPaying(false)
      return
    }

    const resolved = await reconcileOrderStatus(orderId)
    if (!resolved) setIsPaying(false)
  }

  return {
    draft,
    cart,
    buyer,
    isReserving,
    isPaying,
    orderStatus,
    orderRejectionReason,
    setBuyer,
    addToDraft,
    removeDraftLine,
    resetPurchase,
    reservar,
    submitPayment,
  }
}
