interface Props {
  rejectionReason: string | null
}

function reasonText(rejectionReason: string): string {
  switch (rejectionReason) {
    case 'ZoneFull':
      return 'la zona se llenó justo antes de confirmar'
    case 'ReservationTimedOut':
      return 'no se pudo reservar el aforo a tiempo'
    case 'PaymentTimedOut':
      return 'no se eligió método de pago a tiempo, el aforo se liberó'
    case 'TooManyTickets':
      return 'el pedido superaba el máximo de entradas permitido por compra'
    case 'PricingUnavailable':
      return 'no se pudieron obtener los precios para completar el pedido'
    case 'PriceChanged':
      return 'el precio cambió justo antes de confirmar tu pedido'
    case 'InsufficientAvailability':
      return 'no había aforo suficiente para las unidades solicitadas'
    case 'CancelledByCustomer':
      return 'cancelaste la reserva'
    case 'EventCancelled':
      return 'el evento fue cancelado'
    case 'PaymentFailed':
      return 'el pago simulado ha fallado (compensación real: aforo liberado)'
    case 'OrderRequestFailed':
      return 'no se pudo procesar el pedido, inténtalo de nuevo'
    case 'PaymentRequestFailed':
      return 'no se pudo procesar el pago, inténtalo de nuevo'
    default:
      return 'no se pudo completar la compra'
  }
}

export function SagaTimeline({ rejectionReason }: Props) {
  if (!rejectionReason) return null

  return <p className="saga-rejection-reason">Motivo: {reasonText(rejectionReason)}</p>
}
