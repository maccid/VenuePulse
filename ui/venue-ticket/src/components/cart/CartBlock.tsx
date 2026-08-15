import type { ReactNode } from 'react'

export function CartBlock({ children }: { children: ReactNode }) {
  return <div className="cart-panel__block">{children}</div>
}
