import { test, expect, request } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { LicitacionesPage } from '../pages/LicitacionesPage';
import { LicitacionInteresPanel } from '../pages/LicitacionInteresPanel';

const API_BASE = process.env.API_BASE || 'http://localhost:5001';
const TEST_USER = { email: 'admin@tivit.cl', password: 'test123' };

async function getAuthContext() {
  const anon = await request.newContext({ baseURL: API_BASE });
  const res = await anon.post('/api/v1/auth/login', { data: TEST_USER, headers: { 'Content-Type': 'application/json' } });
  const body = await res.json();
  await anon.dispose();
  return request.newContext({ baseURL: API_BASE, extraHTTPHeaders: { Authorization: `Bearer ${body.data.token}` } });
}

// spec 031 (US5): flujo colaborativo go/no-go -- marcar interés, análisis+conversación
// generados una sola vez (FR-013), comentar. Ya validado por API real contra Docker
// (ver research.md / tasks.md T025-T035); esto cubre el camino de UI.
test.describe('Licitación de interés (US5) @regression', () => {
  test.beforeEach(async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
  });

  test('marcar de interés genera el panel colaborativo y permite comentar @critical', async ({ page }) => {
    const licitacionesPage = new LicitacionesPage(page);
    await licitacionesPage.waitForReady();
    await licitacionesPage.clickFirstRow();

    const interesPanel = new LicitacionInteresPanel(page);
    await expect(interesPanel.marcarInteresButton.or(interesPanel.panel)).toBeVisible();

    // Si esta licitación ya fue marcada de interés en una corrida anterior del suite, el botón
    // no aparece -- el panel ya debería estar listo directamente (mismo comportamiento
    // idempotente que valida FR-013).
    if (await interesPanel.marcarInteresButton.isVisible().catch(() => false)) {
      await interesPanel.marcarInteres();
    }

    await interesPanel.esperarPanelListo();
    await expect(interesPanel.panel).toBeVisible();

    const textoComentario = `Comentario de prueba E2E ${Date.now()}`;
    await interesPanel.comentar(textoComentario);
    await expect(page.getByText(textoComentario)).toBeVisible({ timeout: 10000 });
  });

  test('marcar de interés dos veces no duplica el análisis (FR-013) @regression', async ({ page, request }) => {
    const licitacionesPage = new LicitacionesPage(page);
    await licitacionesPage.waitForReady();
    await licitacionesPage.clickFirstRow();

    const interesPanel = new LicitacionInteresPanel(page);
    if (await interesPanel.marcarInteresButton.isVisible().catch(() => false)) {
      await interesPanel.marcarInteres();
      await interesPanel.esperarPanelListo();
    }

    // recargar el drawer (cerrar y reabrir) y confirmar que sigue mostrando el mismo estado,
    // sin volver a ofrecer el botón "Marcar de interés"
    await page.keyboard.press('Escape');
    await licitacionesPage.clickFirstRow();
    await expect(interesPanel.marcarInteresButton).not.toBeVisible();
    await expect(interesPanel.panel.or(page.getByText(/generando/i))).toBeVisible();
  });
});

test.describe('Licitaciones de interés API (US5) @regression', () => {
  test('POST /interes requiere autenticación @smoke', async ({ request }) => {
    const res = await request.post(`${API_BASE}/api/v1/licitaciones/1/interes`);
    expect(res.status()).toBe(401);
  });

  test('POST /interes es idempotente -- misma fila, mismo id @smoke', async () => {
    const ctx = await getAuthContext();

    const listado = await ctx.get('/api/v1/licitaciones?pageSize=1');
    const listadoBody = await listado.json();
    const licitacionId = listadoBody.data.items[0].id;

    const primera = await ctx.post(`/api/v1/licitaciones/${licitacionId}/interes`);
    expect(primera.ok()).toBeTruthy();
    const primeraBody = await primera.json();

    const segunda = await ctx.post(`/api/v1/licitaciones/${licitacionId}/interes`);
    expect(segunda.ok()).toBeTruthy();
    const segundaBody = await segunda.json();

    expect(segundaBody.data.id).toBe(primeraBody.data.id);
    expect(segundaBody.data.createdAt).toBe(primeraBody.data.createdAt);

    await ctx.dispose();
  });
});
