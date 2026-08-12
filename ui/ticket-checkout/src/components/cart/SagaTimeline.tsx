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
    default:
      return 'el pago simulado ha fallado (compensación real: aforo liberado)'
  }
}

export function SagaTimeline({ rejectionReason }: Props) {
  if (!rejectionReason) return null

  return <p className="saga-rejection-reason">Motivo: {reasonText(rejectionReason)}</p>
}
