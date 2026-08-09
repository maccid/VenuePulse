import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { PricingChanged, TicketStatus, TicketStatusUpdate, TicketType, ZoneOccupancy } from './types'
import { SagaTimeline } from './SagaTimeline'
import PurchaseWizard from './components/PurchaseWizard'
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
  const [holderName, setHolderName] = useState(holder)
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

    fetch(`/api/access-control/api/zones?eventId=${eventId}`)
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

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/occupancy')
      .withAutomaticReconnect()
      .build()

    // El hub emite para todas las zonas de todos los eventos; solo aplica si ya está en nuestra
    // lista (cargada con ?eventId=), así la actualización queda scoped al evento sin filtrar aquí.
    connection.on('occupancyChanged', (updated: ZoneOccupancy) => {
      setZones((prev) => prev.map((z) => (z.zoneId === updated.zoneId ? updated : z)))
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de aforo', err))

    return () => {
      connection.stop()
    }
  }, [])

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/pricing')
      .withAutomaticReconnect()
      .build()

    // El hub emite para todos los eventos; solo aplica si el mensaje es del evento que estamos
    // mostrando (mismo criterio que occupancyChanged más arriba).
    connection.on('pricingChanged', (updated: PricingChanged) => {
      if (updated.eventId === eventId) setTicketTypes(updated.ticketTypes)
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de precios', err))

    return () => {
      connection.stop()
    }
  }, [eventId])

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
        holder: holderName,
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
      <h3 className="ticket-checkout__title">Comprar entrada</h3>

      <PurchaseWizard
        ticketTypes={ticketTypes}
        zones={zones}
        ticketTypeId={ticketTypeId}
        zoneId={zoneId}
        holder={holderName}
        simulateFailure={simulateFailure}
        disabled={isPurchasing}
        onTicketTypeChange={setTicketTypeId}
        onZoneChange={setZoneId}
        onHolderChange={setHolderName}
        onSimulateFailureChange={setSimulateFailure}
        onSubmit={handlePurchase}
      />

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
