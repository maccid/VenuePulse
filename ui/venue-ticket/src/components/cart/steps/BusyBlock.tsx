import { SpinnerIcon } from '../../../shared/icons'
import { CartBlock } from './CartBlock'

export default function BusyBlock({ label }: { label: string }) {
  return (
    <CartBlock>
      <p className="cart-panel__result">
        <SpinnerIcon />
        <span className="cart-panel__done">{label}…</span>
      </p>
    </CartBlock>
  )
}
