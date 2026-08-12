import type { SimulationMode } from '../../shared/types'
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
    <div className="simulation-controls">
      <label className="simulation-control">
        Fase
        <select value={mode} onChange={(e) => onChange({ mode: e.target.value as SimulationMode })}>
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
          onChange={(e) => onChange({ durationSeconds: Number(e.target.value) })}
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
          onChange={(e) => onChange({ targetSoldPct: Number(e.target.value) })}
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
          onChange={(e) => onChange({ noShowPct: Number(e.target.value) })}
          disabled={running || mode !== 'Arrivals'}
        />
      </label>
    </div>
  )
}
