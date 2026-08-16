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
}

export default function CartSummaryLines({ groups, activeCount, cartTotal, label, summaryOpen, onToggle }: Props) {
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
    </>
  )
}
