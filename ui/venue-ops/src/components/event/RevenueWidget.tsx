import type { EventRevenueDto } from '@venue/core/domain'
import { InlineError } from '@venue/core/components'

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

      <InlineError message={error} />

      {!error && revenue === null && <p className="m-0 text-sm text-text-muted">Cargando ingresos…</p>}

      {!error && revenue !== null && (
        <div className="grid grid-cols-2 gap-3">
          <div className="flex flex-col gap-0.5 rounded-md border border-border bg-bg p-3">
            <span className="text-[24px] font-bold tracking-[-0.01em]">{CURRENCY.format(estimatedRevenue)}</span>
            <span className="text-xs text-text-muted">vendidas · {sold} entradas</span>
          </div>
          <div className="flex flex-col gap-0.5 rounded-md border border-border bg-bg p-3">
            <span className="text-[24px] font-bold tracking-[-0.01em]">{CURRENCY.format(revenue.pendingRevenue)}</span>
            <span className="text-xs text-text-muted">pendientes · {revenue.pendingCount} entradas</span>
          </div>
        </div>
      )}
    </section>
  )
}
