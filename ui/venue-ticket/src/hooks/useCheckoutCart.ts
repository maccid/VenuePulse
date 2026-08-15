import { useCallback, useEffect, useMemo, useState } from 'react'
import type {
  BuyerData,
  CartLine,
  DraftLine,
  OrderLine,
  OrderStatus,
  OrderStatusUpdate,
  PaymentMethod,
  TicketStatusUpdate,
  TicketType,
  ZoneOccupancy,
} from '@venue/core/domain'
import { isResolvedOrderStatus } from '@venue/core/domain'
import { ApiError, API_ROUTES, api } from '@venue/core/api'
import { orderStore } from '@venue/core/state'
import { sessionStorageService } from '@venue/core/storage'
import { isAbortedHubStartError, createHubConnection } from '@venue/core/hub'

const GENERIC_ORDER_REJECTION_REASON = 'OrderRequestFailed'
const GENERIC_PAYMENT_REJECTION_REASON = 'PaymentRequestFailed'

function orderStorageKey(eventId: string): string {
  return `venuepulse.ticket-checkout.order.${eventId}`
}

function readStoredOrderId(eventId: string): string | null {
  return sessionStorageService.get(orderStorageKey(eventId))
}

function writeStoredOrderId(eventId: string, orderId: string | null) {
  if (orderId) sessionStorageService.set(orderStorageKey(eventId), orderId)
  else sessionStorageService.remove(orderStorageKey(eventId))
}

function orderErrorMessage(error: unknown, fallback: string): string {
  return error instanceof ApiError ? error.errorMessage() ?? fallback : fallback
}

interface CheckoutCart {
  draft: DraftLine[]
  cart: CartLine[]
  buyer: BuyerData
  isReserving: boolean
  isPaying: boolean
  isCancelling: boolean
  orderStatus: OrderStatus | null
  orderRejectionReason: string | null
  setBuyer: (buyer: BuyerData) => void
  addToDraft: (zoneId: string, ticketTypeId: string, quantity: number) => void
  removeDraftLine: (zoneId: string, ticketTypeId: string) => void
  resetPurchase: () => void
  reservar: () => Promise<void>
  submitPayment: (method: PaymentMethod) => Promise<void>
  cancelOrder: () => Promise<void>
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
  const [isCancelling, setIsCancelling] = useState(false)

  const orderId = orderStore.use((s) => s.orderId)
  const orderStatus = orderStore.use((s) => s.status)
  const orderRejectionReason = orderStore.use((s) => s.rejectionReason)
  const orderTickets = orderStore.use((s) => s.tickets)
  const setOrder = orderStore.use((s) => s.setOrder)
  const updateTicketStatus = orderStore.use((s) => s.updateTicketStatus)
  const resetOrder = orderStore.use((s) => s.reset)

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

  const applyOrderStatusUpdate = useCallback(
    (update: OrderStatusUpdate) => {
      if (orderStore.getState().orderId !== update.orderId) return

      setOrder({
        orderId: update.orderId,
        status: update.status,
        rejectionReason: update.rejectionReason,
        tickets: update.tickets,
      })

      if (isResolvedOrderStatus(update.status)) writeStoredOrderId(eventId, null)
    },
    [setOrder, eventId],
  )

  const applyTicketStatusUpdate = useCallback(
    (update: TicketStatusUpdate) => {
      updateTicketStatus(update.ticketId, update.status, update.rejectionReason, update.expiresAtUtc)
    },
    [updateTicketStatus],
  )

  const rejectOrder = useCallback(
    (reason: string) => {
      setOrder({ status: 'Rejected', rejectionReason: reason })
    },
    [setOrder],
  )

  const reconcileOrderStatus = useCallback(
    async (id: string, attempts = 20): Promise<boolean> => {
      for (let attempt = 0; attempt < attempts; attempt++) {
        await new Promise((resolve) => setTimeout(resolve, 1000))
        try {
          const update = await api.get<OrderStatusUpdate>(API_ROUTES.orderStatus(id))
          applyOrderStatusUpdate(update)
          if (isResolvedOrderStatus(update.status)) return true
        } catch {
          // red caída o similar: se reintenta en la siguiente vuelta
        }
      }
      return isResolvedOrderStatus(orderStore.getState().status)
    },
    [applyOrderStatusUpdate],
  )

