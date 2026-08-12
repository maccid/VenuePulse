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
    <section className="activity-feed">
      <h2>Actividad en vivo</h2>

      {entries.length === 0 ? (
        <p className="activity-feed__empty">Sin actividad todavía en este evento.</p>
      ) : (
        <ul className="activity-feed__list">
          {entries.map((entry) => (
            <li key={entry.id} className={`activity-feed__item activity-feed__item--${entry.status.toLowerCase()}`}>
              <span className="activity-feed__zone">{entry.zoneName}</span>
              <span>
                {STATUS_LABEL[entry.status]}
                {entry.status === 'Rejected' && entry.rejectionReason && ` (${entry.rejectionReason})`}
              </span>
              <time className="activity-feed__time">
                {new Date(entry.atUtc).toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
              </time>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}
