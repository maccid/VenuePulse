import { useState } from 'react'
import type { SimulationMode, SimulationStartRequest } from '@venue/core/domain'
import { InlineError } from '@venue/core/components'
import { Play, RotateCw, Square } from 'lucide-react'
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
    <section className="card mb-6">
      <h2 className="mb-4">Simulación de demo</h2>

      <SimulationControls config={config} onChange={updateConfig} running={running} />

      <div className="mb-4 flex gap-3">
        <button
          type="button"
          onClick={() => control('start')}
          disabled={running || busy || eventId === ''}
          className="flex size-11 items-center justify-center !p-0 disabled:cursor-not-allowed disabled:opacity-[0.55]"
          title="Iniciar simulación"
          aria-label="Iniciar simulación"
        >
          <Play className="size-5 fill-current" aria-hidden="true" />
        </button>
        <button
          type="button"
          onClick={() => control('stop')}
          disabled={!running || busy}
          className="flex size-11 items-center justify-center !p-0 bg-danger hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-[0.55]"
          title="Detener simulación"
          aria-label="Detener simulación"
        >
          <Square className="size-5 fill-current" aria-hidden="true" />
        </button>
        <button
          type="button"
          onClick={() => control('reset')}
          disabled={busy || (running && status?.progress !== 0)}
          className="flex size-11 items-center justify-center !p-0 bg-border-strong text-text hover:bg-border disabled:cursor-not-allowed disabled:opacity-[0.55]"
          title="Reiniciar aforo"
          aria-label="Reiniciar aforo"
        >
          <RotateCw className="size-5" aria-hidden="true" />
        </button>
      </div>

      {running && (
        <div className="mt-2">
          <div className="h-2.5 overflow-hidden rounded-full bg-accent-bg">
            <div
              className="h-full bg-linear-to-r from-accent to-accent-hover transition-[width] duration-[400ms]"
              style={{ width: `${progressPct}%` }}
            />
          </div>
          <p className="mt-1 text-sm text-text-muted">
            {MODE_LABELS[status?.mode ?? config.mode]} — {progressPct}%
          </p>
        </div>
      )}

      <InlineError message={error} />
    </section>
  )
}
