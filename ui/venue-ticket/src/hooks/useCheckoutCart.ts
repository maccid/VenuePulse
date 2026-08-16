import { API_ROUTES, ApiError, api } from '@venue/core/api'
import type {
  BuyerData,
  CartLine,
  DraftLine,
  OrderLine,
  OrderStatus,
  OrderStatusUpdate,
  PaymentMethod,
  TicketType,
  ZoneOccupancy,
} from '@venue/core/domain'
import { isResolvedOrderStatus } from '@venue/core/domain'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { useOrderStatus } from './useOrderStatus'

const GENERIC_ORDER_REJECTION_REASON = 'OrderRequestFailed'
const GENERIC_PAYMENT_REJECTION_REASON = 'PaymentRequestFailed'

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
  orderId: string | null
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

  const {
    orderId,
    orderStatus,
    orderRejectionReason,
    orderTickets,
    claimOrder,
    applyOrderStatus,
    rejectOrder,
    reconcile,
    resetOrderStatus,
  } = useOrderStatus(eventId)

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

  useEffect(() => {
    if (!isResolvedOrderStatus(orderStatus)) return
    if (isPaying) setIsPaying(false)
    if (isCancelling) setIsCancelling(false)
  }, [orderStatus, isPaying, isCancelling])

  const addToDraft = useCallback(
    (zoneId: string, ticketTypeId: string, quantity: number) => {
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
    },
    [zones, ticketTypes],
  )

  const removeDraftLine = useCallback((zoneId: string, ticketTypeId: string) => {
    setDraft((prev) => prev.filter((line) => !(line.zoneId === zoneId && line.ticketTypeId === ticketTypeId)))
  }, [])

  const resetPurchase = useCallback(() => {
    resetOrderStatus()
    setDraft([])
    setBuyer({ nombre: '', apellidos: '', email: '' })
    setIsPaying(false)
    setIsReserving(false)
    setIsCancelling(false)
  }, [resetOrderStatus])

  const waitUntilPayable = useCallback(
    async (id: string) => {
      for (let attempt = 0; attempt < 20; attempt++) {
        await new Promise((resolve) => setTimeout(resolve, 1000))
        try {
          const update = await api.get<OrderStatusUpdate>(API_ROUTES.orderStatus(id))
          applyOrderStatus(update)
          if (update.status !== 'Reserving') return
        } catch {
        }
      }
    },
    [applyOrderStatus],
  )

  const reservar = useCallback(async () => {
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

      claimOrder(newOrderId, 'Reserving')
      setDraft([])

      try {
        const status = await api.get<OrderStatusUpdate>(API_ROUTES.orderStatus(newOrderId))
        applyOrderStatus(status)
        if (status.status === 'Reserving') await waitUntilPayable(newOrderId)
      } catch {
        claimOrder(newOrderId, 'Reserving')
      }
    } catch (error) {
      rejectOrder(orderErrorMessage(error, GENERIC_ORDER_REJECTION_REASON))
      setDraft([])
    } finally {
      setIsReserving(false)
    }
  }, [draft, ticketTypes, eventId, buyer.nombre, holder, claimOrder, applyOrderStatus, rejectOrder, waitUntilPayable])

  const submitPayment = useCallback(
    async (method: PaymentMethod) => {
      if (!orderId) return

      setIsPaying(true)

      try {
        await api.post(API_ROUTES.orderPay(orderId), { method })
      } catch (error) {
        rejectOrder(orderErrorMessage(error, GENERIC_PAYMENT_REJECTION_REASON))
        setIsPaying(false)
        return
      }

      const resolved = await reconcile(orderId)
      if (!resolved) setIsPaying(false)
    },
    [orderId, rejectOrder, reconcile],
  )

  const cancelOrder = useCallback(async () => {
    if (!orderId) return
    if (orderStatus !== 'Reserving' && orderStatus !== 'AwaitingPayment') return

    setIsCancelling(true)

    try {
      await api.post(API_ROUTES.orderCancel(orderId))
    } catch (error) {
      try {
        const status = await api.get<OrderStatusUpdate>(API_ROUTES.orderStatus(orderId))
        applyOrderStatus(status)
      } catch {
        rejectOrder(orderErrorMessage(error, GENERIC_ORDER_REJECTION_REASON))
      }
      setIsCancelling(false)
      return
    }

    resetPurchase()
  }, [orderId, orderStatus, resetPurchase, applyOrderStatus, rejectOrder])

  return {
    draft,
    cart,
    buyer,
    isReserving,
    isPaying,
    isCancelling,
    orderId,
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
