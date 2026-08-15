import { useEffect, useState } from 'react'
import type { OccupancyPrediction } from '@venue/core/domain'
import { InlineError } from '@venue/core/components'
import PredictionChart from './PredictionChart'

interface Props {
  eventId: string
  eventStartsAtUtc: string
}

export default function PredictionPanel({ eventId, eventStartsAtUtc }: Props) {
  const [prediction, setPrediction] = useState<OccupancyPrediction | null>(null)
  const [noData, setNoData] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!eventId || !eventStartsAtUtc) {
      setPrediction(null)
      setNoData(true)
      setError(null)
      return
    }

    let cancelled = false

    async function load() {
      const url = `/api/access-control/api/prediction/events/${eventId}?eventStartsAtUtc=${encodeURIComponent(eventStartsAtUtc)}`
      try {
        const response = await fetch(url)
        if (cancelled) return

        if (response.status === 404) {
          setNoData(true)
          setPrediction(null)
          setError(null)
          return
        }
        if (!response.ok) {
          setError('No se pudo cargar la predicción')
          return
        }

        setPrediction((await response.json()) as OccupancyPrediction)
        setNoData(false)
        setError(null)
      } catch {
        if (!cancelled) setError('No se pudo cargar la predicción')
      }
    }

    void load()
    const interval = window.setInterval(load, 2500)

    return () => {
      cancelled = true
      window.clearInterval(interval)
    }
  }, [eventId, eventStartsAtUtc])

  if (noData) {
    return (
      <section className="card mb-6">
        <h2 className="mb-4">Predicción de ocupación</h2>
        <p className="m-0 text-sm text-text-muted">
          Ejecuta la simulación (fase de llegadas) para ver la predicción de ocupación.
        </p>
      </section>
    )
  }

  if (prediction === null) {
    return (
      <section className="card mb-6">
        <h2 className="mb-4">Predicción de ocupación</h2>
        <InlineError message={error} />
        {!error && <p className="m-0 text-sm text-text-muted">Cargando predicción…</p>}
      </section>
    )
  }

  const noShowPct = Math.round(prediction.noShowRate * 100)
  const fitNote = prediction.fitFromData
    ? prediction.fitR2 != null
      ? `Ajuste sobre los datos del simulador · R² ${prediction.fitR2.toFixed(3)}`
      : 'Ajuste sobre los datos del simulador'
    : 'Sin historia de llegadas aún — no-shows asumidos (8%)'

  return (
    <section className="card mb-6">
      <h2 className="mb-4">Predicción de ocupación</h2>

      <PredictionChart prediction={prediction} />

      <div className="mt-4 grid grid-cols-3 gap-3">
        <div className="flex flex-col gap-0.5 rounded-md border border-border bg-bg p-3">
          <span className="text-[24px] font-bold tracking-[-0.01em]">{prediction.predictedTotalArrivals}</span>
          <span className="text-xs text-text-muted">llegadas previstas</span>
        </div>
        <div className="flex flex-col gap-0.5 rounded-md border border-border bg-bg p-3">
          <span className="text-[24px] font-bold tracking-[-0.01em]">{prediction.predictedNoShows}</span>
          <span className="text-xs text-text-muted">no-shows previstos</span>
        </div>
        <div className="flex flex-col gap-0.5 rounded-md border border-border bg-bg p-3">
          <span className="text-[24px] font-bold tracking-[-0.01em]">{noShowPct}%</span>
          <span className="text-xs text-text-muted">tasa de no-shows</span>
        </div>
      </div>

      <p className="mt-1 text-sm text-text-muted">{fitNote}</p>
    </section>
  )
}
