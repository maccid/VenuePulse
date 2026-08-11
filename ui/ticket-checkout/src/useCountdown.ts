import { useEffect, useState } from 'react'

// Segundos restantes hasta `expiresAtUtc`, refrescado cada segundo — null si no hay deadline
// vigente (reserva ya resuelta o nunca trackeada, ver TicketStatusDto en CheckIn y ADR 0025).
export function useCountdown(expiresAtUtc: string | null): number | null {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (!expiresAtUtc) return
    const interval = setInterval(() => setNow(Date.now()), 1000)
    return () => clearInterval(interval)
  }, [expiresAtUtc])

  if (!expiresAtUtc) return null
  return Math.max(0, Math.round((new Date(expiresAtUtc).getTime() - now) / 1000))
}

export function formatCountdown(seconds: number): string {
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  return `${m}:${s.toString().padStart(2, '0')}`
}
