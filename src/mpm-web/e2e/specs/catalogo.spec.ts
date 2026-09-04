import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';

test.describe('Catálogo @regression', () => {
  test.beforeEach(async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
  });

  test('GET /api/v1/catalogos returns all catalogs @smoke', async ({ request }) => {
    const response = await request.get('/api/v1/catalogos');
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.success).toBe(true);
    expect(body.data).toHaveProperty('estadosLicitacion');
    expect(body.data).toHaveProperty('tiposLicitacion');
    expect(body.data).toHaveProperty('monedas');
    expect(Array.isArray(body.data.estadosLicitacion)).toBe(true);
    expect(Array.isArray(body.data.tiposLicitacion)).toBe(true);
    expect(Array.isArray(body.data.monedas)).toBe(true);
  });

  test('GET /api/v1/catalogos/estados-licitacion returns estados @smoke', async ({ request }) => {
    const response = await request.get('/api/v1/catalogos/estados-licitacion');
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.success).toBe(true);
    expect(Array.isArray(body.data)).toBe(true);
    // V108 (spec 027): el catálogo público expone solo los 5 códigos vigentes del
    // portal (5,6,7,8,15); los códigos legacy 1-4 no se usan y quedan excluidos.
    expect(body.data.length).toBeGreaterThanOrEqual(5);
  });

  test('GET /api/v1/catalogos/tipos-licitacion returns tipos @smoke', async ({ request }) => {
    const response = await request.get('/api/v1/catalogos/tipos-licitacion');
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.success).toBe(true);
    expect(Array.isArray(body.data)).toBe(true);
    expect(body.data.length).toBeGreaterThanOrEqual(4);

    // Verify slug field exists
    for (const item of body.data) {
      expect(item).toHaveProperty('codigo');
      expect(item).toHaveProperty('nombre');
      expect(item).toHaveProperty('slug');
      expect(typeof item.slug).toBe('string');
    }
  });

  test('GET /api/v1/catalogos/monedas returns monedas @smoke', async ({ request }) => {
    const response = await request.get('/api/v1/catalogos/monedas');
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.success).toBe(true);
    expect(Array.isArray(body.data)).toBe(true);
    expect(body.data.length).toBeGreaterThanOrEqual(3);

    // Verify ISO code exists
    for (const item of body.data) {
      expect(item).toHaveProperty('codigo');
      expect(item).toHaveProperty('nombre');
      expect(item).toHaveProperty('simbolo');
      expect(item).toHaveProperty('codigoIso');
      expect(typeof item.codigoIso).toBe('string');
    }
  });

  test('estados-licitacion contains expected entries @critical', async ({ request }) => {
    const response = await request.get('/api/v1/catalogos/estados-licitacion');
    const body = await response.json();
    const estados = body.data as { codigo: number; nombre: string }[];

    // Estados vigentes confirmados en V086/V108; "Modificada" (código legacy 2) no
    // forma parte del catálogo público actual.
    const expected = ['Publicada', 'Cerrada', 'Desierta', 'Adjudicada', 'Revocada'];
    for (const nombre of expected) {
      expect(estados.some(e => e.nombre === nombre)).toBeTruthy();
    }
  });

  test('tipos-licitacion contains expected slugs @critical', async ({ request }) => {
    const response = await request.get('/api/v1/catalogos/tipos-licitacion');
    const body = await response.json();
    const tipos = body.data as { codigo: number; nombre: string; slug: string }[];

    // V108 repobló tipos_licitacion con los códigos reales del portal y slugs en
    // minúscula. El tipo genérico "Licitación" ya no existe: se descompuso en
    // LE/LP/LQ/LR/LS — usamos LP como representante de licitación pública.
    const expectedSlugs = ['lp', 'td', 'co', 'ca'];
    for (const slug of expectedSlugs) {
      expect(tipos.some(t => t.slug === slug)).toBeTruthy();
    }
  });

  test('monedas contain CLP, USD, EUR @critical', async ({ request }) => {
    const response = await request.get('/api/v1/catalogos/monedas');
    const body = await response.json();
    const monedas = body.data as { codigo: number; nombre: string; codigoIso: string }[];

    const expectedIso = ['CLP', 'USD', 'EUR'];
    for (const iso of expectedIso) {
      expect(monedas.some(m => m.codigoIso === iso)).toBeTruthy();
    }
  });

  test.skip('Catálogo page renders with tabs @regression', async ({ page }) => {
    await page.goto('/catalogos');
    await page.waitForLoadState('networkidle');

    // Verify page title
    await expect(page.getByRole('heading', { name: 'Catálogos' })).toBeVisible();

    // Verify tabs are present
    await expect(page.getByText('Estados de Licitación')).toBeVisible();
    await expect(page.getByText('Tipos de Licitación')).toBeVisible();
    await expect(page.getByText('Monedas')).toBeVisible();
  });

  test.skip('Catálogo page shows estados table with data @regression', async ({ page }) => {
    await page.goto('/catalogos');
    await page.waitForLoadState('networkidle');

    // Verify the estados table has data
    const table = page.getByTestId('catalogo-estados-table');
    await expect(table).toBeVisible();

    // Verify at least one estado is visible
    await expect(page.getByText('Publicada')).toBeVisible();
  });

  test.skip('Catálogo page switches between tabs @regression', async ({ page }) => {
    await page.goto('/catalogos');
    await page.waitForLoadState('networkidle');

    // Click on Monedas tab
    await page.getByRole('tab', { name: /Monedas/ }).click();
    await expect(page.getByTestId('catalogo-monedas-table')).toBeVisible();

    // Click on Tipos tab
    await page.getByRole('tab', { name: /Tipos/ }).click();
    await expect(page.getByTestId('catalogo-tipos-table')).toBeVisible();
  });

  test.skip('estados are used in licitaciones filter with dynamic data @regression', async ({ page, request }) => {
    // Get estados from API
    const response = await request.get('/api/v1/catalogos/estados-licitacion');
    const body = await response.json();

    // Navigate to licitaciones page and verify filter
    await page.goto('/licitaciones');
    await page.waitForLoadState('networkidle');

    const estadoFilter = page.locator('[data-testid="filter-estado"]');
    await expect(estadoFilter).toBeVisible();

    // Click the filter to see options
    await estadoFilter.click();
    // Should show dynamic options from the catalog
    await expect(page.getByText('Publicada')).toBeVisible();
  });

  test.skip('navigation to catálogos page works from sidebar @regression', async ({ page }) => {
    // Click on Catálogos in sidebar
    await page.getByRole('menuitem', { name: 'Catálogos' }).click();
    await page.waitForURL(/\/catalogos/);
    await expect(page.getByRole('heading', { name: 'Catálogos' })).toBeVisible();
  });
});