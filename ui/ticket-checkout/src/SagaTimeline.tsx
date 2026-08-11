interface Props {
  rejectionReason: string | null
}

// De la saga ya no se pintan los pasos (Reservando aforo → … → Entrada emitida): el feedback de
// usuario los consideraba ruido durante la compra — el botón de pagar ya indica "Procesando pago…"
// mientras la saga trabaja (ver PaymentForm). Lo único que conserva valor es el motivo de rechazo,
// que se muestra UNA sola vez aquí, en vez de repetirlo por grupo (ver feedback de producto).
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

  return (
    <p className="saga-rejection-reason">
      Motivo: {reasonText(rejectionReason)}
    </p>
  )
}
