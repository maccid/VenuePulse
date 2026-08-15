import { CartBlock } from './CartBlock'

export default function BusyBlock({ label }: { label: string }) {
  return (
    <CartBlock>
      <p className="cart-panel__block-busy">
        {label}
        <span className="cart-panel__reserve-dots" aria-hidden="true">
          <span>.</span>
          <span>.</span>
          <span>.</span>
        </span>
      </p>
    </CartBlock>
  )
}
