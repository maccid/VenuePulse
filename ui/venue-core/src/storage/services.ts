export interface StorageService {
  get(key: string): string | null
  set(key: string, value: string): void
  remove(key: string): void
}

function createWebStorageService(storage: Storage): StorageService {
  return {
    get: (key) => {
      try {
        return storage.getItem(key)
      } catch {
        return null
      }
    },
    set: (key, value) => {
      try {
        storage.setItem(key, value)
      } catch {}
    },
    remove: (key) => {
      try {
        storage.removeItem(key)
      } catch {}
    },
  }
}

class MemoryStorageService implements StorageService {
  private readonly data = new Map<string, string>()

  get(key: string): string | null {
    return this.data.get(key) ?? null
  }

  set(key: string, value: string): void {
    this.data.set(key, value)
  }

  remove(key: string): void {
    this.data.delete(key)
  }
}

function webStorage(name: 'sessionStorage' | 'localStorage'): StorageService {
  try {
    const storage = (globalThis as unknown as Record<string, unknown>)[name] as Storage | undefined
    return storage ? createWebStorageService(storage) : new MemoryStorageService()
  } catch {
    return new MemoryStorageService()
  }
}

export const sessionStorageService = webStorage('sessionStorage')
export const localStorageService = webStorage('localStorage')
export const memoryStorageService = new MemoryStorageService()
