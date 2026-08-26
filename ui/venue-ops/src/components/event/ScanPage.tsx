import type { ScanResult } from '../../shared/types'
import ScanPanel from './ScanPanel'

interface Props {
  onScan: (ticketId: string) => Promise<ScanResult>
}

export default function ScanPage({ onScan }: Props) {
  return (
    <div className="mx-auto max-w-[480px]">
      <a href="/venue-ops/" className="mb-4 inline-block text-sm text-text-muted hover:text-text">
        ← Volver a operaciones
      </a>
      <ScanPanel onScan={onScan} />
    </div>
  )
}
