export function CartIcon() {
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

export function ReloadIcon() {
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

export function ChevronDownIcon() {
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

export function TrashIcon() {
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

export function MapIcon() {
  return (
    <svg
      className="purchase-flow__map-icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21" />
      <line x1="9" y1="3" x2="9" y2="18" />
      <line x1="15" y1="6" x2="15" y2="21" />
    </svg>
  )
}

export function CardIcon() {
  return (
    <svg
      className="payment-method__icon"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <rect x="2" y="5" width="20" height="14" rx="2" />
      <line x1="2" y1="10" x2="22" y2="10" />
    </svg>
  )
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
