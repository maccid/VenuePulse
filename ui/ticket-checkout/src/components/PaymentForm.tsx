import { useState } from 'react'
import type { BuyerData, PaymentMethod } from '../types'

interface Props {
  total: number
  disabled: boolean
  buyer: BuyerData
  onBuyerChange: (buyer: BuyerData) => void
  onSubmit: (method: PaymentMethod) => void
}

// "Mis Datos" es obligatorio: "Pagar todo" no se habilita hasta completar Nombre/Apellidos/Correo
// (ver feedback de usuario). El Nombre ya no se pide en "Tus Reservas": se captura aquí, al pagar.
// Apellidos/correo son solo visuales (no los persiste el backend, que solo modela un "holder" de
// texto libre); el holder de cada entrada viaja en el POST de reserva con el nombre del widget.
function CardIcon() {
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

function BizumIcon() {
  return (
    <svg
      className="payment-method__icon"
      viewBox="0 0 38.1 50"
      aria-hidden="true"
    >
      <path
        fillRule="evenodd"
        clipRule="evenodd"
        fill="#05C0C7"
        d="M9.2,17.9c1.8,1.3,4.4,0.9,5.7-0.9l4.8-6.6C21,8.6,20.6,6,18.8,4.7c-1.8-1.3-4.4-0.9-5.7,0.9l-4.8,6.6 C7,14,7.4,16.5,9.2,17.9z M31,8.6c-1.8-1.3-4.4-0.9-5.7,0.9L6.1,35.8c-1.3,1.8-0.9,4.4,0.9,5.7c1.8,1.3,4.4,0.9,5.7-0.9l19.1-26.3 C33.2,12.5,32.8,9.9,31,8.6z M7.4,6.5c1.3-1.8,0.9-4.4-0.9-5.7C4.7-0.5,2.1-0.2,0.8,1.7C-0.5,3.5-0.2,6,1.7,7.4 C3.5,8.7,6,8.3,7.4,6.5z M36.4,42.6c-1.8-1.3-4.4-0.9-5.7,0.9c-1.3,1.8-0.9,4.4,0.9,5.7c1.8,1.3,4.4,0.9,5.7-0.9 C38.6,46.5,38.2,44,36.4,42.6z M28.9,32.2c-1.8-1.3-4.4-0.9-5.7,0.9l-4.8,6.6c-1.3,1.8-0.9,4.4,0.9,5.7c1.8,1.3,4.4,0.9,5.7-0.9 l4.8-6.6C31.1,36.1,30.7,33.5,28.9,32.2z"
      />
    </svg>
  )
}

export default function PaymentForm({ total, disabled, buyer, onBuyerChange, onSubmit }: Props) {
  const [method, setMethod] = useState<PaymentMethod>('Card')
  const dataComplete =
    buyer.nombre.trim() !== '' && buyer.apellidos.trim() !== '' && buyer.email.trim() !== ''

  return (
    <form
      className="payment-form"
      onSubmit={(e) => {
        e.preventDefault()
        if (method && dataComplete) onSubmit(method)
      }}
    >
      <h3>Mis Datos</h3>

      <label className="field">
        Nombre
        <input
          type="text"
          value={buyer.nombre}
          onChange={(e) => onBuyerChange({ ...buyer, nombre: e.target.value })}
          disabled={disabled}
          required
        />
      </label>
      <label className="field">
        Apellidos
        <input
          type="text"
          value={buyer.apellidos}
          onChange={(e) => onBuyerChange({ ...buyer, apellidos: e.target.value })}
          disabled={disabled}
          required
        />
      </label>
      <label className="field">
        Correo
        <input
          type="email"
          value={buyer.email}
          onChange={(e) => onBuyerChange({ ...buyer, email: e.target.value })}
          disabled={disabled}
          required
        />
      </label>

      <p className="field-label">Método de pago</p>
      <div className="payment-toggle" role="radiogroup" aria-label="Método de pago">
        <span
          className={`payment-toggle__slider${method === 'Bizum' ? ' payment-toggle__slider--right' : ''}`}
        />
        <button
          type="button"
          role="radio"
          aria-checked={method === 'Card'}
          className={`payment-toggle__option${method === 'Card' ? ' payment-toggle__option--selected' : ''}`}
          disabled={disabled}
          onClick={() => setMethod('Card')}
        >
          <CardIcon />
          Tarjeta
        </button>
        <button
          type="button"
          role="radio"
          aria-checked={method === 'Bizum'}
          className={`payment-toggle__option${method === 'Bizum' ? ' payment-toggle__option--selected' : ''}`}
          disabled={disabled}
          onClick={() => setMethod('Bizum')}
        >
          <BizumIcon />
          Bizum
        </button>
      </div>

      <button type="submit" disabled={disabled || !dataComplete}>
        {disabled ? 'Procesando pago…' : `Pagar todo — ${total.toFixed(2)} €`}
      </button>
    </form>
  )
}
