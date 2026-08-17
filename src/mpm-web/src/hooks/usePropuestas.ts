import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDelete, apiDownload, apiGet, apiPatch, apiPost, apiPut } from '../lib/apiClient';
import type {
  ApiResponse,
  AvisarResponse,
  CatalogoCapitulo,
  CatalogoCertificacion,
  CatalogoExperiencia,
  CatalogoPage,
  GenerarPropuestaRequest,
  GenerarPropuestaResponse,
  PropuestaHistorial,
  RecomendacionRequest,
  RecomendacionResponse,
} from '../types';

const BASE = (codigoExterno: string) =>
  `/api/v1/licitaciones/${encodeURIComponent(codigoExterno)}`;

function catalogoUrl(path: string, activo = true): string {
  const params = new URLSearchParams({ page: '1', size: '200', activo: String(activo) });
  return `/api/v1/propuestas/catalogos/${path}?${params.toString()}`;
}

export function useCatalogoCapitulos(enabled = true) {
  return useQuery({
    queryKey: ['propuestas-catalogo', 'capitulos'],
    queryFn: () => apiGet<ApiResponse<CatalogoPage<CatalogoCapitulo>>>(catalogoUrl('capitulos')),
    enabled,
    staleTime: 60_000,
    retry: 1,
  });
}

export function useCatalogoCertificaciones(enabled = true) {
  return useQuery({
    queryKey: ['propuestas-catalogo', 'certificaciones'],
    queryFn: () => apiGet<ApiResponse<CatalogoPage<CatalogoCertificacion>>>(catalogoUrl('certificaciones')),
    enabled,
    staleTime: 60_000,
    retry: 1,
  });
}

export function useCatalogoExperiencias(enabled = true) {
  return useQuery({
    queryKey: ['propuestas-catalogo', 'experiencias'],
    queryFn: () => apiGet<ApiResponse<CatalogoPage<CatalogoExperiencia>>>(catalogoUrl('experiencias')),
    enabled,
    staleTime: 60_000,
    retry: 1,
  });
}

// ── Sincronización Census & CRUD de Catálogos ─────────────────────────────

export interface CensusSyncResult {
  procesadas: number;
  insertadas: number;
  actualizadas: number;
  sinArchivo: number;
  durationMs: number;
}

export function useSincronizarCertificacionesCensus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () =>
      apiPost<ApiResponse<CensusSyncResult>>('/api/v1/propuestas/catalogos/certificaciones/sincronizar-census', {}),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['propuestas-catalogo', 'certificaciones'] });
    },
  });
}

export function useCrearExperiencia() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<CatalogoExperiencia>) =>
      apiPost<ApiResponse<CatalogoExperiencia>>('/api/v1/propuestas/catalogos/experiencias', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['propuestas-catalogo', 'experiencias'] });
    },
  });
}

export function useActualizarExperiencia() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: { id: number; data: Partial<CatalogoExperiencia> }) =>
      apiPut<ApiResponse<CatalogoExperiencia>>(`/api/v1/propuestas/catalogos/experiencias/${params.id}`, params.data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['propuestas-catalogo', 'experiencias'] });
    },
  });
}

export function useEliminarExperiencia() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) =>
      apiDelete<ApiResponse<{ experienciaId: number }>>(`/api/v1/propuestas/catalogos/experiencias/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['propuestas-catalogo', 'experiencias'] });
    },
  });
}

export function useCrearCertificacion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<CatalogoCertificacion>) =>
      apiPost<ApiResponse<CatalogoCertificacion>>('/api/v1/propuestas/catalogos/certificaciones', data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['propuestas-catalogo', 'certificaciones'] });
    },
  });
}

export function useEliminarCertificacion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: number) =>
      apiDelete<ApiResponse<{ certificacionId: number }>>(`/api/v1/propuestas/catalogos/certificaciones/${id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['propuestas-catalogo', 'certificaciones'] });
    },
  });
}

// ── Generación de Propuestas y Avisos ─────────────────────────────────────

export function useRecomendaciones() {
  return useMutation({
    mutationFn: (request: RecomendacionRequest) =>
      apiPost<ApiResponse<RecomendacionResponse>>('/api/v1/propuestas/recomendaciones', request),
  });
}

export function useGenerarPropuesta() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: { codigoExterno: string; request: GenerarPropuestaRequest }) =>
      apiPost<ApiResponse<GenerarPropuestaResponse>>(`${BASE(params.codigoExterno)}/propuestas/generar`, params.request),
    onSuccess: (_data, params) => {
      queryClient.invalidateQueries({ queryKey: ['propuestas-historial', params.codigoExterno] });
    },
  });
}

export function usePropuestasHistorial(codigoExterno: string | null, enabled = true) {
  return useQuery({
    queryKey: ['propuestas-historial', codigoExterno],
    queryFn: () => apiGet<ApiResponse<CatalogoPage<PropuestaHistorial>>>(`${BASE(codigoExterno!)}/propuestas?page=1&size=100`),
    enabled: !!codigoExterno && enabled,
    staleTime: 15_000,
    retry: 1,
  });
}

export function useActualizarEstadoPropuesta() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: { codigoExterno: string; propuestaId: number; estado: 'enviada' | 'descartada' }) =>
      apiPatch<ApiResponse<PropuestaHistorial>>(
        `${BASE(params.codigoExterno)}/propuestas/${params.propuestaId}/estado`,
        { estado: params.estado },
      ),
    onSuccess: (_data, params) => {
      queryClient.invalidateQueries({ queryKey: ['propuestas-historial', params.codigoExterno] });
    },
  });
}

export function useAvisarDecision() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: { codigoExterno: string; decisionId: number; destinatarios: string[] }) =>
      apiPost<ApiResponse<AvisarResponse>>(
        `${BASE(params.codigoExterno)}/decision/${params.decisionId}/avisar`,
        { destinatarios: params.destinatarios },
      ),
    onSuccess: (_data, params) => {
      queryClient.invalidateQueries({ queryKey: ['licitacion-decision', params.codigoExterno] });
    },
  });
}

export function descargarPropuesta(codigoExterno: string, propuestaId: number): Promise<Blob> {
  return apiDownload(`${BASE(codigoExterno)}/propuestas/${propuestaId}/archivo`);
}

export interface ExportarDriveResponse {
  propuestaId: number;
  nombreArchivo: string;
  driveFileId: string;
}

export function useExportarPropuestaDrive() {
  return useMutation({
    mutationFn: (params: { codigoExterno: string; propuestaId: number }) =>
      apiPost<ApiResponse<ExportarDriveResponse>>(
        `${BASE(params.codigoExterno)}/propuestas/${params.propuestaId}/exportar-drive`,
        {},
      ),
  });
}