  useEffect(() => {
    const storedOrderId = readStoredOrderId(eventId)
    if (!storedOrderId) return

    let cancelled = false

    async function restoreOrder(id: string) {
      try {
        const status = await api.get<OrderStatusUpdate>(API_ROUTES.orderStatus(id))
        if (cancelled) return

        if (isResolvedOrderStatus(status.status)) {
          writeStoredOrderId(eventId, null)
          return
        }

        setOrder({
          orderId: status.orderId,
          status: status.status,
          rejectionReason: status.rejectionReason,
          tickets: status.tickets,
        })
      } catch {
        writeStoredOrderId(eventId, null)
      }
    }

    void restoreOrder(storedOrderId)

    return () => {
      cancelled = true
    }
  }, [eventId, setOrder])

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
      const { orderId: currentOrderId, status } = orderStore.getState()
      if (currentOrderId && !isResolvedOrderStatus(status)) void reconcileOrderStatus(currentOrderId)
    })

    connection.start().then(joinEvent).catch((err) => {
      if (!isAbortedHubStartError(err)) console.error('No se pudo conectar al hub de entradas', err)
    })

    return () => {
      connection.stop()
    }
  }, [eventId, applyOrderStatusUpdate, applyTicketStatusUpdate, reconcileOrderStatus])

  useEffect(() => {
    if (!isResolvedOrderStatus(orderStatus)) return
    if (isPaying) setIsPaying(false)
    if (isCancelling) setIsCancelling(false)
  }, [orderStatus, isPaying, isCancelling])

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
    resetOrder()
    writeStoredOrderId(eventId, null)
    setDraft([])
    setBuyer({ nombre: '', apellidos: '', email: '' })
    setIsPaying(false)
    setIsReserving(false)
    setIsCancelling(false)
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

      const { orderId: newOrderId } = await api.post<{ orderId: string }>(API_ROUTES.orders, {
        eventId,
        holder: buyer.nombre.trim() || holder,
        lines,
      })

      setOrder({ orderId: newOrderId })
      writeStoredOrderId(eventId, newOrderId)

      try {
        const status = await api.get<OrderStatusUpdate>(API_ROUTES.orderStatus(newOrderId))
        applyOrderStatusUpdate(status)
      } catch {
        setOrder({ status: 'Reserving' })
      }
      setDraft([])
    } catch (error) {
      rejectOrder(orderErrorMessage(error, GENERIC_ORDER_REJECTION_REASON))
      setDraft([])
    } finally {
      setIsReserving(false)
    }
  }

  async function submitPayment(method: PaymentMethod) {
    if (!orderId) return

    setIsPaying(true)

    try {
      await api.post(API_ROUTES.orderPay(orderId), { method })
    } catch (error) {
      rejectOrder(orderErrorMessage(error, GENERIC_PAYMENT_REJECTION_REASON))
      setIsPaying(false)
      return
    }

    const resolved = await reconcileOrderStatus(orderId)
    if (!resolved) setIsPaying(false)
  }

  async function cancelOrder() {
    if (!orderId) return
    if (orderStatus !== 'Reserving' && orderStatus !== 'AwaitingPayment') return

    setIsCancelling(true)

    try {
      await api.post(API_ROUTES.orderCancel(orderId))
    } catch {
      setIsCancelling(false)
      return
    }

    const resolved = await reconcileOrderStatus(orderId)
    if (!resolved) setIsCancelling(false)
  }

  return {
    draft,
    cart,
    buyer,
    isReserving,
    isPaying,
    isCancelling,
    orderStatus,
    orderRejectionReason,
    setBuyer,
    addToDraft,
    removeDraftLine,
    resetPurchase,
    reservar,
    submitPayment,
    cancelOrder,
  }
}
