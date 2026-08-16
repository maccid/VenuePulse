import { ChevronDown, CircleCheck, CircleX, CreditCard, Loader2, Map, RefreshCw, Ticket, Trash2 } from 'lucide-react'

export function TicketReservationIcon() {
  return <Ticket className="cart-panel__icon" strokeWidth={1.8} aria-hidden="true" />
}

export function ChevronDownIcon() {
  return <ChevronDown aria-hidden="true" />
}

export function TrashIcon() {
  return <Trash2 className="cart-panel__remove-icon" aria-hidden="true" />
}

export function ReloadIcon() {
  return <RefreshCw className="cart-panel__remove-icon" aria-hidden="true" />
}

export function SuccessIcon() {
  return <CircleCheck className="cart-panel__result-icon" aria-hidden="true" />
}

export function FailureIcon() {
  return <CircleX className="cart-panel__result-icon" aria-hidden="true" />
}

export function SpinnerIcon() {
  return <Loader2 className="cart-panel__result-icon cart-panel__spinner" aria-hidden="true" />
}

export function MapIcon() {
  return <Map className="purchase-flow__map-icon" strokeWidth={1.8} aria-hidden="true" />
}

export function CardIcon() {
  return <CreditCard className="payment-method__icon" strokeWidth={1.8} aria-hidden="true" />
}

export function BizumIcon() {
  return (
    <svg className="payment-method__icon" viewBox="0 0 38.1 50" aria-hidden="true">
      <path
        fillRule="evenodd"
        clipRule="evenodd"
        fill="#05C0C7"
        d="M9.2,17.9c1.8,1.3,4.4,0.9,5.7-0.9l4.8-6.6C21,8.6,20.6,6,18.8,4.7c-1.8-1.3-4.4-0.9-5.7,0.9l-4.8,6.6 C7,14,7.4,16.5,9.2,17.9z M31,8.6c-1.8-1.3-4.4-0.9-5.7,0.9L6.1,35.8c-1.3,1.8-0.9,4.4,0.9,5.7c1.8,1.3,4.4,0.9,5.7-0.9l19.1-26.3 C33.2,12.5,32.8,9.9,31,8.6z M7.4,6.5c1.3-1.8,0.9-4.4-0.9-5.7C4.7-0.5,2.1-0.2,0.8,1.7C-0.5,3.5-0.2,6,1.7,7.4 C3.5,8.7,6,8.3,7.4,6.5z M36.4,42.6c-1.8-1.3-4.4-0.9-5.7,0.9c-1.3,1.8-0.9,4.4,0.9,5.7c1.8,1.3,4.4,0.9,5.7-0.9 C38.6,46.5,38.2,44,36.4,42.6z M28.9,32.2c-1.8-1.3-4.4-0.9-5.7,0.9l-4.8,6.6c-1.3,1.8-0.9,4.4,0.9,5.7c1.8,1.3,4.4,0.9,5.7-0.9 l4.8-6.6C31.1,36.1,30.7,33.5,28.9,32.2z"
      />
    </svg>
  )
}
