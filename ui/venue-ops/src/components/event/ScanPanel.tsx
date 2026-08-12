import { useState } from 'react'
import type { ScanResult } from '../../shared/types'

interface Props {
  onScan: (ticketId: string) => Promise<ScanResult>
}

export default function ScanPanel({ onScan }: Props) {
  const [ticketId, setTicketId] = useState('')
  const [scanResult, setScanResult] = useState<ScanResult>({ kind: 'idle' })

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    const result = await onScan(ticketId)
    setScanResult(result)
    if (result.kind === 'ok') setTicketId('')
  }

  return (
    <section className="card scan-panel">
      <h2>Escanear entrada</h2>
      <form onSubmit={handleSubmit}>
        <input
          placeholder="ID de la entrada"
          value={ticketId}
          onChange={(e) => setTicketId(e.target.value)}
          required
        />
        <button type="submit">Escanear</button>
      </form>
      {scanResult.kind === 'ok' && (
        <p className="scan-result scan-result--ok">Check-in registrado, aforo actualizado.</p>
      )}
      {scanResult.kind === 'error' && (
        <p className="scan-result scan-result--error">{scanResult.message}</p>
      )}
    </section>
  )
}
