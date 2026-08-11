import type { CartLine, DraftLine, PaymentMethod, TicketStatus } from '../types'
import { MAX_TICKETS_PER_PURCHASE } from '../types'
import { SagaTimeline } from '../SagaTimeline'
import { formatCountdown, useCountdown } from '../useCountdown'
import PaymentForm from './PaymentForm'

interface Props {
  draft: DraftLine[]
  cart: CartLine[]
  holder: string
  onHolderChange: (holder: string) => void
  isReserving: boolean
  isPaying: boolean
  onReservar: () => void
  onSubmitPayment: (method: PaymentMethod) => void
}

interface CartGroup {
  key: string
  zoneName: string
  ticketTypeName: string
  lines: CartLine[]
}

function groupCartLines(cart: CartLine[]): CartGroup[] {
  const groups = new Map<string, CartGroup>()
  for (const line of cart) {
    const key = `${line.zoneId}|${line.ticketTypeName}`
    const group = groups.get(key)
    if (group) group.lines.push(line)
    else groups.set(key, { key, zoneName: line.zoneName, ticketTypeName: line.ticketTypeName, lines: [line] })
  }
  return [...groups.values()]
}

// Estado agregado del grupo = el menos avanzado de sus líneas no rechazadas — si una entrada del
// grupo sigue "Pending" mientras otra ya está "AwaitingPayment", el grupo se muestra como
// "Pending" (el paso real por el que hay que esperar), ver ADR 0025.
const STATUS_ORDER: TicketStatus[] = ['Pending', 'AwaitingPayment', 'Confirmed', 'CheckedIn']

function groupStatus(group: CartGroup): { status: TicketStatus | 'Rejected'; rejectionReason: string | null } {
  const active = group.lines.filter((l) => l.status !== 'Rejected')
  if (active.length === 0) {
    return { status: 'Rejected', rejectionReason: group.lines[0]?.rejectionReason ?? null }
  }
  const worst = active.reduce((worst, l) =>
    STATUS_ORDER.indexOf(l.status) < STATUS_ORDER.indexOf(worst.status) ? l : worst,
  )
  return { status: worst.status, rejectionReason: null }
}

// Deadline de la cuenta atrás del grupo = la más próxima entre sus líneas activas (deberían
// coincidir, al reservarse todas a la vez, pero se toma la más temprana por seguridad).
function earliestExpiry(lines: CartLine[]): string | null {
  const deadlines = lines
    .filter((l) => l.status !== 'Rejected' && l.expiresAtUtc)
    .map((l) => l.expiresAtUtc as string)
  if (deadlines.length === 0) return null
  return deadlines.reduce((earliest, d) => (new Date(d) < new Date(earliest) ? d : earliest))
}

// Componente propio por fila para poder llamar a `useCountdown` una vez por grupo — el número de
// grupos cambia con el carrito, así que el hook no puede vivir en el cuerpo de un `.map()` del
// componente padre (violaría las reglas de hooks).
function CartGroupRow({ group }: { group: CartGroup }) {
  const { status, rejectionReason } = groupStatus(group)
  const rejectedCount = group.lines.length - group.lines.filter((l) => l.status !== 'Rejected').length
  const confirmedTickets = group.lines.filter((l) => l.status === 'Confirmed' || l.status === 'CheckedIn')
  const groupSubtotal = group.lines.filter((l) => l.status !== 'Rejected').reduce((sum, l) => sum + l.unitPrice, 0)
  const secondsLeft = useCountdown(earliestExpiry(group.lines))

  return (
    <li className="cart-panel__line">
      <div className="cart-panel__line-header">
        <span>
          {group.zoneName} × {group.lines.length}
        </span>
        <span>{groupSubtotal.toFixed(2)} €</span>
      </div>
      <SagaTimeline status={status} rejectionReason={rejectionReason} />
      {secondsLeft !== null && secondsLeft > 0 && (
        <p className="cart-panel__countdown">Reserva expira en {formatCountdown(secondsLeft)}</p>
      )}
      {rejectedCount > 0 && status !== 'Rejected' && (
        <p className="saga-rejection-reason">
          {rejectedCount} de {group.lines.length} entradas de esta zona se rechazaron.
        </p>
      )}
      {confirmedTickets.length > 0 && (
        <p className="ticket-id-hint">
          ID (para escanear en <em>venue-ops</em>):{' '}
          {confirmedTickets.map((t, i) => (
            <span key={t.ticketId}>
              {i > 0 && ', '}
              <code>{t.ticketId}</code>
            </span>
          ))}
        </p>
      )}
    </li>
  )
}

