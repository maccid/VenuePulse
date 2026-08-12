import type { EventRevenueDto } from '../../shared/types'

interface Props {
  revenue: EventRevenueDto | null
  error: string | null
  estimatedRevenue: number
  sold: number
}

const CURRENCY = new Intl.NumberFormat('es-ES', { style: 'currency', currency: 'EUR' })

export default function RevenueWidget({ revenue, error, estimatedRevenue, sold }: Props) {
  return (
    <section className="card revenue-widget">
      <h2>Ventas e ingresos</h2>

      {error && <p className="scan-result scan-result--error">{error}</p>}

      {!error && revenue === null && <p className="prediction-empty">Cargando ingresos…</p>}

      {!error && revenue !== null && (
        <div className="revenue-widget__stats">
          <div className="prediction-stat">
            <span className="prediction-stat__value">{sold}</span>
            <span className="prediction-stat__label">entradas vendidas</span>
          </div>
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
