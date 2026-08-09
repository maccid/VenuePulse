import type { ZoneOccupancy } from '../types'
import StadiumMap from './StadiumMap'

interface Props {
  zones: ZoneOccupancy[]
  zoneId: string
  disabled: boolean
  onChange: (id: string) => void
}

export default function StepZone({ zones, zoneId, disabled, onChange }: Props) {
  const selected = zones.find((z) => z.zoneId === zoneId)

  return (
    <div className="wizard-step">
      <p className="field-label">Zona / puerta de acceso</p>
      <StadiumMap zones={zones} selectedZoneId={zoneId} onSelect={disabled ? undefined : onChange} />
      {selected && (
        <p className="stadium-map__hint">
          {selected.name}: {selected.availableToSell} libres de {selected.capacity}
        </p>
      )}
    </div>
  )
}
