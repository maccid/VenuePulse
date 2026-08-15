export const queryKeys = {
  events: ['events'] as const,
  event: (id: string) => ['events', id] as const,
  ticketTypes: (eventId: string) => ['events', eventId, 'ticket-types'] as const,
  zones: (eventId: string) => ['zones', eventId] as const,
  orderStatus: (orderId: string) => ['orders', orderId, 'status'] as const,
  simulationStatus: (eventId: string) => ['simulation', eventId, 'status'] as const,
  prediction: (eventId: string) => ['prediction', eventId] as const,
  revenue: (eventId: string) => ['revenue', eventId] as const,
} as const

export const mutationKeys = {
  createEvent: ['events', 'create'] as const,
  createOrder: ['orders', 'create'] as const,
  payOrder: (orderId: string) => ['orders', orderId, 'pay'] as const,
  checkIn: ['tickets', 'check-in'] as const,
} as const
