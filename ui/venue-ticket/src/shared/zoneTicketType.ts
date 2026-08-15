import type { TicketType, ZoneOccupancy } from './types'

export function matchTicketType(zoneName: string, ticketTypes: TicketType[]): TicketType | undefined {
  return (
    ticketTypes.find((t) => t.name === zoneName) ??
    ticketTypes.find((t) => t.name === 'General') ??
    ticketTypes[0]
  )
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
