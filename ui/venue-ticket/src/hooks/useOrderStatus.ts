import { useCallback, useEffect } from 'react'
import type {
  OrderStatus,
  OrderStatusUpdate,
  OrderTicketStatus,
  TicketStatusUpdate,
} from '@venue/core/domain'
import { isResolvedOrderStatus } from '@venue/core/domain'
import { API_ROUTES, api } from '@venue/core/api'
import { orderStore } from '@venue/core/state'
import { sessionStorageService } from '@venue/core/storage'
import { isAbortedHubStartError, createHubConnection } from '@venue/core/hub'

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

interface OrderStatusHook {
  orderId: string | null
  orderStatus: OrderStatus | null
  orderRejectionReason: string | null
  orderTickets: OrderTicketStatus[]
  claimOrder: (orderId: string, status?: OrderStatus | null) => void
  applyOrderStatus: (update: OrderStatusUpdate) => void
  rejectOrder: (reason: string) => void
  reconcile: (orderId: string, attempts?: number) => Promise<boolean>
  resetOrderStatus: () => void
}

export function useOrderStatus(eventId: string): OrderStatusHook {
  const orderId = orderStore.use((s) => s.orderId)
  const orderStatus = orderStore.use((s) => s.status)
  const orderRejectionReason = orderStore.use((s) => s.rejectionReason)
  const orderTickets = orderStore.use((s) => s.tickets)
  const setOrder = orderStore.use((s) => s.setOrder)
  const updateTicketStatus = orderStore.use((s) => s.updateTicketStatus)
  const resetOrder = orderStore.use((s) => s.reset)

  const claimOrder = useCallback(
    (id: string, status?: OrderStatus | null) => {
      setOrder({ orderId: id, status: status ?? orderStore.getState().status })
      writeStoredOrderId(eventId, id)
    },
    [setOrder, eventId],
  )

  const applyOrderStatus = useCallback(
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

  const applyTicketStatus = useCallback(
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

  const reconcile = useCallback(
    async (id: string, attempts = 20): Promise<boolean> => {
      for (let attempt = 0; attempt < attempts; attempt++) {
        await new Promise((resolve) => setTimeout(resolve, 1000))
        try {
          const update = await api.get<OrderStatusUpdate>(API_ROUTES.orderStatus(id))
          applyOrderStatus(update)
          if (isResolvedOrderStatus(update.status)) return true
        } catch {
          // red caída o similar: se reintenta en la siguiente vuelta
        }
      }
      return isResolvedOrderStatus(orderStore.getState().status)
    },
    [applyOrderStatus],
  )

  const resetOrderStatus = useCallback(() => {
    writeStoredOrderId(eventId, null)
    resetOrder()
  }, [eventId, resetOrder])

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
      applyTicketStatus(update)
    })

    connection.on('orderStatusChanged', (update: OrderStatusUpdate) => {
      applyOrderStatus(update)
    })

    async function joinEvent() {
      await connection.invoke('JoinEvent', eventId).catch(() => undefined)
    }

    connection.onreconnected(() => {
      void joinEvent()
      const { orderId: currentOrderId, status } = orderStore.getState()
      if (currentOrderId && !isResolvedOrderStatus(status)) void reconcile(currentOrderId)
    })

    connection.start().then(joinEvent).catch((err) => {
      if (!isAbortedHubStartError(err)) console.error('No se pudo conectar al hub de entradas', err)
    })

    return () => {
      connection.stop()
    }
  }, [eventId, applyOrderStatus, applyTicketStatus, reconcile])

  return {
    orderId,
    orderStatus,
    orderRejectionReason,
    orderTickets,
    claimOrder,
    applyOrderStatus,
    rejectOrder,
    reconcile,
    resetOrderStatus,
  }
}
