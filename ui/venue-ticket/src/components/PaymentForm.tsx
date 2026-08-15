import { useState } from 'react'
import type { BuyerData, PaymentMethod } from '@venue/core/domain'
import { CardIcon, BizumIcon } from '../shared/icons'

interface Props {
  total: number
  disabled: boolean
  buyer: BuyerData
  onBuyerChange: (buyer: BuyerData) => void
  onSubmit: (method: PaymentMethod) => void
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

      <div className="payment-form__row">
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
      </div>
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
