import type { TicketStatus } from '@venue/core/domain'

export type ScanResult = { kind: 'idle' } | { kind: 'ok' } | { kind: 'error'; message: string }

export interface ActivityEntry {
  id: string
  zoneName: string
  status: TicketStatus
  rejectionReason: string | null
  atUtc: string
}
