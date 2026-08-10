interface Props {
  sold: number
  capacity: number
  occupancyPct: number
}

// Resumen compacto de la cabecera del modo Ventas: cuántas entradas se han vendido sobre el aforo
// total y la ocupación porcentual con barra. Derivado de zones.confirmedSold (única señal que sube
// con compra real o simulada, ver ADR 0022 Decisión 5) — el desglose de ingresos vive abajo.
export default function SalesSummary({ sold, capacity, occupancyPct }: Props) {
  return (
    <section className="sales-summary">
      <h2>Ventas y ocupación</h2>
      <div className="sales-summary__stats">
        <div className="prediction-stat">
          <span className="prediction-stat__value">{sold}</span>
          <span className="prediction-stat__label">entradas vendidas de {capacity}</span>
        </div>
        <div className="prediction-stat">
          <span className="prediction-stat__value">{occupancyPct}%</span>
          <span className="prediction-stat__label">aforo ocupado</span>
        </div>
      </div>
      <div className="gauge-bar">
        <div className="gauge-fill" style={{ width: `${occupancyPct}%` }} />
      </div>
    </section>
  )
}
