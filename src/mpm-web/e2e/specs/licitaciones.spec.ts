import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { LicitacionesPage } from '../pages/LicitacionesPage';

test.describe('Licitaciones @regression', () => {
  test.beforeEach(async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
  });

  test('should display licitaciones list page @smoke', async ({ page }) => {
    const licitacionesPage = new LicitacionesPage(page);
    await licitacionesPage.waitForReady();
    await expect(licitacionesPage.title).toBeVisible();
    await expect(licitacionesPage.table).toBeVisible();
  });

  test('should display table with rows @smoke', async ({ page }) => {
    const licitacionesPage = new LicitacionesPage(page);
    await licitacionesPage.waitForReady();
    const rowCount = await licitacionesPage.getRowCount();
    expect(rowCount).toBeGreaterThan(0);
  });

  test('should open detail drawer on row click @critical', async ({ page }) => {
    const licitacionesPage = new LicitacionesPage(page);
    await licitacionesPage.waitForReady();
    await licitacionesPage.clickFirstRow();
    await expect(page.locator('.ant-drawer')).toBeVisible();
  });

  test('should show a single search field without smart search or sync button @smoke', async ({ page }) => {
    const licitacionesPage = new LicitacionesPage(page);
    await licitacionesPage.waitForReady();
    await expect(licitacionesPage.searchInput).toBeVisible();
    await expect(licitacionesPage.searchInput).toHaveCount(1);
    await expect(licitacionesPage.syncButton).toHaveCount(0);
    await expect(licitacionesPage.smartSearchButton).toHaveCount(0);
  });

  test('should reset all filters with reset button @critical', async ({ page }) => {
    const licitacionesPage = new LicitacionesPage(page);
    await licitacionesPage.waitForReady();

    await licitacionesPage.searchInput.fill('servicios');
    await expect(licitacionesPage.searchInput).toHaveValue('servicios');

    await licitacionesPage.resetFiltersButton.click();
    await expect(licitacionesPage.searchInput).toHaveValue('');
    await expect(licitacionesPage.table).toBeVisible();
  });

  test('should have pagination @regression', async ({ page }) => {
    const licitacionesPage = new LicitacionesPage(page);
    await licitacionesPage.waitForReady();
    await expect(licitacionesPage.pagination).toBeVisible();
  });

});

test.describe('Licitaciones API @regression', () => {
  test('GET /api/v1/licitaciones returns paginated results @smoke', async ({ request }) => {
    const response = await request.get('/api/v1/licitaciones?pageSize=5');
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.success).toBe(true);
    expect(body.data.items).toBeDefined();
    expect(body.data.page).toBe(1);
    expect(body.data.pageSize).toBe(5);
    expect(body.data.totalRecords).toBeGreaterThanOrEqual(0);
  });

  test('GET /api/v1/licitaciones/buscar requires min 3 chars @smoke', async ({ request }) => {
    const response = await request.get('/api/v1/licitaciones/buscar?q=LI');
    expect(response.status()).toBe(400);
  });

  test('GET /api/v1/licitaciones/buscar returns results with valid query @regression', async ({ request }) => {
    const response = await request.get('/api/v1/licitaciones/buscar?q=LIC&limit=5');
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.success).toBe(true);
    expect(Array.isArray(body.data)).toBe(true);
  });

  test('GET /api/v1/licitaciones/{codigo} returns 404 for non-existent @smoke', async ({ request }) => {
    const response = await request.get('/api/v1/licitaciones/NOEXISTE-999-XX99');
    expect(response.status()).toBe(404);
  });

  test('GET /health/licitaciones returns health info @smoke', async ({ request }) => {
    const response = await request.get('/health/licitaciones');
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.status).toBe('healthy');
    expect(body.module).toBe('licitaciones');
    expect(body.totalRecords).toBeGreaterThanOrEqual(0);
  });
});