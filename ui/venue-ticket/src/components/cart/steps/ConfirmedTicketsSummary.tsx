import { groupCartLines } from '../cartGrouping'
import { ConfirmedGroupRow } from '../CartGroupRow'

interface Props {
  groups: ReturnType<typeof groupCartLines>
  activeCount: number
  cartTotal: number
  groupId: string | null
}

export default function ConfirmedTicketsSummary({ groups, activeCount, cartTotal, groupId }: Props) {
  return (
    <>
      <ul className="cart-panel__lines">
        {groups.map((group) => (
          <ConfirmedGroupRow key={group.key} group={group} />
        ))}
      </ul>

      <p className="cart-panel__total">
        <span className="cart-panel__quota">{activeCount} entradas compradas</span>
        <span className="cart-panel__total-actions">
          <strong>{cartTotal.toFixed(2)} €</strong>
          <span className="cart-panel__line-slot" aria-hidden="true" />
        </span>
      </p>

      {groupId && activeCount > 1 && (
        <p className="cart-panel__group-code">
          Código de grupo: <code>group-{groupId}</code>
        </p>
      )}
    </>
  )
}
