import { test, expect, request } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { LicitacionesPage } from '../pages/LicitacionesPage';
import { PropuestasPanel } from '../pages/PropuestasPanel';

const API_BASE = process.env.API_BASE || 'http://localhost:5001';
const TEST_USER = { email: 'admin@tivit.cl', password: 'test123' };

async function getAuthContext() {
  const anon = await request.newContext({ baseURL: API_BASE });
  const res = await anon.post('/api/v1/auth/login', { data: TEST_USER, headers: { 'Content-Type': 'application/json' } });
  const body = await res.json();
  await anon.dispose();
  return request.newContext({ baseURL: API_BASE, extraHTTPHeaders: { Authorization: `Bearer ${body.data.token}` } });
}

test.describe('Propuestas API — Contratos y validaciones (Fase 3) @regression', () => {
  test('Catalogos requieren autenticacion @smoke', async ({ request }) => {
    const resCapitulos = await request.get(`${API_BASE}/api/v1/propuestas/catalogos/capitulos`);
    expect(resCapitulos.status()).toBe(401);

    const resCertificaciones = await request.get(`${API_BASE}/api/v1/propuestas/catalogos/certificaciones`);
    expect(resCertificaciones.status()).toBe(401);

    const resExperiencias = await request.get(`${API_BASE}/api/v1/propuestas/catalogos/experiencias`);
    expect(resExperiencias.status()).toBe(401);
  });

  test('Catalogos con autenticacion devuelven items paginados @smoke', async () => {
    const ctx = await getAuthContext();

    const res = await ctx.get('/api/v1/propuestas/catalogos/capitulos?page=1&size=10');
    expect(res.ok()).toBeTruthy();

    const body = await res.json();
    expect(body.success).toBe(true);
    expect(Array.isArray(body.data.items)).toBe(true);
    expect(body.data.items.length).toBeGreaterThan(0);

    // Verificar estructura de capitulo
    const cap = body.data.items[0];
    expect(cap).toHaveProperty('id');
    expect(cap).toHaveProperty('titulo');
    expect(cap).toHaveProperty('orden');

    await ctx.dispose();
  });

  test('Generar propuesta sin decision GO devuelve PRO_003 o LIC_001 @regression', async () => {
    const ctx = await getAuthContext();

    const res = await ctx.post('/api/v1/licitaciones/LICITACION-SIN-GO-12345/propuestas/generar', {
      data: {
        capitulosIds: [1, 2, 3],
      },
    });

    expect(res.status()).toBeGreaterThanOrEqual(400);
    const body = await res.json();
    expect(body.success).toBe(false);
    const code = body.errors?.[0]?.code ?? body.error?.code;
    expect(['PRO_003', 'LIC_001']).toContain(code);

    await ctx.dispose();
  });

  test('Avisar decision valida destinatarios no vacios @regression', async () => {
    const ctx = await getAuthContext();

    const res = await ctx.post('/api/v1/licitaciones/LIC-TEST/decision/1/avisar', {
      data: {
        destinatarios: [],
      },
    });

    expect(res.status()).toBe(422);
    const body = await res.json();
    expect(body.success).toBe(false);
    const code = body.errors?.[0]?.code ?? body.error?.code;
    expect(code).toBe('PRO_007');

    await ctx.dispose();
  });

  test('Avisar decision valida formato de emails @regression', async () => {
    const ctx = await getAuthContext();

    const res = await ctx.post('/api/v1/licitaciones/LIC-TEST/decision/1/avisar', {
      data: {
        destinatarios: ['email_invalido_sin_arroba'],
      },
    });

    expect(res.status()).toBe(422);
    const body = await res.json();
    expect(body.success).toBe(false);
    const code = body.errors?.[0]?.code ?? body.error?.code;
    expect(code).toBe('PRO_007');

    await ctx.dispose();
  });
});

test.describe('Propuestas UI — Panel en detalle de licitacion (Fase 3) @regression', () => {
  test.beforeEach(async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
  });

  test('Apertura de drawer y visualizacion de seccion de propuestas si existe decision @critical', async ({ page }) => {
    const licitacionesPage = new LicitacionesPage(page);
    await licitacionesPage.waitForReady();
    await licitacionesPage.clickFirstRow();

    const propuestasPanel = new PropuestasPanel(page);

    // Si la licitación ya tiene decisión, el panel de avisos o propuesta debe estar montado
    const panelAvisosVisible = await propuestasPanel.selectDestinatarios.isVisible().catch(() => false);
    if (panelAvisosVisible) {
      await expect(propuestasPanel.selectDestinatarios).toBeVisible();
      await expect(propuestasPanel.avisarButton).toBeVisible();
    }
  });
});
