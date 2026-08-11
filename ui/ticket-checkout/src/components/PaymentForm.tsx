import { useState } from 'react'
import type { PaymentMethod } from '../types'

interface Props {
  total: number
  disabled: boolean
  onSubmit: (method: PaymentMethod) => void
}

// Apellidos/correo son solo visuales (no los persiste el backend, que solo modela un "holder" de
// texto libre) — el nombre real que viaja con cada entrada llega como atributo del Web Component
// desde el shell. Pedirlos aquí es una confirmación de datos, no una segunda fuente de verdad.
export default function PaymentForm({ total, disabled, onSubmit }: Props) {
  const [surname, setSurname] = useState('')
  const [email, setEmail] = useState('')
  const [method, setMethod] = useState<PaymentMethod | null>(null)

  return (
    <form
      className="payment-form"
      onSubmit={(e) => {
        e.preventDefault()
        if (method) onSubmit(method)
      }}
    >
      <h3>Tus datos</h3>

      <label className="field">
        Apellidos
        <input type="text" value={surname} onChange={(e) => setSurname(e.target.value)} disabled={disabled} />
      </label>
      <label className="field">
        Correo
        <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} disabled={disabled} />
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

      <button type="submit" disabled={disabled || !method}>
        {disabled ? 'Procesando pago…' : `Pagar todo — ${total.toFixed(2)} €`}
      </button>
    </form>
  )
}
