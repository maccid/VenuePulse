import { useEffect, useRef } from 'react'
import type { HubConnection } from '@microsoft/signalr'
import { createHubConnection } from './hubConnection'
import { isAbortedHubStartError } from './hubErrors'

export function useEventScopedConnection(
  hubPath: string,
  activeId: string | null,
  registerHandlers: (connection: HubConnection) => void,
): void {
  const registerRef = useRef(registerHandlers)
  const activeIdRef = useRef(activeId)
  const joinedEventRef = useRef<string | null>(null)
  const connectionRef = useRef<HubConnection | null>(null)

  useEffect(() => {
    registerRef.current = registerHandlers
  }, [registerHandlers])

  useEffect(() => {
    activeIdRef.current = activeId
  }, [activeId])

  useEffect(() => {
    const connection = createHubConnection(hubPath)
    connectionRef.current = connection
    registerRef.current(connection)

    connection.onreconnected(() => {
      const current = joinedEventRef.current
      if (current) void connection.invoke('JoinEvent', current).catch(() => undefined)
    })

    connection
      .start()
      .then(() => {
        const current = activeIdRef.current
        if (current) {
          void connection.invoke('JoinEvent', current).catch(() => undefined)
          joinedEventRef.current = current
        }
      })
      .catch((err) => {
        if (!isAbortedHubStartError(err)) console.error(`No se pudo conectar al hub ${hubPath}`, err)
      })

    return () => {
      joinedEventRef.current = null
      connectionRef.current = null
      void connection.stop()
    }
  }, [hubPath])

  useEffect(() => {
    const connection = connectionRef.current
    const previous = joinedEventRef.current
    if (previous === activeId) return

    if (previous && connection) void connection.invoke('LeaveEvent', previous).catch(() => undefined)
    if (activeId && connection) void connection.invoke('JoinEvent', activeId).catch(() => undefined)
    joinedEventRef.current = activeId
  }, [activeId])
}
