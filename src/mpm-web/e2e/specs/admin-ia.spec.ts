import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';

// 033-migracion-qwen-g4 (US4): switch del proveedor de IA (gcloud/qwen) — solo SuperAdmin.
// Requiere stack completo (API + DB) y el endpoint /api/system/ai-provider.
test.describe('Admin IA (switch proveedor) @regression', () => {
  test('super admin ve la sección y el estado actual', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');

    await page.goto('/admin/ia');
    await expect(page.getByRole('heading', { name: /Configuración del proveedor de IA/ })).toBeVisible();
    await expect(page.getByText('Proveedor activo')).toBeVisible();
    await expect(page.getByText(/gemini|openai/)).toBeVisible();
  });

  test('super admin cambia a qwen y vuelve a gcloud', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');

    await page.goto('/admin/ia');
    const switchBtn = page.getByRole('switch');
    await switchBtn.click();

    // Modal de confirmación: endpoint + modelo sugeridos
    const modal = page.locator('.ant-modal');
    await expect(modal).toBeVisible();
    const urlInput = page.getByPlaceholder(/URL del servidor Qwen/);
    await urlInput.fill('http://qwen.tivit.internal/v1');
    await page.getByRole('button', { name: 'Confirmar cambio' }).click();

    await expect(page.getByText(/Proveedor cambiado a Qwen/)).toBeVisible();

    // Volver a gcloud (restaurar estado)
    await switchBtn.click();
    await page.getByRole('button', { name: 'Confirmar cambio' }).click();
    await expect(page.getByText(/Proveedor cambiado a Google/)).toBeVisible();
  });

  test('usuario sin rol SuperAdmin no ve el item de menú', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('analista@tivit.cl', 'test123');

    await page.goto('/licitaciones');
    await expect(page.getByText('Admin IA')).toHaveCount(0);
  });
});
