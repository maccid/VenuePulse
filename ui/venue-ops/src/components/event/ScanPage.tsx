import type { ScanResult } from '../../shared/types'
import SiteHeader from '../ui/SiteHeader'
import ScanPanel from './ScanPanel'

interface Props {
  onScan: (ticketId: string) => Promise<ScanResult>
}

export default function ScanPage({ onScan }: Props) {
  return (
    <div className="mx-auto flex min-h-screen max-w-[420px] items-center px-4">
      <div className="w-full">
        <SiteHeader
          badge="VT"
          title="VenuePulse · Torniquete"
          subtitle="Escanea la entrada para registrar el acceso"
        />
        <ScanPanel onScan={onScan} />
      </div>
    </div>
  )
}
