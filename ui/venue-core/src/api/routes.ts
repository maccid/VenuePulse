export const API_ROUTES = {
  events: '/api/ticketing/api/events',
  eventTicketTypes: (eventId: string) => `/api/ticketing/api/events/${eventId}/ticket-types`,
  zones: (eventId: string) => `/api/access-control/api/zones?eventId=${eventId}`,
  simulationStatus: (eventId: string) => `/api/access-control/api/simulation/events/${eventId}/status`,
  simulationStart: (eventId: string) => `/api/access-control/api/simulation/events/${eventId}/start`,
  simulationStop: (eventId: string) => `/api/access-control/api/simulation/events/${eventId}/stop`,
  prediction: (eventId: string, eventStartsAtUtc: string) =>
    `/api/access-control/api/prediction/events/${eventId}?eventStartsAtUtc=${encodeURIComponent(eventStartsAtUtc)}`,
  orderStatus: (orderId: string) => `/api/checkin/api/orders/${orderId}/status`,
  orderCancel: (orderId: string) => `/api/checkin/api/orders/${orderId}/cancel`,
  orders: '/api/checkin/api/orders',
  orderPay: (orderId: string) => `/api/checkin/api/orders/${orderId}/pay`,
  ticketCheckIn: (ticketId: string) => `/api/checkin/api/tickets/${ticketId}/check-in`,
  ticketRevenue: (eventId: string) => `/api/checkin/api/tickets/events/${eventId}/revenue`,
} as const
