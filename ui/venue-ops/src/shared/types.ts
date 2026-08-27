import type { TicketStatus } from '@venue/core/domain'

export type ScanResult = { kind: 'idle' } | { kind: 'ok'; message?: string } | { kind: 'error'; message: string }

export interface ActivityEntry {
  id: string
  orderId: string | null
  ticketId: string | null
  ticketCount: number
  ticketTypeSummary: string | null
  zoneName: string
  status: TicketStatus
  rejectionReason: string | null
  atUtc: string
}
