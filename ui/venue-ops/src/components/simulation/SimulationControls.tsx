import type { SimulationMode } from '@venue/core/domain'
import { MODE_LABELS } from './simulationLabels'

export interface SimulationConfig {
  mode: SimulationMode
  durationSeconds: number
  targetSoldPct: number
  noShowPct: number
}

interface Props {
  config: SimulationConfig
  onChange: (patch: Partial<SimulationConfig>) => void
  running: boolean
}

export default function SimulationControls({ config, onChange, running }: Props) {
  const { mode, durationSeconds, targetSoldPct, noShowPct } = config

  return (
    <div className="mb-4 grid grid-cols-[repeat(auto-fit,minmax(200px,1fr))] gap-4">
      <label className="flex flex-col gap-1.5 text-[13px] text-text-muted">
        Fase
        <select value={mode} onChange={(e) => onChange({ mode: e.target.value as SimulationMode })}>
          {(Object.keys(MODE_LABELS) as SimulationMode[]).map((m) => (
            <option key={m} value={m}>{MODE_LABELS[m]}</option>
          ))}
        </select>
      </label>

      <label className="flex flex-col gap-1.5 text-[13px] text-text-muted">
        Duración ({durationSeconds}s)
        <input
          type="range"
          min={5}
          max={60}
          step={5}
          value={durationSeconds}
          onChange={(e) => onChange({ durationSeconds: Number(e.target.value) })}
          disabled={running}
        />
      </label>

      <label className="flex flex-col gap-1.5 text-[13px] text-text-muted">
        Objetivo de venta ({Math.round(targetSoldPct * 100)}%)
        <input
          type="range"
          min={0.4}
          max={0.95}
          step={0.05}
          value={targetSoldPct}
          onChange={(e) => onChange({ targetSoldPct: Number(e.target.value) })}
          disabled={running || mode !== 'Sales'}
        />
      </label>

      <label className="flex flex-col gap-1.5 text-[13px] text-text-muted">
        No-shows ({Math.round(noShowPct * 100)}%)
        <input
          type="range"
          min={0.03}
          max={0.15}
          step={0.01}
          value={noShowPct}
          onChange={(e) => onChange({ noShowPct: Number(e.target.value) })}
          disabled={running || mode !== 'Arrivals'}
        />
      </label>
    </div>
  )
}
