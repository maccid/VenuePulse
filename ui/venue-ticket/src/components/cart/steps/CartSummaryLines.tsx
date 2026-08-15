import type { CartLine } from '@venue/core/domain'
import { ChevronDownIcon } from '../../../shared/icons'
import { CartGroupRow } from '../CartGroupRow'
import { groupCartLines } from '../cartGrouping'

interface Props {
  groups: ReturnType<typeof groupCartLines>
  activeCount: number
  cartTotal: number
  label: string
  summaryOpen: boolean
  onToggle: () => void
  confirmedTickets: CartLine[]
}

export default function CartSummaryLines({
  groups,
  activeCount,
  cartTotal,
  label,
  summaryOpen,
  onToggle,
  confirmedTickets,
}: Props) {
  return (
    <>
      {summaryOpen && (
        <ul className="cart-panel__lines">
          {groups.map((group) => (
            <CartGroupRow key={group.key} group={group} />
          ))}
        </ul>
      )}

      <p className="cart-panel__total">
        <span className="cart-panel__quota">
          {activeCount} {label}
        </span>
        <span className="cart-panel__total-actions">
          <strong>{cartTotal.toFixed(2)} €</strong>
          <button
            type="button"
            className={`cart-panel__toggle${summaryOpen ? ' cart-panel__toggle--open' : ''}`}
            onClick={onToggle}
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
  )
}
