import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';

test.describe('Sesión expirada @critical', () => {
  test('401 redirects to login with expired-session notice', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
    await expect(page).toHaveURL(/\/licitaciones/);

    // Corromper el token para forzar 401 en la siguiente llamada
    await page.evaluate(() => localStorage.setItem('mpm_token', 'invalid-token'));

    // Navegar a una página que consulta datos
    await page.goto('/notificaciones');

    // Debe redirigir a login con el aviso de sesión expirada
    await page.waitForURL(/\/login/, { timeout: 10000 });
    await expect(page.getByTestId('session-expired-alert')).toBeVisible();

    // La sesión quedó limpia
    const token = await page.evaluate(() => localStorage.getItem('mpm_token'));
    expect(token).toBeNull();
  });

  test('re-login after expiry works without residual errors', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');

    await page.evaluate(() => localStorage.setItem('mpm_token', 'invalid-token'));
    await page.goto('/licitaciones');
    await page.waitForURL(/\/login/, { timeout: 10000 });

    // Re-login
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
    await expect(page).toHaveURL(/\/licitaciones/);
    await expect(page.getByRole('heading', { name: 'Licitaciones' })).toBeVisible();
  });
});
