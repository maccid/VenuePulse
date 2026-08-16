import type { CartGroup } from './cartGrouping'

export function CartGroupRow({ group }: { group: CartGroup }) {
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

export function ConfirmedGroupRow({ group }: { group: CartGroup }) {
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
      <ul className="cart-panel__group-tickets">
        {group.lines.map((line) => (
          <li key={line.ticketId}>
            <code>{line.ticketId}</code>
          </li>
        ))}
      </ul>
    </li>
  )
}
