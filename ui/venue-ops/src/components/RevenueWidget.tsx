import type { EventRevenueDto } from '../types'

interface Props {
  revenue: EventRevenueDto | null
  error: string | null
  // Ver ADR 0022, decisión de ingresos estimados: precio de catálogo × zone.confirmedSold — la
  // única señal que sube tanto con compras reales como con el motor de simulación (Bloque 8), que
  // nunca toca CheckIn y por tanto nunca movería `revenue` por sí solo.
  estimatedRevenue: number
}

const CURRENCY = new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' })

export default function RevenueWidget({ revenue, error, estimatedRevenue }: Props) {
  return (
    <section className="card revenue-widget">
      <h2>Ingresos</h2>

      {error && <p className="scan-result scan-result--error">{error}</p>}

      {!error && revenue === null && <p className="prediction-empty">Cargando ingresos…</p>}

      {!error && revenue !== null && (
        <div className="revenue-widget__stats">
          <div className="prediction-stat">
            <span className="prediction-stat__value">{CURRENCY.format(revenue.confirmedRevenue)}</span>
            <span className="prediction-stat__label">confirmados · {revenue.confirmedCount} entradas</span>
          </div>
          <div className="prediction-stat">
            <span className="prediction-stat__value">{CURRENCY.format(revenue.pendingRevenue)}</span>
            <span className="prediction-stat__label">pendientes · {revenue.pendingCount} entradas</span>
          </div>
        </div>
      )}

      <div className="revenue-widget__estimate">
        <span className="revenue-widget__estimate-value">{CURRENCY.format(estimatedRevenue)}</span>
        <span className="revenue-widget__estimate-label">
          estimado a precio de catálogo (incluye venta simulada)
        </span>
      </div>
    </section>
  )
}
