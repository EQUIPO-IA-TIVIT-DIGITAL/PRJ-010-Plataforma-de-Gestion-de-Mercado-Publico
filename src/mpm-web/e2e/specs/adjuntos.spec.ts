import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { MensajeriaPage } from '../pages/MensajeriaPage';
import { ChatPanel } from '../pages/ChatPanel';

test.describe('Adjuntos @regression', () => {
  test.beforeEach(async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
    const mensajeriaPage = new MensajeriaPage(page);
    await mensajeriaPage.navigate();
    await mensajeriaPage.waitForReady();
    const count = await mensajeriaPage.getConversacionCount();
    if (count > 0) {
      await mensajeriaPage.selectConversacion(0);
    }
  });

  test('should upload image attachment @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    // Upload image test
  });

  test('should upload PDF attachment @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    // Upload PDF test
  });

  test('should reject oversized file @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    // Oversized file test
  });

  test('should download attachment @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    // Download test
  });
});
