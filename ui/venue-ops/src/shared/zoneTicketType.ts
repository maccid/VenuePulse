import type { TicketType } from './types'

export function matchTicketType(zoneName: string, ticketTypes: TicketType[]): TicketType | undefined {
  const exactMatch = ticketTypes.find((t) => t.name === zoneName)
  if (exactMatch) return exactMatch

  const containedMatches = ticketTypes.filter((t) => zoneName.includes(t.name))
  const longestMatch = containedMatches.sort((a, b) => b.name.length - a.name.length)[0]
  if (longestMatch) return longestMatch

  return ticketTypes.find((t) => t.name === 'General') ?? ticketTypes[0]
}
