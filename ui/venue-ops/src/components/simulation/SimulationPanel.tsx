import { useState } from 'react'
import type { SimulationMode, SimulationStartRequest } from '../../shared/types'
import { useSimulationStatus } from '../../hooks/useSimulationStatus'
import SimulationControls, { type SimulationConfig } from './SimulationControls'
import { MODE_LABELS } from './simulationLabels'

interface Props {
  eventId: string
  eventStartsAtUtc: string
  onSimulationEnded: () => void
  defaultMode?: SimulationMode
}

export default function SimulationPanel({ eventId, eventStartsAtUtc, onSimulationEnded, defaultMode = 'Sales' }: Props) {
  const [config, setConfig] = useState<SimulationConfig>({
    mode: defaultMode,
    durationSeconds: 20,
    targetSoldPct: 0.85,
    noShowPct: 0.08,
  })
  const { status, running, progressPct } = useSimulationStatus(eventId, onSimulationEnded)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  function updateConfig(patch: Partial<SimulationConfig>) {
    setConfig((prev) => ({ ...prev, ...patch }))
  }

  async function control(kind: 'start' | 'stop' | 'reset') {
    setError(null)
    setBusy(true)
    try {
      if (kind === 'start') {
        const request: SimulationStartRequest = {
          mode: config.mode,
          eventStartsAtUtc,
          durationSeconds: config.durationSeconds,
          targetSoldPct: config.targetSoldPct,
          noShowPct: config.noShowPct,
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

  return (
    <section className="card simulation-panel">
      <h2>Simulación de demo</h2>

      <SimulationControls config={config} onChange={updateConfig} running={running} />

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
            {MODE_LABELS[status?.mode ?? config.mode]} — {progressPct}%
          </p>
        </div>
      )}

      {error && <p className="scan-result scan-result--error">{error}</p>}
    </section>
  )
}
