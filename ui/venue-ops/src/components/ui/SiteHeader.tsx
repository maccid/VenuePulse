interface Props {
  badge?: string
  title?: string
  subtitle?: string
}

export default function SiteHeader({
  badge = 'VO',
  title = 'VenuePulse · Operaciones',
  subtitle = 'Aforo, ventas y simulación en tiempo real por evento',
}: Props) {
  return (
    <header className="mb-8">
      <div className="flex items-center gap-4">
        <span
          className="grid size-12 place-items-center rounded-lg bg-linear-to-r from-accent to-accent-hover text-lg font-bold text-text-on-accent shadow-sm"
          aria-hidden="true"
        >
          {badge}
        </span>
        <div>
          <h1>{title}</h1>
          <p className="m-0 text-sm text-text-muted">{subtitle}</p>
        </div>
      </div>
    </header>
  )
}
