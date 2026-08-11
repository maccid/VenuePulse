import { useState } from 'react'
import type { BuyerData, CartLine, DraftLine, PaymentMethod } from '../types'
import { MAX_TICKETS_PER_PURCHASE } from '../types'
import { SagaTimeline } from '../SagaTimeline'
import { formatCountdown, useCountdown } from '../useCountdown'
import PaymentForm from './PaymentForm'

interface Props {
  draft: DraftLine[]
  cart: CartLine[]
  buyer: BuyerData
  isReserving: boolean
  isPaying: boolean
  onBuyerChange: (buyer: BuyerData) => void
  onRemoveDraftLine: (zoneId: string, ticketTypeId: string) => void
  onResetPurchase: () => void
  onReservar: () => void
  onSubmitPayment: (method: PaymentMethod) => void
}

interface CartGroup {
  key: string
  zoneName: string
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

// Icono de recarga para reiniciar la compra desde la fila de "Tu compra" (ver feedback de usuario).
function ReloadIcon() {
  return (
    <svg
      className="cart-panel__reload-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M21 12a9 9 0 1 1-2.64-6.36" />
      <polyline points="21 3 21 9 15 9" />
    </svg>
  )
}

// Flecha para desplegar/plegar el resumen de entradas de "Tu compra" junto al precio total.
function ChevronDownIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <polyline points="6 9 12 15 18 9" />
    </svg>
  )
}

// Icono de papelera para quitar una línea del borrador — el usuario lo pidió en vez del "×"
// (ver feedback de usuario); misma traza lucide-style que el resto de iconos del checkout.
function TrashIcon() {
  return (
    <svg
      className="cart-panel__remove-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M3 6h18" />
      <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6" />
      <path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
      <line x1="10" y1="11" x2="10" y2="17" />
      <line x1="14" y1="11" x2="14" y2="17" />
    </svg>
  )
}

function groupCartLines(cart: CartLine[]): CartGroup[] {
  const groups = new Map<string, CartGroup>()
  for (const line of cart) {
    const key = `${line.zoneId}|${line.ticketTypeName}`
    const group = groups.get(key)
    if (group) group.lines.push(line)
    else groups.set(key, { key, zoneName: line.zoneName, lines: [line] })
  }
  return [...groups.values()]
}

// Deadline de la cuenta atrás de TODA la compra = la más próxima entre sus líneas activas
// (deberían coincidir, al reservarse todas a la vez, pero se toma la más temprana por seguridad).
// Se muestra en la fila del título "Tu compra", no por grupo (ver feedback de usuario).
function earliestExpiry(lines: CartLine[]): string | null {
  const deadlines = lines
    .filter((l) => l.status !== 'Rejected' && l.expiresAtUtc)
    .map((l) => l.expiresAtUtc as string)
  if (deadlines.length === 0) return null
  return deadlines.reduce((earliest, d) => (new Date(d) < new Date(earliest) ? d : earliest))
}

// Componente propio por grupo del carrito real ("Tu compra"): el motivo de rechazo ya no se
// repite aquí por grupo — el timeline de la saga lo muestra una sola vez, al final (ver feedback
// de usuario).
function CartGroupRow({ group }: { group: CartGroup }) {
  const groupSubtotal = group.lines.filter((l) => l.status !== 'Rejected').reduce((sum, l) => sum + l.unitPrice, 0)

  return (
    <li className="cart-panel__line">
      <div className="cart-panel__line-header">
        <span>
          {group.zoneName} × {group.lines.length}
        </span>
        <span className="cart-panel__line-actions">
          <span className="cart-panel__subtotal">{groupSubtotal.toFixed(2)} €</span>
          <span className="cart-panel__line-slot" aria-hidden="true" />
        </span>
      </div>
    </li>
  )
}

