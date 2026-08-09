import { useEffect, useRef, useState } from 'react'
import type { SimulationMode, SimulationRunStatus, SimulationStartRequest } from '../types'

interface Props {
  eventId: string
  eventStartsAtUtc: string
  onSimulationEnded: () => void
}

const MODE_LABELS: Record<SimulationMode, string> = {
  Sales: 'Ventas (antes del evento)',
  Arrivals: 'Llegadas (horas previas)',
}

export default function SimulationPanel({ eventId, eventStartsAtUtc, onSimulationEnded }: Props) {
  const [mode, setMode] = useState<SimulationMode>('Sales')
  const [durationSeconds, setDurationSeconds] = useState(20)
  const [targetSoldPct, setTargetSoldPct] = useState(0.85)
  const [noShowPct, setNoShowPct] = useState(0.08)
  const [status, setStatus] = useState<SimulationRunStatus | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const statusRef = useRef(status)
  const onSimulationEndedRef = useRef(onSimulationEnded)
  useEffect(() => {
    statusRef.current = status
  }, [status])
  useEffect(() => {
    onSimulationEndedRef.current = onSimulationEnded
  }, [onSimulationEnded])

  useEffect(() => {
    if (eventId === '') {
      setStatus(null)
      return
    }

    let cancelled = false
    const interval = window.setInterval(async () => {
      const response = await fetch(`/api/access-control/api/simulation/events/${eventId}/status`)
      if (cancelled || !response.ok) return

      const current = (await response.json().catch(() => null)) as SimulationRunStatus | null
      const wasRunning = statusRef.current?.running === true
      setStatus(current)

      if (wasRunning && !current?.running) {
        onSimulationEndedRef.current()
      }
    }, 500)

    return () => {
      cancelled = true
      window.clearInterval(interval)
    }
  }, [eventId])

  async function control(kind: 'start' | 'stop' | 'reset') {
    setError(null)
    setBusy(true)
    try {
      if (kind === 'start') {
        const request: SimulationStartRequest = {
          mode,
          eventStartsAtUtc,
          durationSeconds,
          targetSoldPct,
          noShowPct,
        }
        const response = await fetch(`/api/access-control/api/simulation/events/${eventId}/start`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request),
        })
        if (!response.ok) {
          setError((await response.json().catch(() => null))?.error ?? 'No se pudo iniciar la simulación')
        }
      } else {
        const response = await fetch(`/api/access-control/api/simulation/events/${eventId}/${kind}`, {
          method: 'POST',
        })
        if (!response.ok && kind === 'stop') {
          setError('No hay ninguna simulación en curso')
        }
        if (kind === 'reset') onSimulationEnded()
      }
    } finally {
      setBusy(false)
    }
  }

  const running = status?.running === true
  const progressPct = Math.round((status?.progress ?? 0) * 100)

  return (
    <section className="card simulation-panel">
      <h2>Simulación de demo</h2>

      <div className="simulation-controls">
        <label className="simulation-control">
          Fase
          <select value={mode} onChange={(e) => setMode(e.target.value as SimulationMode)}>
            {(Object.keys(MODE_LABELS) as SimulationMode[]).map((m) => (
              <option key={m} value={m}>{MODE_LABELS[m]}</option>
            ))}
          </select>
        </label>

        <label className="simulation-control">
          Duración ({durationSeconds}s)
          <input
            type="range"
            min={5}
            max={60}
            step={5}
            value={durationSeconds}
            onChange={(e) => setDurationSeconds(Number(e.target.value))}
            disabled={running}
          />
        </label>

        <label className="simulation-control">
          Objetivo de venta ({Math.round(targetSoldPct * 100)}%)
          <input
            type="range"
            min={0.4}
            max={0.95}
            step={0.05}
            value={targetSoldPct}
            onChange={(e) => setTargetSoldPct(Number(e.target.value))}
            disabled={running || mode !== 'Sales'}
          />
        </label>

        <label className="simulation-control">
          No-shows ({Math.round(noShowPct * 100)}%)
          <input
            type="range"
            min={0.03}
            max={0.15}
            step={0.01}
            value={noShowPct}
            onChange={(e) => setNoShowPct(Number(e.target.value))}
            disabled={running || mode !== 'Arrivals'}
          />
        </label>
      </div>

      <div className="simulation-actions">
        <button onClick={() => control('start')} disabled={running || busy || eventId === ''}>
          {running ? 'En curso…' : 'Iniciar'}
        </button>
        <button onClick={() => control('stop')} disabled={!running || busy}>
          Detener
        </button>
        <button onClick={() => control('reset')} disabled={busy || (running && status?.progress !== 0)}>
          Reiniciar aforo
        </button>
      </div>

      {running && (
        <div className="simulation-progress">
          <div className="gauge-bar">
            <div className="gauge-fill" style={{ width: `${progressPct}%` }} />
          </div>
          <p className="zone-meta">
            {MODE_LABELS[status?.mode ?? mode]} — {progressPct}%
          </p>
        </div>
      )}

      {error && <p className="scan-result scan-result--error">{error}</p>}
    </section>
  )
}
