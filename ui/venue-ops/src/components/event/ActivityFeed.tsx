import type { ActivityEntry } from '../../shared/types'

interface Props {
  entries: ActivityEntry[]
}

const STATUS_LABEL: Record<ActivityEntry['status'], string> = {
  Pending: 'iniciando compra',
  AwaitingPayment: 'reservada, esperando pago',
  Confirmed: 'entrada confirmada',
  Rejected: 'entrada rechazada',
  CheckedIn: 'check-in realizado',
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
            const borderColor =
              entry.status === 'Confirmed'
                ? 'border-l-success'
                : entry.status === 'Rejected'
                  ? 'border-l-danger'
                  : entry.status === 'AwaitingPayment'
                    ? 'border-l-accent'
                    : 'border-l-border-strong'

            return (
              <li
                key={entry.id}
                className={`flex items-baseline gap-2 rounded-sm border-l-[3px] bg-bg px-3 py-2 text-sm ${borderColor}`}
              >
                <span className="font-[650] text-text">{entry.zoneName}</span>
                <span>
                  {STATUS_LABEL[entry.status]}
                  {entry.status === 'Rejected' && entry.rejectionReason && ` (${entry.rejectionReason})`}
                </span>
                <time className="ml-auto flex-shrink-0 text-xs text-text-muted">
                  {new Date(entry.atUtc).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
                </time>
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}
