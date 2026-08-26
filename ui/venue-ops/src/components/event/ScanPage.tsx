import type { ScanResult } from '../../shared/types'
import ScanPanel from './ScanPanel'

interface Props {
  onScan: (ticketId: string) => Promise<ScanResult>
}

export default function ScanPage({ onScan }: Props) {
  return (
    <div className="mx-auto flex min-h-screen max-w-[420px] items-center px-4">
      <div className="w-full">
        <h1 className="mb-6 text-center text-lg">VenuePulse · Control de acceso</h1>
        <ScanPanel onScan={onScan} />
      </div>
    </div>
  )
}
