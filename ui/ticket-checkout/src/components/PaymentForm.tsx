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
// y elegir método (ver feedback de usuario). Apellidos/correo son solo visuales (no los persiste el
// backend, que solo modela un "holder" de texto libre); el Nombre se capturó ya antes de reservar
// y aquí se confirma pre-relleno.
export default function PaymentForm({ total, disabled, buyer, onBuyerChange, onSubmit }: Props) {
  const [method, setMethod] = useState<PaymentMethod | null>(null)
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
      <div className="payment-methods">
        <button
          type="button"
          className={`payment-method${method === 'Card' ? ' payment-method--selected' : ''}`}
          disabled={disabled}
          onClick={() => setMethod('Card')}
        >
          💳 Tarjeta
        </button>
        <button
          type="button"
          className={`payment-method${method === 'Bizum' ? ' payment-method--selected' : ''}`}
          disabled={disabled}
          onClick={() => setMethod('Bizum')}
        >
          📱 Bizum
        </button>
      </div>

      <button type="submit" disabled={disabled || !method || !dataComplete}>
        {disabled ? 'Procesando pago…' : `Pagar todo — ${total.toFixed(2)} €`}
      </button>
    </form>
  )
}