export default function CartPanel({
  draft,
  cart,
  holder,
  onHolderChange,
  isReserving,
  isPaying,
  onReservar,
  onSubmitPayment,
}: Props) {
  const hasReservation = cart.length > 0
  const activeLines = cart.filter((l) => l.status !== 'Rejected')
  const cartTotal = activeLines.reduce((sum, l) => sum + l.unitPrice, 0)
  const draftTotal = draft.reduce((sum, l) => sum + l.unitPrice * l.quantity, 0)
  const draftCount = draft.reduce((sum, l) => sum + l.quantity, 0)
  const awaitingPayment = cart.some((l) => l.status === 'AwaitingPayment')
  const allResolved =
    cart.length > 0 && cart.every((l) => l.status === 'Confirmed' || l.status === 'Rejected' || l.status === 'CheckedIn')
  const holderReady = holder.trim().length > 0
  const groups = groupCartLines(cart)

  return (
    <aside className="cart-panel">
      <h3>{hasReservation ? 'Tu compra' : 'Tus Reservas'}</h3>

      {!hasReservation && draft.length === 0 && (
        <p className="cart-panel__empty">Elige una zona en el plano para añadir entradas.</p>
      )}

      {!hasReservation && draft.length > 0 && (
        <>
          <ul className="cart-panel__lines">
            {draft.map((line) => (
              <li key={`${line.zoneId}-${line.ticketTypeId}`} className="cart-panel__line-header">
                <span>
                  {line.zoneName} × {line.quantity}
                </span>
                <span>{(line.unitPrice * line.quantity).toFixed(2)} €</span>
              </li>
            ))}
          </ul>

          <p className="cart-panel__total">
            Total <strong>{draftTotal.toFixed(2)} €</strong>
          </p>
          <p className="cart-panel__quota">
            {draftCount} / {MAX_TICKETS_PER_PURCHASE} entradas
          </p>

          <label className="field">
            Tu nombre
            <input
              type="text"
              value={holder}
              onChange={(e) => onHolderChange(e.target.value)}
              placeholder="¿A nombre de quién reservas?"
              required
            />
          </label>

          <button type="button" disabled={!holderReady || isReserving} onClick={onReservar}>
            {isReserving ? 'Reservando…' : 'Reservar'}
          </button>
        </>
      )}

      {hasReservation && (
        <>
          <ul className="cart-panel__lines">
            {groups.map((group) => (
              <CartGroupRow key={group.key} group={group} />
            ))}
          </ul>

          <p className="cart-panel__total">
            Total <strong>{cartTotal.toFixed(2)} €</strong>
          </p>
          <p className="cart-panel__quota">
            {activeLines.length} / {MAX_TICKETS_PER_PURCHASE} entradas
          </p>

          {awaitingPayment && (
            <PaymentForm holder={holder} total={cartTotal} disabled={isPaying} onSubmit={onSubmitPayment} />
          )}

          {allResolved && !awaitingPayment && (
            <p className="cart-panel__done">
              {activeLines.length > 0 ? '✔ Compra completada.' : 'La compra no se pudo completar.'}
            </p>
          )}
        </>
      )}
    </aside>
  )
}
