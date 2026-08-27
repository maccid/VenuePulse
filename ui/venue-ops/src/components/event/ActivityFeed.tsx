import type { ActivityEntry } from '../../shared/types'

interface Props {
  entries: ActivityEntry[]
}

const GROUP_PREFIX = 'group-'

const TICKET_STATUS_LABEL: Record<ActivityEntry['status'], string> = {
  Pending: 'iniciando compra',
  AwaitingPayment: 'reservada, esperando pago',
  ProcessingPayment: 'procesando pago',
  Confirmed: 'entrada confirmada',
  Rejected: 'entrada rechazada',
  CheckedIn: 'check-in realizado',
  Refunded: 'entrada reembolsada',
}

const REJECTION_REASON_LABEL: Record<string, string> = {
  ZoneFull: 'zona llena',
  InsufficientAvailability: 'disponibilidad insuficiente',
  ReservationTimedOut: 'se agotó el tiempo de reserva',
  PaymentTimedOut: 'se agotó el tiempo de pago',
  PaymentFailed: 'fallo en el pago',
  EventCancelled: 'evento cancelado',
  CancelledByCustomer: 'cancelado por el cliente',
}

function rejectionReasonLabel(reason: string | null): string | null {
  if (!reason) return null
  const label = REJECTION_REASON_LABEL[reason] ?? reason
  return label.charAt(0).toUpperCase() + label.slice(1)
}

const ORDER_TIMELINE: Record<string, string[]> = {
  AwaitingPayment: ['Reservando', 'Esperando pago'],
  Completed: ['Reservando', 'Esperando pago', 'Confirmando', 'Completado'],
  Rejected: ['Reservando', 'Rechazado'],
}

function borderColorFor(status: ActivityEntry['status']) {
  return status === 'Confirmed'
    ? 'border-l-success'
    : status === 'Rejected'
      ? 'border-l-danger'
      : status === 'AwaitingPayment'
        ? 'border-l-accent'
        : 'border-l-border-strong'
}

export default function ActivityFeed({ entries }: Props) {
  return (
    <section>
      <h2 className="mb-3">Actividad en vivo</h2>

      {entries.length === 0 ? (
        <p className="m-0 text-sm text-text-muted">Sin actividad todavía en este evento.</p>
      ) : (
        <ul className="m-0 flex max-h-[260px] list-none flex-col gap-2 overflow-y-auto p-0">
          {entries.map((entry) => {
            const isOrder = entry.orderId !== null

            return (
              <li
                key={entry.id}
                className={`relative rounded-sm border-l-[3px] bg-bg px-3 py-2 text-sm ${borderColorFor(entry.status)}`}
              >
                {isOrder ? (
                  <div className="flex flex-col gap-1">
                    <div className="flex items-baseline gap-2">
                      <span className="inline-flex items-center gap-1 rounded-full bg-accent/15 px-1.5 py-0.5 text-[10px] font-semibold text-accent">
                        Pedido
                      </span>
                      <span className="font-[650] text-text">{entry.ticketTypeSummary ?? `${entry.ticketCount} entradas`}</span>
                      <time className="ml-auto flex-shrink-0 text-xs text-text-muted">
                        {new Date(entry.atUtc).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                      </time>
                    </div>
                    {entry.status === 'Confirmed' ? (
                      entry.ticketIds?.length === 1 ? (
                        <div className="text-[10px] text-text-muted">
                          <code className="truncate text-text">{entry.ticketIds[0]}</code>
                        </div>
                      ) : (
                        <>
                          <details className="group text-[10px] text-text-muted">
                            <summary className="cursor-pointer list-none marker:content-none">
                              <span className="flex items-center gap-2">
                                <code className="min-w-0 truncate text-text" title={entry.orderId ? GROUP_PREFIX + entry.orderId : undefined}>
                                  {entry.orderId ? GROUP_PREFIX + entry.orderId : ''}
                                </code>
                                <span className="ml-auto flex-shrink-0 font-medium text-accent">Ver entradas del pedido</span>
                                <span className="flex-shrink-0 text-border-strong transition-transform group-open:rotate-90">›</span>
                              </span>
                            </summary>
                            <div className="mt-2 flex flex-wrap gap-1 rounded border border-border-strong p-2">
                              {entry.ticketIds?.map((id) => (
                                <code key={id} className="truncate rounded bg-border-strong/20 px-1 py-0.5">{id}</code>
                              ))}
                            </div>
                          </details>
                        </>
                      )
                    ) : (
                      ORDER_TIMELINE[entry.status] && (
                        <div className="flex items-center gap-1 text-[10px] text-text-muted">
                          {ORDER_TIMELINE[entry.status].map((step, i, arr) => (
                            <span key={step} className="flex items-center gap-1">
                              <span className={i === arr.length - 1 ? 'font-semibold text-text' : ''}>{step}</span>
                              {i < arr.length - 1 && <span className="text-border-strong">→</span>}
                            </span>
                          ))}
                          {entry.status === 'Rejected' && entry.rejectionReason && (
                            <span className="ml-1 font-semibold text-danger">{rejectionReasonLabel(entry.rejectionReason)}</span>
                          )}
                        </div>
                      )
                    )}
                  </div>
                ) : (
                  <div className="flex items-baseline gap-2">
                    <span className="font-[650] text-text">{entry.zoneName}</span>
                    <span>
                      {TICKET_STATUS_LABEL[entry.status]}
                      {entry.status === 'Rejected' && entry.rejectionReason && (
                        <span className="ml-1 text-danger">({rejectionReasonLabel(entry.rejectionReason)})</span>
                      )}
                    </span>
                    <time className="ml-auto flex-shrink-0 text-xs text-text-muted">
                      {new Date(entry.atUtc).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                    </time>
                  </div>
                )}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
