import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';

// Centro de Administración — gestión de usuarios (módulo MPM.Modules.Administracion).
// Requiere stack completo (API + DB) y las migraciones V131+ aplicadas.
const SUFIXO = Date.now();

// El listado ordena por nombre y pagina de a 20 — tras varias corridas la fila
// nueva puede quedar en otra página. Se busca por email para traerla a la vista.
async function buscarUsuario(page: import('@playwright/test').Page, email: string) {
  await page.getByPlaceholder('Buscar por nombre o correo...').fill(email);
  await page.getByRole('button', { name: 'Buscar' }).click();
  await expect(page.getByRole('cell', { name: email })).toBeVisible();
}

test.describe('Admin Usuarios @regression', () => {
  test('super admin crea un usuario analista y lo ve en la tabla', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');

    await page.goto('/admin/usuarios');
    await expect(page.getByRole('heading', { name: 'Usuarios del sistema' })).toBeVisible();

    const email = `analista-demo-${SUFIXO}@tivit.cl`;
    await page.getByRole('button', { name: 'Nuevo usuario' }).click();

    const modal = page.locator('.ant-modal');
    await expect(modal).toBeVisible();
    await modal.getByPlaceholder('ej. María Pérez').fill('Analista Demo');
    await modal.getByPlaceholder('ej. maria.perez@tivit.cl').fill(email);
    await modal.getByRole('combobox').click();
    await page.getByRole('option', { name: /Analista/ }).click();
    await modal.getByPlaceholder('Mínimo 6 caracteres').fill('demo12345');
    await modal.getByPlaceholder('Repite la contraseña').fill('demo12345');
    await page.getByRole('button', { name: 'Crear usuario' }).click();

    await expect(page.getByText(/creado correctamente/)).toBeVisible();
    await buscarUsuario(page, email);
    await expect(page.getByRole('row', { name: new RegExp(email) }).getByText('Analista', { exact: true })).toBeVisible();
  });

  test('super admin desactiva un usuario y este deja de poder operar', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');

    // Crear usuario para desactivar
    await page.goto('/admin/usuarios');
    const email = `desactivar-demo-${SUFIXO}@tivit.cl`;
    await page.getByRole('button', { name: 'Nuevo usuario' }).click();
    const modal = page.locator('.ant-modal');
    await modal.getByPlaceholder('ej. María Pérez').fill('Usuario Desactivar');
    await modal.getByPlaceholder('ej. maria.perez@tivit.cl').fill(email);
    await modal.getByRole('combobox').click();
    await page.getByRole('option', { name: /Usuario/ }).first().click();
    await modal.getByPlaceholder('Mínimo 6 caracteres').fill('demo12345');
    await modal.getByPlaceholder('Repite la contraseña').fill('demo12345');
    await page.getByRole('button', { name: 'Crear usuario' }).click();
    await expect(page.getByText(/creado correctamente/)).toBeVisible();
    await buscarUsuario(page, email);

    // Desactivar
    const row = page.getByRole('row', { name: new RegExp(email) });
    await row.getByRole('button', { name: 'Desactivar' }).click();
    await page.locator('.ant-popconfirm-buttons').getByRole('button', { name: 'Desactivar' }).click();
    await expect(page.getByRole('row', { name: new RegExp(email) }).getByText('Desactivado')).toBeVisible();
  });

  test('usuario con rol Admin ve la sección y no puede crear super admins', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');

    // 1) SuperAdmin crea un Admin
    await page.goto('/admin/usuarios');
    const adminEmail = `admin-demo-${SUFIXO}@tivit.cl`;
    await page.getByRole('button', { name: 'Nuevo usuario' }).click();
    const modal = page.locator('.ant-modal');
    await modal.getByPlaceholder('ej. María Pérez').fill('Admin Demo');
    await modal.getByPlaceholder('ej. maria.perez@tivit.cl').fill(adminEmail);
    await modal.getByRole('combobox').click();
    await page.getByRole('option', { name: /Administrador/ }).click();
    await modal.getByPlaceholder('Mínimo 6 caracteres').fill('demo12345');
    await modal.getByPlaceholder('Repite la contraseña').fill('demo12345');
    await page.getByRole('button', { name: 'Crear usuario' }).click();
    await expect(page.getByText(/creado correctamente/)).toBeVisible();
    await buscarUsuario(page, adminEmail);

    // 2) Login como Admin: ve la sección de administración
    await page.evaluate(() => localStorage.clear());
    await page.goto('/login');
    await loginPage.loginAndWaitForRedirect(adminEmail, 'demo12345');
    await expect(page.getByText('Usuarios')).toBeVisible();
    await expect(page.getByText('Logs y actividad')).toBeVisible();

    // 3) El Admin no puede crear Admins/SuperAdmins (solo Analista/Usuario)
    await page.goto('/admin/usuarios');
    await page.getByRole('button', { name: 'Nuevo usuario' }).click();
    const modalAdmin = page.locator('.ant-modal');
    await modalAdmin.getByRole('combobox').click();
    await expect(page.getByRole('option', { name: /Super Admin/ })).toHaveCount(0);
    await expect(page.getByRole('option', { name: /Administrador/ })).toHaveCount(0);
    await expect(page.getByRole('option', { name: /Analista/ })).toBeVisible();
    await page.keyboard.press('Escape');
  });

  test('analista no ve la sección de administración', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('analista@tivit.cl', 'test123');

    await page.goto('/licitaciones');
    await expect(page.getByText('Usuarios')).toHaveCount(0);
    await expect(page.getByText('Logs y actividad')).toHaveCount(0);
    await expect(page.getByText('Admin IA')).toHaveCount(0);
  });
});
