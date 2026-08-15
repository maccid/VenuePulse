import { useStore } from 'zustand'
import { createStore } from 'zustand/vanilla'
import type { StateCreator, StoreApi } from 'zustand/vanilla'

export interface SharedStore<T> extends StoreApi<T> {
  use: <U>(selector: (state: T) => U) => U
}

const REGISTRY_KEY = '__venue_core_shared_stores__'

type Registry = Record<string, SharedStore<unknown>>

function getRegistry(): Registry {
  const scope = globalThis as unknown as { [key: string]: unknown }
  return (scope[REGISTRY_KEY] ??= {}) as Registry
}

export function createSharedStore<T>(name: string, initializer: StateCreator<T>): SharedStore<T> {
  const registry = getRegistry()
  const existing = registry[name]
  if (existing) return existing as SharedStore<T>

  const api = createStore<T>()(initializer)
  const shared = Object.assign(api, {
    use: <U>(selector: (state: T) => U) => useStore(api, selector),
  }) as SharedStore<T>

  registry[name] = shared as SharedStore<unknown>
  return shared
}
