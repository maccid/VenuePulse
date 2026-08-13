export default function SiteHeader() {
  return (
    <header className="mb-8">
      <div className="flex items-center gap-4">
        <span
          className="grid size-12 place-items-center rounded-lg bg-linear-to-r from-accent to-accent-hover text-lg font-bold text-text-on-accent shadow-sm"
          aria-hidden="true"
        >
          VO
        </span>
        <div>
          <h1>VenuePulse · Operaciones</h1>
          <p className="m-0 text-sm text-text-muted">Escaneo de torniquete y aforo en tiempo real por puerta</p>
        </div>
      </div>
    </header>
  )
}
