import { useState } from 'react'
import type { EventRelevance, EventSummary, EventType } from '../../shared/types'
import { toDatetimeLocalInputValue } from '../../shared/datetime'
import { useCreateEvent } from '../../hooks/useCreateEvent'

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

const REQUIRED_FIELDS_MESSAGE = 'Rellena el nombre y la fecha del evento.'

export default function EventCreateForm({ onCreated, onCancel }: Props) {
  const [name, setName] = useState('')
  const [type, setType] = useState<EventType>('Match')
  const [relevance, setRelevance] = useState<EventRelevance>('Standard')
  const [startsAt, setStartsAt] = useState(() => toDatetimeLocalInputValue(new Date()))
  const [validationError, setValidationError] = useState<string | null>(null)
  const { create, error, busy } = useCreateEvent(onCreated)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!name.trim() || !startsAt) {
      setValidationError(REQUIRED_FIELDS_MESSAGE)
      return
    }

    setValidationError(null)
    await create({
      name: name.trim(),
      startsAtUtc: new Date(startsAt).toISOString(),
      type,
      relevance,
    })
  }

  const shownError = validationError ?? error

  return (
    <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
      <label className="flex flex-col gap-1.5 text-[13px] text-text-muted">
        Nombre
        <input value={name} onChange={(e) => setName(e.target.value)} placeholder="p. ej. Final de Copa" />
      </label>

      <label className="flex flex-col gap-1.5 text-[13px] text-text-muted">
        Tipo de evento
        <select value={type} onChange={(e) => setType(e.target.value as EventType)}>
          {(Object.keys(TYPE_LABELS) as EventType[]).map((t) => (
            <option key={t} value={t}>{TYPE_LABELS[t]}</option>
          ))}
        </select>
      </label>

      <label className="flex flex-col gap-1.5 text-[13px] text-text-muted">
        Fecha y hora
        <input type="datetime-local" value={startsAt} onChange={(e) => setStartsAt(e.target.value)} />
      </label>

      <label className="flex flex-col gap-1.5 text-[13px] text-text-muted">
        Relevancia
        <select value={relevance} onChange={(e) => setRelevance(e.target.value as EventRelevance)}>
          {(Object.keys(RELEVANCE_LABELS) as EventRelevance[]).map((r) => (
            <option key={r} value={r}>{RELEVANCE_LABELS[r]}</option>
          ))}
        </select>
      </label>

      <div className="flex gap-3">
        <button type="submit" disabled={busy}>{busy ? 'Creando…' : 'Crear evento'}</button>
        <button type="button" className="border border-border bg-transparent text-text-muted" onClick={onCancel} disabled={busy}>
          Cancelar
        </button>
      </div>

      {shownError && <p className="mt-4 flex items-center gap-2 text-sm text-danger before:content-['✖'] before:text-danger">{shownError}</p>}
    </form>
  )
}
