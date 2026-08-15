import { useCallback, useState } from 'react'
import { localStorageService, sessionStorageService } from './services'
import type { StorageService } from './services'

function readValue<T>(service: StorageService, key: string, initialValue: T | (() => T)): T {
  try {
    const raw = service.get(key)
    if (raw !== null) return JSON.parse(raw) as T
  } catch {}
  return typeof initialValue === 'function' ? (initialValue as () => T)() : initialValue
}

export function usePersistedState<T>(
  service: StorageService,
  key: string,
  initialValue: T | (() => T),
): [T, (next: T) => void] {
  const [value, setValue] = useState<T>(() => readValue(service, key, initialValue))

  const setPersisted = useCallback(
    (next: T) => {
      setValue(next)
      try {
        if (next === null || next === undefined) service.remove(key)
        else service.set(key, JSON.stringify(next))
      } catch {}
    },
    [service, key],
  )

  return [value, setPersisted]
}

export function useSessionStorage<T>(key: string, initialValue: T | (() => T)): [T, (next: T) => void] {
  return usePersistedState(sessionStorageService, key, initialValue)
}

export function useLocalStorage<T>(key: string, initialValue: T | (() => T)): [T, (next: T) => void] {
  return usePersistedState(localStorageService, key, initialValue)
}
