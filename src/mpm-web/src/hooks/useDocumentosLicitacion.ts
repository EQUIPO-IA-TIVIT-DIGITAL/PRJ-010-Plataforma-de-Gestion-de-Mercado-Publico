import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDownload, apiGet, apiPost } from '../lib/apiClient';
import type { DescargarDocumentosResult, EstadoDocumentos } from '../types/licitacion';

const BASE = (codigoExterno: string) => `/api/v1/licitaciones/${encodeURIComponent(codigoExterno)}/documentos`;

/** Estado de los documentos guardados; hace polling mientras la descarga está en curso. */
export function useEstadoDocumentos(codigoExterno: string | null) {
  return useQuery({
    queryKey: ['licitacion-documentos', codigoExterno],
    queryFn: () => apiGet<{ data: EstadoDocumentos }>(BASE(codigoExterno!)),
    enabled: !!codigoExterno,
    staleTime: 15_000,
    retry: 1,
    refetchInterval: (query) =>
      (query.state.data?.data.estadoConjunto === 'descargando' ? 3000 : false) as false | 3000,
  });
}

/** Dispara la descarga bajo demanda de los documentos (botón en la ficha). */
export function useDescargarDocumentos() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: { codigoExterno: string; forzar?: boolean }) =>
      apiPost<{ data: DescargarDocumentosResult }>(BASE(params.codigoExterno) + '/descargar', {
        forzar: params.forzar ?? false,
      }),
    onSuccess: (_data, params) => {
      queryClient.invalidateQueries({ queryKey: ['licitacion-documentos', params.codigoExterno] });
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
