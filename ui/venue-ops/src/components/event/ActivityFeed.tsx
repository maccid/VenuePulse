import type { ActivityEntry } from '../../shared/types'

interface Props {
  entries: ActivityEntry[]
}

const TICKET_STATUS_LABEL: Record<ActivityEntry['status'], string> = {
  Pending: 'iniciando compra',
  AwaitingPayment: 'reservada, esperando pago',
  ProcessingPayment: 'procesando pago',
  Confirmed: 'entrada confirmada',
  Rejected: 'entrada rechazada',
  CheckedIn: 'check-in realizado',
  Refunded: 'entrada reembolsada',
}

const ORDER_STATUS_LABEL: Record<string, string> = {
  AwaitingPayment: 'reservado, esperando pago',
  Completed: 'pedido completado',
  Rejected: 'pedido rechazado',
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
                className={`rounded-sm border-l-[3px] bg-bg px-3 py-2 text-sm ${borderColorFor(entry.status)}`}
              >
                {isOrder ? (
                  <div className="flex flex-col gap-1">
                    <div className="flex items-baseline gap-2">
                      <span className="inline-flex items-center gap-1 rounded-full bg-accent/15 px-1.5 py-0.5 text-[10px] font-semibold text-accent">
                        Pedido
                      </span>
                      <span className="font-[650] text-text">{entry.ticketTypeSummary ?? `${entry.ticketCount} entradas`}</span>
                      <span>
                        · {ORDER_STATUS_LABEL[entry.status] ?? entry.status}
                      </span>
                      <time className="ml-auto flex-shrink-0 text-xs text-text-muted">
                        {new Date(entry.atUtc).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                      </time>
                    </div>
                    {ORDER_TIMELINE[entry.status] && (
                      <div className="flex items-center gap-1 text-[10px] text-text-muted">
                        {ORDER_TIMELINE[entry.status].map((step, i, arr) => (
                          <span key={step} className="flex items-center gap-1">
                            <span className={i === arr.length - 1 ? 'font-semibold text-text' : ''}>{step}</span>
                            {i < arr.length - 1 && <span className="text-border-strong">→</span>}
                          </span>
                        ))}
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="flex items-baseline gap-2">
                    <span className="font-[650] text-text">{entry.zoneName}</span>
                    <span>
                      {TICKET_STATUS_LABEL[entry.status]}
                      {entry.status === 'Rejected' && entry.rejectionReason && ` (${entry.rejectionReason})`}
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
