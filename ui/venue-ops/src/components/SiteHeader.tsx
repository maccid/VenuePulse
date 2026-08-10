interface Props {
  onCreateEvent: () => void
}

export default function SiteHeader({ onCreateEvent }: Props) {
  return (
    <header className="site-header">
      <div className="site-header__brand">
        <button type="button" className="site-header__create" onClick={onCreateEvent}>
          Crear nuevo evento
        </button>
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
