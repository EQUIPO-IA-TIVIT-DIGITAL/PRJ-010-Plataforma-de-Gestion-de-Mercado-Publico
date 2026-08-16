/**
 * Cliente HTTP central de la aplicación.
 *
 * - Adjunta el Bearer token desde localStorage.
 * - Ante un 401 dispara el handler global de sesión expirada UNA sola vez,
 *   aunque múltiples requests fallen en paralelo.
 * - El resto de errores se lanzan como Error con el mensaje del backend.
 */

let onSessionExpired: (() => void) | null = null;
let sessionExpiredFired = false;

export function registerSessionExpiredHandler(handler: () => void) {
  onSessionExpired = handler;
  sessionExpiredFired = false;
}

function getToken(): string | null {
  return localStorage.getItem('mpm_token');
}

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

export async function apiFetch<T>(url: string, options?: RequestInit): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options?.body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
    ...((options?.headers as Record<string, string>) ?? {}),
  };

  const response = await fetch(url, { ...options, headers });

  if (response.status === 401) {
    if (!sessionExpiredFired && onSessionExpired) {
      sessionExpiredFired = true;
      onSessionExpired();
    }
    throw new ApiError(401, 'Sesión expirada');
  }

  if (!response.ok) {
    let msg = `Error ${response.status}`;
    try {
      const err = await response.json();
      if (err?.message) msg = err.message;
    } catch {
      /* ignore */
    }
    throw new ApiError(response.status, msg);
  }

  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

export function apiGet<T>(url: string): Promise<T> {
  return apiFetch<T>(url);
}

export function apiPost<T>(url: string, body?: unknown): Promise<T> {
  return apiFetch<T>(url, {
    method: 'POST',
    body: body instanceof FormData ? body : body !== undefined ? JSON.stringify(body) : undefined,
  });
}

export function apiPut<T>(url: string, body?: unknown): Promise<T> {
  return apiFetch<T>(url, {
    method: 'PUT',
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
}

export function apiPatch<T>(url: string, body?: unknown): Promise<T> {
  return apiFetch<T>(url, {
    method: 'PATCH',
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
}

export function apiDelete<T>(url: string): Promise<T> {
  return apiFetch<T>(url, { method: 'DELETE' });
}

/**
 * Descarga de binario con el Bearer token (036-flujo-comercial-ofertas).
 * El token viaja en header, por lo que un <a href> directo no sirve.
 */
export async function apiDownload(url: string): Promise<Blob> {
  const token = getToken();
  const headers: Record<string, string> = {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };

  const response = await fetch(url, { headers });

  if (response.status === 401) {
    if (!sessionExpiredFired && onSessionExpired) {
      sessionExpiredFired = true;
      onSessionExpired();
    }
    throw new ApiError(401, 'Sesión expirada');
  }

  if (!response.ok) {
    let msg = `Error ${response.status}`;
    try {
      const err = await response.json();
      if (err?.message) msg = err.message;
    } catch {
      /* ignore */
    }
    throw new ApiError(response.status, msg);
  }

  return response.blob();
}
