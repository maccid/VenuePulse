import type { ReactNode } from 'react'
import { FailureIcon, SuccessIcon } from '../../../shared/icons'
import { CartBlock } from './CartBlock'

interface Props {
  kind: 'success' | 'error'
  title: string
  onResetPurchase: () => void
  children?: ReactNode
}

export default function ResultBlock({ kind, title, onResetPurchase, children }: Props) {
  return (
    <CartBlock>
      <p className={`cart-panel__result${kind === 'error' ? ' cart-panel__result--error' : ''}`}>
        {kind === 'success' ? <SuccessIcon /> : <FailureIcon />}
        <span className={`cart-panel__done${kind === 'error' ? ' cart-panel__done--error' : ''}`}>{title}</span>
      </p>
      {children}
      <button type="button" onClick={onResetPurchase}>
        Reiniciar compra
      </button>
    </CartBlock>
  )
}
