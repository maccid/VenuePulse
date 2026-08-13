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
    <section className="card">
      <h2>Ventas e ingresos</h2>

      {error && (
        <p className="mt-4 flex items-center gap-2 text-sm text-danger before:content-['✖'] before:text-danger">{error}</p>
      )}

      {!error && revenue === null && <p className="m-0 text-sm text-text-muted">Cargando ingresos…</p>}

      {!error && revenue !== null && (
        <div className="grid grid-cols-3 gap-3">
          <div className="flex flex-col gap-0.5 rounded-md border border-border bg-bg p-3">
            <span className="text-[24px] font-bold tracking-[-0.01em]">{sold}</span>
            <span className="text-xs text-text-muted">entradas vendidas</span>
          </div>
          <div className="flex flex-col gap-0.5 rounded-md border border-border bg-bg p-3">
            <span className="text-[24px] font-bold tracking-[-0.01em]">{CURRENCY.format(revenue.confirmedRevenue)}</span>
            <span className="text-xs text-text-muted">confirmados · {revenue.confirmedCount} entradas</span>
          </div>
          <div className="flex flex-col gap-0.5 rounded-md border border-border bg-bg p-3">
            <span className="text-[24px] font-bold tracking-[-0.01em]">{CURRENCY.format(revenue.pendingRevenue)}</span>
            <span className="text-xs text-text-muted">pendientes · {revenue.pendingCount} entradas</span>
          </div>
        </div>
      )}

      <div className="mt-4 flex items-baseline gap-2 border-t border-dashed border-border pt-4">
        <span className="font-[650] text-text">{CURRENCY.format(estimatedRevenue)}</span>
        <span className="text-xs text-text-muted">
          estimado a precio de catálogo (incluye venta simulada)
        </span>
      </div>
    </section>
  )
}
