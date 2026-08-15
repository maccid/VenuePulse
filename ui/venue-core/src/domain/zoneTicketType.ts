import type { TicketType } from './ticketing'
import type { ZoneOccupancy } from './zones'

export function matchTicketType(zoneName: string, ticketTypes: TicketType[]): TicketType | undefined {
  const exactMatch = ticketTypes.find((t) => t.name === zoneName)
  if (exactMatch) return exactMatch

  const containedMatches = ticketTypes.filter((t) => zoneName.includes(t.name))
  const longestMatch = containedMatches.sort((a, b) => b.name.length - a.name.length)[0]
  if (longestMatch) return longestMatch

  return ticketTypes.find((t) => t.name === 'General') ?? ticketTypes[0]
}

export function pricingByZoneId(
  zones: ZoneOccupancy[],
  ticketTypes: TicketType[],
): Record<string, { price: number; effectivePrice: number; priceTier: string }> {
  const result: Record<string, { price: number; effectivePrice: number; priceTier: string }> = {}

  for (const zone of zones) {
    const ticketType = matchTicketType(zone.name, ticketTypes)
    if (ticketType) {
      result[zone.zoneId] = {
        price: ticketType.basePrice,
        effectivePrice: ticketType.effectivePrice,
        priceTier: ticketType.priceTier,
      }
    }
  }

  return result
}
