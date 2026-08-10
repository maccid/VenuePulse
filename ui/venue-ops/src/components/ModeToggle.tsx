import type { VenueOpsMode } from '../types'

interface Props {
  mode: VenueOpsMode
  onChange: (mode: VenueOpsMode) => void
}

const OPTIONS: { value: VenueOpsMode; label: string }[] = [
  { value: 'sales', label: 'Ventas' },
  { value: 'checkin', label: 'Ocupación' },
]

export default function ModeToggle({ mode, onChange }: Props) {
  return (
    <div className="mode-toggle" role="tablist" aria-label="Modo de Operaciones">
      {OPTIONS.map((opt) => (
        <button
          key={opt.value}
          role="tab"
          aria-selected={mode === opt.value}
          className={`mode-toggle__option${mode === opt.value ? ' mode-toggle__option--active' : ''}`}
          onClick={() => onChange(opt.value)}
        >
          {opt.label}
        </button>
      ))}
    </div>
  )
}
