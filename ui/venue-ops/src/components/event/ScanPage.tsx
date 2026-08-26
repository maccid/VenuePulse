import type { ScanResult } from '../../shared/types'
import ScanPanel from './ScanPanel'

interface Props {
  onScan: (ticketId: string) => Promise<ScanResult>
}

export default function ScanPage({ onScan }: Props) {
  return (
    <div className="mx-auto max-w-[480px]">
      <ScanPanel onScan={onScan} />
    </div>
  )
}
