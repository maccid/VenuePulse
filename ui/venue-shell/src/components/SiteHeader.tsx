export default function SiteHeader() {
  return (
    <header className="site-header">
      <div className="site-header__brand">
        <span className="site-header__mark" aria-hidden="true">
          VP
        </span>
        <div>
          <h1>VenuePulse</h1>
          <p className="site-header__tagline">Ticketing y aforo en tiempo real del recinto</p>
        </div>
      </div>
    </header>
  )
}
