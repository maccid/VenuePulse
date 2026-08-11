import type { CartLine, DraftLine, PaymentMethod, TicketStatus } from '../types'
import { MAX_TICKETS_PER_PURCHASE } from '../types'
import { SagaTimeline } from '../SagaTimeline'
import { formatCountdown, useCountdown } from '../useCountdown'
import PaymentForm from './PaymentForm'

interface Props {
  draft: DraftLine[]
  cart: CartLine[]
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

// Icono de carrito (sin librería de iconos, mismo criterio que el resto del proyecto — ver ADR 0017).
function CartIcon() {
  return (
    <svg
      className="cart-panel__icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <circle cx="8" cy="21" r="1" />
      <circle cx="19" cy="21" r="1" />
      <path d="M2.05 2.05h2l2.66 12.42a2 2 0 0 0 2 1.58h9.78a2 2 0 0 0 1.95-1.57l1.65-7.43H5.12" />
    </svg>
  )
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

function rejectionReasonText(rejectionReason: string | null): string {
  if (rejectionReason === 'ZoneFull') return 'la zona se llenó justo antes de confirmar'
  if (rejectionReason === 'ReservationTimedOut') return 'no se pudo reservar el aforo a tiempo'
  if (rejectionReason === 'PaymentTimedOut') return 'no se eligió método de pago a tiempo, el aforo se liberó'
  return 'el pago simulado ha fallado (compensación real: aforo liberado)'
}

// Componente propio por fila para poder llamar a `useCountdown` una vez por grupo — el número de
// grupos cambia con el carrito, así que el hook no puede vivir en el cuerpo de un `.map()` del
// componente padre (violaría las reglas de hooks). El timeline de la saga es común a toda la
// compra y se muestra una sola vez, debajo del método de pago.
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
      {secondsLeft !== null && secondsLeft > 0 && (
        <p className="cart-panel__countdown">Reserva expira en {formatCountdown(secondsLeft)}</p>
      )}
      {status === 'Rejected' ? (
        <p className="saga-rejection-reason">Motivo: {rejectionReasonText(rejectionReason)}</p>
      ) : (
        rejectedCount > 0 && (
          <p className="saga-rejection-reason">
            {rejectedCount} de {group.lines.length} entradas de esta zona se rechazaron.
          </p>
        )
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
  const allRejected = cart.length > 0 && activeLines.length === 0
  const groups = groupCartLines(cart)

  // Timeline común de toda la compra (no por zona — ver feedback de producto): representa la saga
  // completa de reservar → esperar pago → procesar pago → confirmar → entrada emitida (ADR 0025).
  const phase =
    isReserving
      ? 'Purchasing'
      : awaitingPayment
        ? 'AwaitingPayment'
        : isPaying
          ? 'Paying'
          : allResolved && activeLines.length > 0
            ? 'Confirmed'
            : 'Pending'

  return (
    <aside className="ticket-checkout cart-panel">
      <h3 className="cart-panel__title">
        <CartIcon />
        <span>{hasReservation ? 'Tu compra' : 'Tus Reservas'}</span>
      </h3>

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

          <button type="button" disabled={isReserving} onClick={onReservar}>
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
            <PaymentForm total={cartTotal} disabled={isPaying} onSubmit={onSubmitPayment} />
          )}

          {allResolved && !awaitingPayment && (
            <p className="cart-panel__done">
              {activeLines.length > 0 ? '✔ Compra completada.' : 'La compra no se pudo completar.'}
            </p>
          )}

          {!allRejected && <SagaTimeline status={phase} rejectionReason={null} />}
        </>
      )}
    </aside>
  )
}
