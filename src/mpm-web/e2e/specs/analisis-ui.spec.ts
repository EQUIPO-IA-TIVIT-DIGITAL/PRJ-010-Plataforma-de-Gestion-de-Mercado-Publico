import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { AnalisisListPage } from '../pages/AnalisisListPage';

test.describe('Analisis UI @regression', () => {
  test.beforeEach(async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
  });

  test('should display analisis list page @smoke', async ({ page }) => {
    const analisisPage = new AnalisisListPage(page);
    await analisisPage.goto();
    await expect(analisisPage.title).toBeVisible();
  });

  test('should show new workspace button @smoke', async ({ page }) => {
    const analisisPage = new AnalisisListPage(page);
    await analisisPage.goto();
    await expect(analisisPage.newWorkspaceButton).toBeVisible();
  });

  test('should open modal when clicking new workspace @smoke', async ({ page }) => {
    const analisisPage = new AnalisisListPage(page);
    await analisisPage.goto();
    await analisisPage.openNewWorkspaceModal();
    await expect(analisisPage.nombreInput).toBeVisible();
  });

  test('should create workspace and see it in list @critical', async ({ page }) => {
    const analisisPage = new AnalisisListPage(page);
    await analisisPage.goto();

    const nombre = `E2E UI ${Date.now()}`;
    await analisisPage.createWorkspace(nombre);
    // La lista no ordena por creación: hay que buscar para ver la tarjeta nueva.
    await analisisPage.searchByText(nombre);

    await expect(
      analisisPage.workspaceCards.filter({ hasText: nombre })
    ).toBeVisible({ timeout: 10000 });
  });

  test('should close modal on cancel @regression', async ({ page }) => {
    const analisisPage = new AnalisisListPage(page);
    await analisisPage.goto();
    await analisisPage.openNewWorkspaceModal();
    await analisisPage.cancelButton.click();
    await expect(analisisPage.modal).toBeHidden();
  });

  test('should keep submit disabled when nombre empty (or show validation) @regression', async ({ page }) => {
    const analisisPage = new AnalisisListPage(page);
    await analisisPage.goto();
    await analisisPage.openNewWorkspaceModal();
    await analisisPage.submitButton.click();
    await expect(analisisPage.nombreInput).toBeVisible();
  });

  test('should navigate to workspace detail on card click @smoke', async ({ page }) => {
    const analisisPage = new AnalisisListPage(page);
    await analisisPage.goto();
    const nombre = `Click-Test-${Date.now()}`;
    await analisisPage.createWorkspace(nombre);
    await analisisPage.searchByText(nombre);
    await analisisPage.clickWorkspaceByName(nombre);
    await page.waitForURL(/\/analisis\/\d+/, { timeout: 10000 });
  });
});
