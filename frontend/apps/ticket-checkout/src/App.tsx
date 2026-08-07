import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { TicketStatus, TicketStatusUpdate, TicketType, ZoneOccupancy } from './types'
import { SagaTimeline } from './SagaTimeline'
import './App.css'

interface Props {
  eventId: string
  holder: string
}

export default function App({ eventId, holder }: Props) {
  const [ticketTypes, setTicketTypes] = useState<TicketType[]>([])
  const [zones, setZones] = useState<ZoneOccupancy[]>([])
  const [ticketTypeId, setTicketTypeId] = useState('')
  const [zoneId, setZoneId] = useState('')
  const [simulateFailure, setSimulateFailure] = useState(false)

  const [ticketId, setTicketId] = useState<string | null>(null)
  const [status, setStatus] = useState<TicketStatus | 'Idle' | 'Purchasing'>('Idle')
  const [rejectionReason, setRejectionReason] = useState<string | null>(null)

  useEffect(() => {
    fetch(`/api/ticketing/api/events/${eventId}/ticket-types`)
      .then((r) => r.json())
      .then((types: TicketType[]) => {
        setTicketTypes(types)
        if (types[0]) setTicketTypeId(types[0].id)
      })
      .catch(() => setTicketTypes([]))

    fetch('/api/access-control/api/zones')
      .then((r) => r.json())
      .then((z: ZoneOccupancy[]) => {
        setZones(z)
        if (z[0]) setZoneId(z[0].zoneId)
      })
      .catch(() => setZones([]))
  }, [eventId])

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/tickets')
      .withAutomaticReconnect()
      .build()

    connection.on('ticketStatusChanged', (update: TicketStatusUpdate) => {
      setTicketId((current) => {
        if (current !== null && update.ticketId === current) {
          setStatus(update.status)
          setRejectionReason(update.rejectionReason)
        }
        return current
      })
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de entradas', err))

    return () => {
      connection.stop()
    }
  }, [])

  async function handlePurchase() {
    setStatus('Purchasing')
    setRejectionReason(null)

    const response = await fetch('/api/checkin/api/tickets', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        eventId,
        ticketTypeId,
        zoneId,
        holder,
        simulateFailure,
      }),
    })

    if (!response.ok) {
      setStatus('Idle')
      return
    }

    const { ticketId: newTicketId } = await response.json()
    setTicketId(newTicketId)
    setStatus('Pending')
  }

  const isPurchasing = status !== 'Idle'

  return (
    <div className="ticket-checkout">
      <h3>Comprar entrada</h3>

      <label>
        Tipo de entrada
        <select value={ticketTypeId} onChange={(e) => setTicketTypeId(e.target.value)} disabled={isPurchasing}>
          {ticketTypes.map((t) => (
            <option key={t.id} value={t.id}>{t.name} — {t.price.toFixed(2)} €</option>
          ))}
        </select>
      </label>

      <label>
        Zona / puerta de acceso
        <select value={zoneId} onChange={(e) => setZoneId(e.target.value)} disabled={isPurchasing}>
          {zones.map((z) => (
            <option key={z.zoneId} value={z.zoneId}>{z.name} ({z.capacity - z.confirmedSold} libres)</option>
          ))}
        </select>
      </label>

      <label className="checkbox-field">
        <input
          type="checkbox"
          checked={simulateFailure}
          onChange={(e) => setSimulateFailure(e.target.checked)}
          disabled={isPurchasing}
        />
        Simular fallo de pago (dispara la compensación real de la Saga)
      </label>

      <button onClick={handlePurchase} disabled={isPurchasing || !ticketTypeId || !zoneId}>
        {isPurchasing ? 'Procesando…' : 'Comprar entrada'}
      </button>

      {status !== 'Idle' && (
        <div className="saga-panel">
          <SagaTimeline status={status} rejectionReason={rejectionReason} />
          {ticketId && (status === 'Confirmed' || status === 'Rejected') && (
            <p className="ticket-id-hint">
              ID de la entrada (para escanear en <em>venue-ops</em>): <code>{ticketId}</code>
            </p>
          )}
        </div>
      )}
    </div>
  )
}
