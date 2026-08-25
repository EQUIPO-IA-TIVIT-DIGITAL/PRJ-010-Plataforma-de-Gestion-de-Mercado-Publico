import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDownload, apiGet, apiPost, apiPostForm } from '../lib/apiClient';
import type { DescargarDocumentosResult, EstadoDocumentos } from '../types/licitacion';

const BASE = (codigoExterno: string) => `/api/v1/licitaciones/${encodeURIComponent(codigoExterno)}/documentos`;

/** Estado de los documentos guardados; hace polling activo mientras la descarga está en curso. */
export function useEstadoDocumentos(codigoExterno: string | null) {
  return useQuery({
    queryKey: ['licitacion-documentos', codigoExterno],
    queryFn: async () => {
      const res = await apiGet<{ data: EstadoDocumentos }>(BASE(codigoExterno!));
      return res;
    },
    enabled: !!codigoExterno,
    staleTime: 0,
    refetchInterval: (query) => {
      const estado = query.state.data?.data?.estadoConjunto;
      return estado === 'descargando' ? 1500 : false;
    },
  });
}

/** Dispara la descarga bajo demanda de los documentos (botón en la ficha). @deprecated ADR-015 - modo manual por defecto */
export function useDescargarDocumentos() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: { codigoExterno: string; forzar?: boolean }) =>
      apiPost<{ data: DescargarDocumentosResult }>(BASE(params.codigoExterno) + '/descargar', {
        forzar: params.forzar ?? false,
      }),
    onSuccess: (data, params) => {
      // Inmediatamente actualizamos la caché local a 'descargando' para activar el polling instantáneo
      queryClient.setQueryData(['licitacion-documentos', params.codigoExterno], {
        data: {
          estadoConjunto: 'descargando',
          documentos: [],
          conjuntoHash: data.data?.conjuntoHash ?? null,
          descargaError: null,
        },
      });
      queryClient.invalidateQueries({ queryKey: ['licitacion-documentos', params.codigoExterno] });
    },
  });
}

/** Carga manual de pliegos (ADR-015, 038). */
export function useUploadManualDocumentos() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: { codigoExterno: string; files: File[] }) => {
      const form = new FormData();
      params.files.forEach((f) => form.append('files', f, f.name));
      return apiPostForm<{ data: { descargados: number; reutilizados: number; rechazados: number; errores: string[]; conjuntoHash: string | null; mensaje: string } }>(
        BASE(params.codigoExterno) + '/upload-manual',
        form,
      );
    },
    onSuccess: (_data, params) => {
      queryClient.invalidateQueries({ queryKey: ['licitacion-documentos', params.codigoExterno] });
      // También invalida análisis comercial (puede pasar de pendiente a desactualizado)
      queryClient.invalidateQueries({ queryKey: ['licitacion-analisis-comercial', params.codigoExterno] });
    },
  });
}

/** Descarga binaria de un documento guardado (blob → descarga en el navegador). */
export async function descargarArchivoDocumento(
  codigoExterno: string,
  documento: { id: number; nombreArchivo: string },
): Promise<void> {
  const blob = await apiDownload(`${BASE(codigoExterno)}/${documento.id}/archivo`);
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = documento.nombreArchivo;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

export function formatTamanio(bytes: number | null): string {
  if (bytes == null) return '-';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}
