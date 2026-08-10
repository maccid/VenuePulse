import type { TicketType } from './types'

// Fusiona zona (AccessControl) con tipo de entrada (Ticketing) por nombre, sin relación real en el
// backend (bounded contexts separados, ver ADR 0016/0018) — copia conceptual de
// ui/ticket-checkout/src/zoneTicketType.ts (ADR 0004, sin código compartido entre micro frontends),
// pero con una regla más robusta que la copia original: además de la igualdad exacta ("Pista" ==
// "Pista"), se acepta que el nombre del tipo de entrada aparezca DENTRO del nombre de la zona
// ("Grada VIP".includes("VIP")) — verificado en real contra el stack: la igualdad exacta por sí
// sola deja "Grada VIP" sin ningún tipo de entrada que coincida (el tipo se llama "VIP", no "Grada
// VIP") y cae al `General` de forma incorrecta. Se prefiere el nombre de tipo más largo que
// coincida, para no confundir "VIP" con un "General" que también apareciera como substring.
export function matchTicketType(zoneName: string, ticketTypes: TicketType[]): TicketType | undefined {
  const exact = ticketTypes.find((t) => t.name === zoneName)
  if (exact) return exact

  const contained = ticketTypes
    .filter((t) => zoneName.includes(t.name))
    .sort((a, b) => b.name.length - a.name.length)[0]
  if (contained) return contained

  return ticketTypes.find((t) => t.name === 'General') ?? ticketTypes[0]
}
