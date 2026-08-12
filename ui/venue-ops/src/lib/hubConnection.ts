import * as signalR from '@microsoft/signalr'

export function createHubConnection(hubPath: string): signalR.HubConnection {
  return new signalR.HubConnectionBuilder().withUrl(hubPath).withAutomaticReconnect().build()
}