export default function CartPanel({
  draft,
  cart,
  buyer,
  isReserving,
  isPaying,
  onBuyerChange,
  onRemoveDraftLine,
  onResetPurchase,
  onReservar,
  onSubmitPayment,
}: Props) {
  const hasReservation = cart.length > 0
  const activeLines = cart.filter((l) => l.status !== 'Rejected')
  const cartTotal = activeLines.reduce((sum, l) => sum + l.unitPrice, 0)
  const draftTotal = draft.reduce((sum, l) => sum + l.unitPrice * l.quantity, 0)
  const draftCount = draft.reduce((sum, l) => sum + l.quantity, 0)
  const awaitingPayment = cart.some((l) => l.status === 'AwaitingPayment' || l.status === 'ProcessingPayment')
  const allResolved =
    cart.length > 0 && cart.every((l) => l.status === 'Confirmed' || l.status === 'Rejected' || l.status === 'CheckedIn')
  const confirmedTickets = cart.filter((l) => l.status === 'Confirmed' || l.status === 'CheckedIn')
  const groups = groupCartLines(cart)

  // Los pasos de la saga ya no se muestran durante la compra (feedback de usuario: ruido) — el
  // botón de pagar indica "Procesando pago…" mientras trabaja. Aquí solo queda el motivo de
  // rechazo, que se pinta UNA sola vez, en vez de repetirlo por grupo.
  const rejectedLine = cart.find((l) => l.status === 'Rejected')
  const timelineReason = rejectedLine?.rejectionReason ?? null

  // Cuenta atrás de toda la compra en la fila del título, pegada a la derecha (ver feedback de
  // usuario). El hook vive aquí (no en un .map()) porque hay un único deadline por compra.
  const countdown = useCountdown(earliestExpiry(cart))

  // Cuando el plazo se agota, el resumen de precio deja de tener sentido (la reserva ha expirado
  // y la Saga la acabará rechazando) — se oculta el total junto con la cuenta atrás (feedback de
  // usuario). También se oculta si no queda ninguna línea activa (todo rechazado).
  const reservationExpired = countdown !== null && countdown <= 0

  // Resumen de entradas de "Tu compra" plegado por defecto: solo se ve el total, la flecha junto
  // al precio despliega/pliega la lista de grupos (ver feedback de usuario).
  const [summaryOpen, setSummaryOpen] = useState(false)

  return (
    <aside className="ticket-checkout cart-panel">
      <h3 className="cart-panel__title">
        <CartIcon />
        <span>{hasReservation ? 'Tu compra' : 'Tus Reservas'}</span>
        {hasReservation && countdown !== null && countdown > 0 && (
          <span className="cart-panel__countdown-inline">Reserva expira en {formatCountdown(countdown)}</span>
        )}
        {allResolved && (
          <button
            type="button"
            className="cart-panel__reload"
            onClick={onResetPurchase}
            aria-label="Reiniciar compra"
            title="Reiniciar compra"
          >
            <ReloadIcon />
          </button>
        )}
      </h3>

      {!hasReservation && draft.length === 0 && (
        <p className="cart-panel__empty">Elige una zona en el plano para añadir entradas.</p>
      )}

      {!hasReservation && draft.length > 0 && (
        <>
          <ul className="cart-panel__lines">
            {draft.map((line) => (
              <li key={`${line.zoneId}-${line.ticketTypeId}`} className="cart-panel__draft-line">
                <div className="cart-panel__line-header">
                  <span>
                    {line.zoneName} × {line.quantity}
                  </span>
                  <span className="cart-panel__line-actions">
                    <span className="cart-panel__subtotal">{(line.unitPrice * line.quantity).toFixed(2)} €</span>
                    <button
                      type="button"
                      className="cart-panel__remove"
                      aria-label={`Quitar ${line.zoneName}`}
                      onClick={() => onRemoveDraftLine(line.zoneId, line.ticketTypeId)}
                    >
                      <TrashIcon />
                    </button>
                  </span>
                </div>
              </li>
            ))}
          </ul>

          <p className="cart-panel__total">
            <span className="cart-panel__quota">
              {draftCount} / {MAX_TICKETS_PER_PURCHASE} entradas
            </span>
            <span className="cart-panel__total-actions">
              <strong>{draftTotal.toFixed(2)} €</strong>
              <span className="cart-panel__line-slot" aria-hidden="true" />
            </span>
          </p>

          <button type="button" className="cart-panel__reserve" disabled={isReserving} onClick={onReservar}>
            {isReserving ? (
              <>
                Reservando
                <span className="cart-panel__reserve-dots" aria-hidden="true">
                  <span>.</span>
                  <span>.</span>
                  <span>.</span>
                </span>
              </>
            ) : (
              'Reservar'
            )}
          </button>
        </>
      )}

      {hasReservation && (
        <>
          {summaryOpen && (
            <ul className="cart-panel__lines">
              {groups.map((group) => (
                <CartGroupRow key={group.key} group={group} />
              ))}
            </ul>
          )}

          {activeLines.length > 0 && !reservationExpired && (
            <>
              <p className="cart-panel__total">
                <span className="cart-panel__quota">
                  {activeLines.length} {awaitingPayment ? 'entradas reservadas' : 'entradas compradas'}
                </span>
                <span className="cart-panel__total-actions">
                  <strong>{cartTotal.toFixed(2)} €</strong>
                  <button
                    type="button"
                    className={`cart-panel__toggle${summaryOpen ? ' cart-panel__toggle--open' : ''}`}
                    onClick={() => setSummaryOpen((open) => !open)}
                    aria-expanded={summaryOpen}
                    aria-label={summaryOpen ? 'Ocultar resumen de entradas' : 'Ver resumen de entradas'}
                  >
                    <ChevronDownIcon />
                  </button>
                </span>
              </p>

              {confirmedTickets.length > 0 && (
                <div className="ticket-id-hint">
                  <p>
                    ID (para escanear en <em>venue-ops</em>):
                  </p>
                  <ul>
                    {confirmedTickets.map((t) => (
                      <li key={t.ticketId}>
                        <code>{t.ticketId}</code>
                      </li>
                    ))}
                  </ul>
                </div>
              )}
            </>
          )}

          {awaitingPayment && (
            <PaymentForm total={cartTotal} disabled={isPaying} buyer={buyer} onBuyerChange={onBuyerChange} onSubmit={onSubmitPayment} />
          )}

          {allResolved && !awaitingPayment && (
            <p className={`cart-panel__done${activeLines.length > 0 ? '' : ' cart-panel__done--error'}`}>
              {activeLines.length > 0 ? '✔ Compra completada.' : 'La compra no se pudo completar.'}
            </p>
          )}

          <SagaTimeline rejectionReason={timelineReason} />
        </>
      )}
    </aside>
  )
}
