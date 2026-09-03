import { useEffect, useRef, useState } from 'react'
import type { ZoneOccupancy } from '@venue/core/domain'
import ZoneGauge from './ZoneGauge'

interface Props {
  zones: ZoneOccupancy[]
}

const CARD_WIDTH = 288
const GAP = 16

export default function ZonesDashboard({ zones }: Props) {
  const scrollRef = useRef<HTMLDivElement>(null)
  const [canLeft, setCanLeft] = useState(false)
  const [canRight, setCanRight] = useState(false)

  function updateArrows() {
    const el = scrollRef.current
    if (!el) return
    setCanLeft(el.scrollLeft > 1)
    setCanRight(el.scrollLeft + el.clientWidth < el.scrollWidth - 1)
  }

  useEffect(() => {
    updateArrows()
  }, [zones])

  function scroll(direction: -1 | 1) {
    const el = scrollRef.current
    if (!el) return

    const stride = CARD_WIDTH + GAP
    const visible = Math.max(1, Math.floor((el.clientWidth + GAP) / stride))
    const newIndex = Math.max(0, Math.min(zones.length - visible, (el.scrollLeft / stride) + direction))

    if (direction > 0 && newIndex >= zones.length - visible) {
      el.scrollTo({ left: el.scrollWidth, behavior: 'smooth' })
    } else {
      el.scrollTo({ left: newIndex * stride, behavior: 'smooth' })
    }
  }

  return (
    <section className="card min-w-0 overflow-hidden">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="m-0">Aforo por zona</h2>
        <div className="flex gap-1">
          <button
            type="button"
            onClick={() => scroll(-1)}
            disabled={!canLeft}
            className="flex size-8 items-center justify-center !p-0 rounded-md border border-border bg-transparent font-bold text-[#111] hover:bg-accent-bg hover:text-accent disabled:border-border-strong disabled:bg-border disabled:text-text-muted disabled:cursor-not-allowed"
            aria-label="Desplazar a la izquierda"
          >
            <svg
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="m15 18-6-6 6-6" />
            </svg>
          </button>
          <button
            type="button"
            onClick={() => scroll(1)}
            disabled={!canRight}
            className="flex size-8 items-center justify-center !p-0 rounded-md border border-border bg-transparent font-bold text-[#111] hover:bg-accent-bg hover:text-accent disabled:border-border-strong disabled:bg-border disabled:text-text-muted disabled:cursor-not-allowed"
            aria-label="Desplazar a la derecha"
          >
            <svg
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="m9 18 6-6-6-6" />
            </svg>
          </button>
        </div>
      </div>
      <div className="overflow-hidden">
        <div
          ref={scrollRef}
          onScroll={updateArrows}
          className="-mx-1 flex snap-x snap-mandatory gap-4 overflow-x-auto px-1 pb-2 [scroll-padding-inline:1rem] scrollbar-none"
        >
          {zones.map((z) => (
            <div key={z.zoneId} className="w-72 shrink-0 snap-start">
              <ZoneGauge zone={z} />
            </div>
          ))}
        </div>
      </div>
    </section>
  )
}
