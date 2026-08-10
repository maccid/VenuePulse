import { useState } from 'react'
import type { CreateEventRequest, EventRelevance, EventSummary, EventType } from '../types'

interface Props {
  onCreated: (event: EventSummary) => void
  onCancel: () => void
}

const TYPE_LABELS: Record<EventType, string> = {
  Match: 'Partido',
  Concert: 'Concierto',
}

const RELEVANCE_LABELS: Record<EventRelevance, string> = {
  Standard: 'Estándar',
  Special: 'Especial (×1,5 sobre el precio base)',
}

export default function EventCreateForm({ onCreated, onCancel }: Props) {
  const [name, setName] = useState('')
  const [type, setType] = useState<EventType>('Match')
  const [relevance, setRelevance] = useState<EventRelevance>('Standard')
  const [startsAt, setStartsAt] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim() || !startsAt) {
      setError('Rellena el nombre y la fecha del evento.')
      return
    }

    setError(null)
    setBusy(true)
    try {
      const request: CreateEventRequest = {
        name: name.trim(),
        startsAtUtc: new Date(startsAt).toISOString(),
        type,
        relevance,
      }
      const response = await fetch('/api/ticketing/api/events', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      })
      if (!response.ok) {
        setError('No se pudo crear el evento.')
        return
      }
      onCreated((await response.json()) as EventSummary)
    } catch {
      setError('No se pudo crear el evento.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card event-create-form">
      <h2>Nuevo evento</h2>
      <form onSubmit={handleSubmit}>
        <label className="event-create-form__field">
          Nombre
          <input value={name} onChange={(e) => setName(e.target.value)} placeholder="p. ej. Final de Copa" />
        </label>

        <label className="event-create-form__field">
          Tipo de evento
          <select value={type} onChange={(e) => setType(e.target.value as EventType)}>
            {(Object.keys(TYPE_LABELS) as EventType[]).map((t) => (
              <option key={t} value={t}>{TYPE_LABELS[t]}</option>
            ))}
          </select>
        </label>

        <label className="event-create-form__field">
          Fecha y hora
          <input type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} />
        </label>

        <label className="event-create-form__field">
          Relevancia
          <select value={relevance} onChange={(e) => setRelevance(e.target.value as EventRelevance)}>
            {(Object.keys(RELEVANCE_LABELS) as EventRelevance[]).map((r) => (
              <option key={r} value={r}>{RELEVANCE_LABELS[r]}</option>
            ))}
          </select>
        </label>

        <div className="event-create-form__actions">
          <button type="submit" disabled={busy}>{busy ? 'Creando…' : 'Crear evento'}</button>
          <button type="button" className="event-create-form__cancel" onClick={onCancel} disabled={busy}>
            Cancelar
          </button>
        </div>

        {error && <p className="scan-result scan-result--error">{error}</p>}
      </form>
    </section>
  )
}
