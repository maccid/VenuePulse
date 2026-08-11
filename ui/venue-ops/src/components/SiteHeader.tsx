export default function SiteHeader() {
  return (
    <header className="site-header">
      <div className="site-header__brand">
        <span className="site-header__mark" aria-hidden="true">
          VO
        </span>
        <div>
          <h1>VenuePulse · Operaciones</h1>
          <p className="site-header__tagline">Escaneo de torniquete y aforo en tiempo real por puerta</p>
        </div>
      </div>
    </header>
  )
}
