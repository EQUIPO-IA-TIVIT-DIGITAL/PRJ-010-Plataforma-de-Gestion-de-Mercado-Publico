import { useState, useCallback, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useConversaciones } from './useConversaciones';
import { useConversacionDetalle } from './useConversacionDetalle';
import { useMensajes } from './useMensajes';
import { useCrearConversacion } from './useCrearConversacion';
import { useEnviarMensaje } from './useEnviarMensaje';
import { useSubirAdjunto } from './useSubirAdjunto';
import { useEditarMensaje } from './useEditarMensaje';
import { useEliminarMensaje } from './useEliminarMensaje';
import { useSignalR } from './useSignalR';
import type { ConversacionFilter, MensajeFilter, CrearConversacionRequest, EnviarMensajeRequest, EditarMensajeRequest } from '../types/mensajeria';

export function useChatLogic() {
  const [searchParams, setSearchParams] = useSearchParams();
  const paramConversacionId = searchParams.get('conversacionId');
  const initialId = paramConversacionId ? Number(paramConversacionId) : null;
  const [selectedConversacionId, setSelectedConversacionId] = useState<number | null>(initialId);

  useEffect(() => {
    if (paramConversacionId) {
      const num = Number(paramConversacionId);
      if (!isNaN(num) && num !== selectedConversacionId) {
        setSelectedConversacionId(num);
      }
    }
  }, [paramConversacionId]);
  const [filter, setFilter] = useState<ConversacionFilter>({
    page: 1,
    pageSize: 20,
    search: null,
    sortBy: 'updated_at',
    sortDir: 'desc',
  });
  const [mensajeFilter, setMensajeFilter] = useState<MensajeFilter>({
    page: 1,
    pageSize: 50,
    before: null,
  });

  const conversacionesQuery = useConversaciones(filter);
  const conversacionDetalleQuery = useConversacionDetalle(selectedConversacionId);
  const mensajesQuery = useMensajes(selectedConversacionId, mensajeFilter);
  const { crearConversacion, isPending: isCreando } = useCrearConversacion();
  const { enviarMensaje, isPending: isEnviando } = useEnviarMensaje();
  const { subirAdjunto, isUploading } = useSubirAdjunto();
  const { editarMensaje, isPending: isEditando } = useEditarMensaje();
  const { eliminarMensaje, isPending: isEliminando } = useEliminarMensaje();
  const { notificarTyping, typingUserId } = useSignalR(selectedConversacionId);

  const handleSelectConversacion = useCallback((id: number) => {
    setSelectedConversacionId(id);
    setMensajeFilter({ page: 1, pageSize: 50, before: null });
    setSearchParams({ conversacionId: String(id) }, { replace: true });
  }, [setSearchParams]);

  const handleCrearConversacion = useCallback((data: CrearConversacionRequest) => {
    crearConversacion(data, {
      onSuccess: (result) => {
        setSelectedConversacionId(result.id);
      },
    });
  }, [crearConversacion]);

  const handleEnviarMensaje = useCallback(async (data: EnviarMensajeRequest, archivos?: File[]) => {
    if (!selectedConversacionId) return;
    const mensaje = await enviarMensaje({ conversacionId: selectedConversacionId, ...data });
    if (archivos && archivos.length > 0 && mensaje) {
      const uploads = archivos.map(a =>
        subirAdjunto({ conversacionId: selectedConversacionId, mensajeId: mensaje.id, archivo: a })
      );
      await Promise.all(uploads);
    }
  }, [selectedConversacionId, enviarMensaje, subirAdjunto]);

  const handleEditarMensaje = useCallback((data: { mensajeId: number } & EditarMensajeRequest) => {
    if (!selectedConversacionId) return;
    editarMensaje({ conversacionId: selectedConversacionId, ...data });
  }, [selectedConversacionId, editarMensaje]);

  const handleEliminarMensaje = useCallback((mensajeId: number) => {
    if (!selectedConversacionId) return;
    eliminarMensaje({ conversacionId: selectedConversacionId, mensajeId });
  }, [selectedConversacionId, eliminarMensaje]);

  const handleTyping = useCallback((escribiendo: boolean) => {
    notificarTyping(escribiendo);
  }, [notificarTyping]);

  return {
    conversaciones: conversacionesQuery.data?.items ?? [],
    isLoadingConversaciones: conversacionesQuery.isLoading,
    conversacionSeleccionada: conversacionDetalleQuery.data ?? null,
    mensajes: mensajesQuery.data?.items ?? [],
    isLoadingMensajes: mensajesQuery.isLoading,
    selectedConversacionId,
    filter,
    isCreando,
    isEnviando: isEnviando || isUploading,
    isEditando,
    isEliminando,
    handleSelectConversacion,
    handleCrearConversacion,
    handleEnviarMensaje,
    handleEditarMensaje,
    handleEliminarMensaje,
    handleTyping,
    setFilter,
    typingUserId,
  };
}
