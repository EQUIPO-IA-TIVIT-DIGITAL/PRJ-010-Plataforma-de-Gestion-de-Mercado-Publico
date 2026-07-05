import { useEffect, useRef, useState, useCallback } from 'react';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';

export function useSignalR(conversacionId: number | null) {
  const connectionRef = useRef<any>(null);
  const queryClient = useQueryClient();
  const [typingUserId, setTypingUserId] = useState<string | null>(null);
  const typingTimeoutRef = useRef<ReturnType<typeof setTimeout>>();

  const clearTypingAfterDelay = useCallback((userId: string) => {
    setTypingUserId(userId);
    if (typingTimeoutRef.current) clearTimeout(typingTimeoutRef.current);
    typingTimeoutRef.current = setTimeout(() => {
      setTypingUserId(null);
    }, 3000);
  }, []);

  useEffect(() => {
    const token = localStorage.getItem('mpm_token');
    if (!token) return;

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/mensajeria', { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Information)
      .build();

    connectionRef.current = connection;

    connection.start()
      .then(() => {
        if (conversacionId) {
          connection.invoke('UnirseConversacion', conversacionId);
        }
      })
      .catch(err => console.error('SignalR connection error:', err));

    connection.on('RecibirMensaje', () => {
      queryClient.invalidateQueries({ queryKey: ['mensajes', conversacionId] });
      queryClient.invalidateQueries({ queryKey: ['conversaciones'] });
    });

    connection.on('MensajeEditado', () => {
      queryClient.invalidateQueries({ queryKey: ['mensajes', conversacionId] });
    });

    connection.on('MensajeEliminado', () => {
      queryClient.invalidateQueries({ queryKey: ['mensajes', conversacionId] });
    });

    connection.on('MensajeLeido', () => {
      queryClient.invalidateQueries({ queryKey: ['mensajes', conversacionId] });
    });

    connection.on('TypingIndicator', (userId: string) => {
      clearTypingAfterDelay(userId);
    });

    connection.on('PresenceUpdate', () => {
      queryClient.invalidateQueries({ queryKey: ['presencia'] });
    });

    return () => {
      setTypingUserId(null);
      if (conversacionId && connectionRef.current) {
        connectionRef.current.invoke('SalirConversacion', conversacionId);
      }
      connectionRef.current?.stop();
    };
  }, [conversacionId, queryClient, clearTypingAfterDelay]);

  const notificarTyping = useCallback((escribiendo: boolean) => {
    if (conversacionId && connectionRef.current) {
      connectionRef.current.invoke('NotificarTyping', conversacionId, escribiendo);
    }
  }, [conversacionId]);

  return { notificarTyping, typingUserId };
}
