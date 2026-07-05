import crypto from 'crypto';

// Read at call time (not module init) — dotenv runs after ESM imports are hoisted
const getApiBase = () => process.env.API_BASE_URL || 'http://localhost:5001';

function base64URLEncode(buf) {
  return buf.toString('base64')
    .replace(/=/g, '')
    .replace(/\+/g, '-')
    .replace(/\//g, '_');
}

function generarServiceToken() {
  const jwtSecret = process.env.JWT_SECRET || 'CHANGE-THIS-IN-PRODUCTION-MIN-32-CHARS-LONG';
  const header = { alg: 'HS256', typ: 'JWT' };
  const now = Math.floor(Date.now() / 1000);
  const payload = {
    sub: '00000000-0000-0000-0000-000000000000',
    name: 'Scraper Service',
    email: 'scraper@tivit.cl',
    role: 'admin',
    tenant_id: '',
    tenant_nombre: '',
    iat: now,
    exp: now + 3600,
    iss: process.env.JWT_ISSUER || 'TIVIT.MPM',
    aud: process.env.JWT_AUDIENCE || 'MPM.Users',
  };

  const headerEncoded = base64URLEncode(Buffer.from(JSON.stringify(header)));
  const payloadEncoded = base64URLEncode(Buffer.from(JSON.stringify(payload)));
  const signature = base64URLEncode(
    crypto.createHmac('sha256', jwtSecret)
      .update(`${headerEncoded}.${payloadEncoded}`)
      .digest()
  );

  return `${headerEncoded}.${payloadEncoded}.${signature}`;
}

async function apiCall(method, path, bodyOrFile) {
  const url = `${getApiBase()}${path}`;
  const token = generarServiceToken();

  const headers = {
    'Authorization': `Bearer ${token}`,
  };

  let fetchOptions = { method, headers };

  if (bodyOrFile) {
    if (bodyOrFile instanceof FormData) {
      fetchOptions.body = bodyOrFile;
    } else {
      headers['Content-Type'] = 'application/json';
      fetchOptions.body = JSON.stringify(bodyOrFile);
    }
  }

  const response = await fetch(url, fetchOptions);

  if (!response.ok) {
    const text = await response.text().catch(() => '');
    throw new Error(`API ${response.status}: ${text.substring(0, 200)}`);
  }

  return response.json();
}

export async function crearWorkspaceAnalisis(licitacionId, nombre) {
  try {
    console.log(`[API] Creando workspace de analisis para licitacion ${licitacionId}...`);
    const data = await apiCall('POST', '/api/v1/analisis/workspaces', {
      licitacionId: licitacionId,
      nombre: nombre,
    });
    const workspace = data?.data;
    console.log(`[API] Workspace creado: ID ${workspace?.id}`);
    return { workspaceId: workspace?.id || null, error: null };
  } catch (e) {
    console.log(`[API] Error creando workspace: ${e.message}`);
    return { workspaceId: null, error: e.message };
  }
}

export async function subirDocumento(workspaceId, filePath, fileName) {
  try {
    console.log(`[API] Subiendo documento a workspace ${workspaceId}: ${fileName}`);

    const fs = await import('fs');
    const buffer = fs.readFileSync(filePath);

    const token = generarServiceToken();
    const formData = new FormData();
    formData.append('archivo', new Blob([buffer], { type: 'application/pdf' }), fileName);

    const response = await fetch(`${getApiBase()}/api/v1/analisis/workspaces/${workspaceId}/documentos`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${token}` },
      body: formData,
    });

    if (!response.ok) {
      const text = await response.text().catch(() => '');
      throw new Error(`API ${response.status}: ${text.substring(0, 200)}`);
    }

    const data = await response.json();
    const documento = data?.data;
    console.log(`[API] Documento subido: ID ${documento?.id}`);
    return { documentoId: documento?.id || null, error: null };
  } catch (e) {
    console.log(`[API] Error subiendo documento: ${e.message}`);
    return { documentoId: null, error: e.message };
  }
}

export async function iniciarAnalisis(workspaceId) {
  try {
    console.log(`[API] Iniciando analisis Gemini para workspace ${workspaceId}...`);
    const data = await apiCall('POST', `/api/v1/analisis/workspaces/${workspaceId}/analizar`, {});
    const resultado = data?.data;
    console.log(`[API] Analisis iniciado: estado=${resultado?.Estado || resultado?.estado}`);
    return { success: true, error: null };
  } catch (e) {
    console.log(`[API] Error iniciando analisis: ${e.message}`);
    return { success: false, error: e.message };
  }
}

export async function pipelineAnalisisCompleto(licitacionId, nombreLicitacion, pdfPath, pdfFileName) {
  console.log(`\n[API] === Pipeline IA iniciado ===`);
  console.log(`[API] Licitacion: ${nombreLicitacion} (ID: ${licitacionId})`);
  console.log(`[API] PDF: ${pdfFileName}`);

  const { workspaceId, error: wsError } = await crearWorkspaceAnalisis(licitacionId, nombreLicitacion);
  if (!workspaceId) {
    console.log(`[API] Pipeline detenido: no se pudo crear workspace`);
    return { workspaceId: null, documentoId: null, error: wsError || 'Error creando workspace' };
  }

  const { documentoId, error: docError } = await subirDocumento(workspaceId, pdfPath, pdfFileName);
  if (!documentoId) {
    console.log(`[API] Pipeline detenido: no se pudo subir documento`);
    return { workspaceId, documentoId: null, error: docError || 'Error subiendo documento' };
  }

  const { success, error: analError } = await iniciarAnalisis(workspaceId);
  if (!success) {
    console.log(`[API] Pipeline completado parcialmente: analisis no iniciado`);
    return { workspaceId, documentoId, error: analError || 'Error iniciando analisis' };
  }

  console.log(`[API] === Pipeline IA completado ===`);
  return { workspaceId, documentoId, error: null };
}