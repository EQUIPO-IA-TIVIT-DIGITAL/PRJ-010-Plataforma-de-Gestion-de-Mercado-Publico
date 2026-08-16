import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '../lib/apiClient';
import type {
  CensoMatchEstado,
  CensoMatchRequest,
  CensoMatchResult,
  CensoPreferencias,
  CensoPreferenciasUpdate,
  Decision,
  DecisionEstado,
  DecisionValor,
} from '../types/licitacion';

const MATCH_BASE = (codigoExterno: string) =>
  `/api/v1/licitaciones/${encodeURIComponent(codigoExterno)}/match-capacidades`;

const DECISION_BASE = (codigoExterno: string) =>
  `/api/v1/licitaciones/${encodeURIComponent(codigoExterno)}/decision`;

const PREFERENCIAS = '/api/v1/usuarios/me/preferencias-censo';

/**
 * Estado + último resultado del match de capacidades (lectura local, sin Census).
 * Se invalida tras un POST exitoso.
 */
export function useMatchCapacidades(codigoExterno: string | null) {
  return useQuery({
    queryKey: ['licitacion-censo-match', codigoExterno],
    queryFn: () => apiGet<{ data: CensoMatchEstado }>(MATCH_BASE(codigoExterno!)),
    enabled: !!codigoExterno,
    staleTime: 15_000,
    retry: 1,
  });
}

/**
 * Ejecuta (o re-ejecuta) el match de capacidades contra Census.
 * El body es opcional: los requisitos se toman del último análisis comercial completado;
 * `filtrarPais`/`pais` respetan la precedencia body > preferencias > defaults.
 */
export function useEjecutarMatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: { codigoExterno: string; body?: CensoMatchRequest }) =>
      apiPost<{ data: CensoMatchResult }>(MATCH_BASE(params.codigoExterno), params.body),
    onSuccess: (_data, params) => {
      queryClient.invalidateQueries({ queryKey: ['licitacion-censo-match', params.codigoExterno] });
    },
  });
}

/** Estado vigente de la decisión GO/NO GO de la licitación. */
export function useDecision(codigoExterno: string | null) {
  return useQuery({
    queryKey: ['licitacion-decision', codigoExterno],
    queryFn: () => apiGet<{ data: DecisionEstado }>(DECISION_BASE(codigoExterno!)),
    enabled: !!codigoExterno,
    staleTime: 15_000,
    retry: 1,
  });
}

/** Registra (o reemplaza) la decisión del gerente; el snapshot IA lo copia el backend. */
export function useRegistrarDecision() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (params: { codigoExterno: string; decision: DecisionValor; motivo?: string }) =>
      apiPost<{ data: Decision }>(DECISION_BASE(params.codigoExterno), {
        decision: params.decision,
        ...(params.motivo ? { motivo: params.motivo } : {}),
      }),
    onSuccess: (_data, params) => {
      queryClient.invalidateQueries({ queryKey: ['licitacion-decision', params.codigoExterno] });
    },
  });
}

/** Preferencias "Filtrar por país" + país del usuario (defaults: false / Chile). */
export function usePreferenciasCenso() {
  return useQuery({
    queryKey: ['usuario-preferencias-censo'],
    queryFn: () => apiGet<{ data: CensoPreferencias }>(PREFERENCIAS),
    staleTime: 60_000,
    retry: 1,
  });
}

/** Actualización parcial (UPSERT) de las preferencias de país. */
export function useActualizarPreferenciasCenso() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: CensoPreferenciasUpdate) =>
      apiPut<{ data: CensoPreferencias }>(PREFERENCIAS, body),
    onSuccess: (data) => {
      queryClient.setQueryData(['usuario-preferencias-censo'], data);
    },
  });
}
