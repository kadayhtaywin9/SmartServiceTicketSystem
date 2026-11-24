import { ref, onUnmounted } from 'vue'
import * as signalR from '@microsoft/signalr'

export function useTicketHub() {
  const activeViewers = ref<string[]>([])
  const typingAgent = ref<string | null>(null)
  const slaAlerts = ref<Array<{ ticketCode: string; severity: string; message: string }>>([])
  let connection: signalR.HubConnection | null = null
  let currentRoomId: string | null = null

  const connect = async (onMessageReceived?: (msg: any) => void, onTicketUpdated?: () => void) => {
    const token = localStorage.getItem('stms_token') || ''
    connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5000/hubs/tickets', {
        accessTokenFactory: () => token
      })
      .withAutomaticReconnect()
      .build()

    connection.on('PresenceUpdated', (viewers: string[]) => {
      activeViewers.value = viewers
    })

    connection.on('AgentTyping', (payload: { agentName: string; isTyping: boolean }) => {
      typingAgent.value = payload.isTyping ? payload.agentName : null
    })

    connection.on('ReceiveMessage', (msg: any) => {
      if (onMessageReceived) onMessageReceived(msg)
    })

    connection.on('TicketUpdated', () => {
      if (onTicketUpdated) onTicketUpdated()
    })

    connection.on('TicketCreated', () => {
      if (onTicketUpdated) onTicketUpdated()
    })

    connection.on('SlaAlert', (alert: any) => {
      slaAlerts.value.unshift(alert)
    })

    try {
      await connection.start()
    } catch {
      // Auto-reconnect handles transient connection delays
    }
  }

  const joinTicketRoom = async (ticketId: number | string, agentName: string) => {
    const nextRoom = String(ticketId)
    if (connection?.state !== signalR.HubConnectionState.Connected) return

    if (currentRoomId && currentRoomId !== nextRoom) {
      try {
        await connection.invoke('LeaveTicketRoom', currentRoomId)
      } catch {
        // ignore leave error
      }
    }

    typingAgent.value = null
    currentRoomId = nextRoom
    await connection.invoke('JoinTicketRoom', nextRoom, agentName)
  }

  const sendTyping = async (ticketId: number | string, agentName: string, isTyping: boolean) => {
    if (connection?.state === signalR.HubConnectionState.Connected) {
      await connection.invoke('NotifyTyping', String(ticketId), agentName, isTyping)
    }
  }

  onUnmounted(async () => {
    if (connection) {
      if (currentRoomId && connection.state === signalR.HubConnectionState.Connected) {
        try {
          await connection.invoke('LeaveTicketRoom', currentRoomId)
        } catch {
          // ignore
        }
      }
      await connection.stop()
    }
  })

  return {
    activeViewers,
    typingAgent,
    slaAlerts,
    connect,
    joinTicketRoom,
    sendTyping
  }
}
