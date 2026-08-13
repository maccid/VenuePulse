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
    <section className="card mb-6">
      <h2>Escanear entrada</h2>
      <form className="flex gap-3" onSubmit={handleSubmit}>
        <input
          className="flex-1"
          placeholder="ID de la entrada"
          value={ticketId}
          onChange={(e) => setTicketId(e.target.value)}
          required
        />
        <button type="submit">Escanear</button>
      </form>
      {scanResult.kind === 'ok' && (
        <p className="mt-4 flex items-center gap-2 text-sm text-success before:content-['✔'] before:text-success">
          Check-in registrado, aforo actualizado.
        </p>
      )}
      {scanResult.kind === 'error' && (
        <p className="mt-4 flex items-center gap-2 text-sm text-danger before:content-['✖'] before:text-danger">
          {scanResult.message}
        </p>
      )}
    </section>
  )
}
