import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { ScanResult, ZoneOccupancy } from './types'
import SiteHeader from './components/SiteHeader'
import ScanPanel from './components/ScanPanel'
import ZonesDashboard from './components/ZonesDashboard'
import './App.css'

export default function App() {
  const [zones, setZones] = useState<ZoneOccupancy[]>([])

  useEffect(() => {
    loadZones()

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/occupancy')
      .withAutomaticReconnect()
      .build()

    connection.on('occupancyChanged', (updated: ZoneOccupancy) => {
      setZones((prev) => prev.map((z) => (z.zoneId === updated.zoneId ? updated : z)))
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de aforo', err))

    return () => {
      connection.stop()
    }
  }, [])

  function loadZones() {
    fetch('/api/access-control/api/zones')
      .then((r) => r.json())
      .then(setZones)
      .catch(() => setZones([]))
  }

  async function scanTicket(ticketId: string): Promise<ScanResult> {
    const response = await fetch(`/api/checkin/api/tickets/${ticketId}/check-in`, { method: 'POST' })

    if (response.ok) return { kind: 'ok' }

    const body = await response.json().catch(() => ({ error: 'Entrada no válida' }))
    return { kind: 'error', message: body.error ?? 'No se pudo hacer el check-in' }
  }

  return (
    <div className="venue-ops">
      <SiteHeader />

      <ScanPanel onScan={scanTicket} />

      <ZonesDashboard zones={zones} />
    </div>
  )
}
