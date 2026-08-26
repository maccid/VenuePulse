import type { ScanResult } from '../../shared/types'
import SiteHeader from '../ui/SiteHeader'
import ScanPanel from './ScanPanel'

interface Props {
  onScan: (ticketId: string) => Promise<ScanResult>
}

export default function ScanPage({ onScan }: Props) {
  return (
    <div className="mx-auto max-w-[1440px] px-6 py-16">
      <SiteHeader
        badge="VT"
        title="VenuePulse · Torniquete"
        subtitle="Escanea la entrada para registrar el acceso"
      />
      <div className="mx-auto max-w-[420px]">
        <ScanPanel onScan={onScan} />
      </div>
    </div>
  )
}
