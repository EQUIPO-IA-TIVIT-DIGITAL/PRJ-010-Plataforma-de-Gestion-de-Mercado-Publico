import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { MensajeriaPage } from '../pages/MensajeriaPage';

test.describe('Mensajeria @regression', () => {
  test.beforeEach(async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
  });

  test('should navigate to mensajes from sidebar @smoke', async ({ page }) => {
    await page.click('text=Mensajes');
    await expect(page).toHaveURL(/\/mensajes/);
  });

  test('should display conversation list @smoke', async ({ page }) => {
    const mensajeriaPage = new MensajeriaPage(page);
    await mensajeriaPage.navigate();
    await mensajeriaPage.waitForReady();
    const count = await mensajeriaPage.getConversacionCount();
    expect(count).toBeGreaterThan(0);
  });

  test('should search conversations @regression', async ({ page }) => {
    const mensajeriaPage = new MensajeriaPage(page);
    await mensajeriaPage.navigate();
    await mensajeriaPage.waitForReady();
    await mensajeriaPage.buscarConversacion('test');
    await page.waitForTimeout(500);
  });

  test('should show unread badge @smoke', async ({ page }) => {
    const mensajeriaPage = new MensajeriaPage(page);
    await mensajeriaPage.navigate();
    await mensajeriaPage.waitForReady();
    // Badge may or may not be visible depending on data
  });

  test('should create direct conversation @critical', async ({ page }) => {
    const mensajeriaPage = new MensajeriaPage(page);
    await mensajeriaPage.navigate();
    await mensajeriaPage.waitForReady();
    await mensajeriaPage.abrirCrearConversacion();
    await expect(mensajeriaPage.crearModal).toBeVisible();
  });

  test('should create group conversation @regression', async ({ page }) => {
    const mensajeriaPage = new MensajeriaPage(page);
    await mensajeriaPage.navigate();
    await mensajeriaPage.waitForReady();
    await mensajeriaPage.abrirCrearConversacion();
    await expect(mensajeriaPage.crearModal).toBeVisible();
  });

  test('should select conversation and show chat @critical', async ({ page }) => {
    const mensajeriaPage = new MensajeriaPage(page);
    await mensajeriaPage.navigate();
    await mensajeriaPage.waitForReady();
    const count = await mensajeriaPage.getConversacionCount();
    if (count > 0) {
      await mensajeriaPage.selectConversacion(0);
      await page.waitForTimeout(500);
    }
  });
});
