import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';

// Centro de Administración — logs/auditoría (usp_Admin_ListarLogs, V132).
test.describe('Admin Logs @regression', () => {
  // El listado de usuarios pagina de a 20; busca por email para traer la fila a la vista.
  async function buscarUsuario(page: import('@playwright/test').Page, email: string) {
    await page.getByPlaceholder('Buscar por nombre o correo...').fill(email);
    await page.getByRole('button', { name: 'Buscar' }).click();
    await expect(page.getByRole('cell', { name: email })).toBeVisible();
  }

  test('super admin ve el resumen del sistema y las pestañas de logs', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');

    await page.goto('/admin/logs');
    await expect(page.getByRole('heading', { name: 'Logs y actividad del sistema' })).toBeVisible();
    await expect(page.getByText('Resumen del sistema')).toBeVisible();

    // El propio login ya generó al menos un evento de auth reciente.
    await expect(page.getByText(/Último inicio de sesión/)).toBeVisible();
    await expect(page.getByText(/admin@tivit\.cl/).first()).toBeVisible();
  });

  test('super admin navega las pestañas de logs', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');

    await page.goto('/admin/logs');

    for (const tab of ['Inicios de sesión', 'Sincronizaciones', 'Scraper', 'Extracción', 'Proveedor IA']) {
      await page.getByRole('tab', { name: new RegExp(tab) }).click();
      // El panel activo debe mostrar la tabla o el empty state (los paneles
      // inactivos quedan ocultos en el DOM y no deben matchear).
      await expect(page.locator('.ant-tabs-tabpane-active .ant-table, .ant-tabs-tabpane-active .ant-empty').first()).toBeVisible();
    }
  });

  test('admin con rol Admin también puede ver los logs', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');

    // Crear un Admin para probar su acceso a logs
    const adminEmail = `logs-admin-${Date.now()}@tivit.cl`;
    await page.goto('/admin/usuarios');
    await page.getByRole('button', { name: 'Nuevo usuario' }).click();
    const modal = page.locator('.ant-modal');
    await modal.getByPlaceholder('ej. María Pérez').fill('Logs Admin');
    await modal.getByPlaceholder('ej. maria.perez@tivit.cl').fill(adminEmail);
    await modal.getByRole('combobox').click();
    await page.getByRole('option', { name: /Administrador/ }).click();
    await modal.getByPlaceholder('Mínimo 6 caracteres').fill('demo12345');
    await modal.getByPlaceholder('Repite la contraseña').fill('demo12345');
    await page.getByRole('button', { name: 'Crear usuario' }).click();
    await expect(page.getByText(/creado correctamente/)).toBeVisible();
    await buscarUsuario(page, adminEmail);

    await page.evaluate(() => localStorage.clear());
    await page.goto('/login');
    await loginPage.loginAndWaitForRedirect(adminEmail, 'demo12345');
    await page.goto('/admin/logs');
    await expect(page.getByRole('heading', { name: 'Logs y actividad del sistema' })).toBeVisible();
  });
});
