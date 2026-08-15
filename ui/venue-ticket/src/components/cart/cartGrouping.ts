import type { CartLine } from '@venue/core/domain'

export interface CartGroup {
  key: string
  zoneName: string
  lines: CartLine[]
}

export function groupCartLines(cart: CartLine[]): CartGroup[] {
  const groups = new Map<string, CartGroup>()
  for (const line of cart) {
    const key = `${line.zoneId}|${line.ticketTypeName}`
    const group = groups.get(key)
    if (group) group.lines.push(line)
    else groups.set(key, { key, zoneName: line.zoneName, lines: [line] })
  }
  return [...groups.values()]
}
