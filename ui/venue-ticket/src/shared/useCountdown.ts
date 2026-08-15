import { useEffect, useState } from 'react'

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
