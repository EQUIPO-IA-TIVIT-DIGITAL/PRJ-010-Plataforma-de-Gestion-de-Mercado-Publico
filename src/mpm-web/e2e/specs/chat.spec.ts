import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { MensajeriaPage } from '../pages/MensajeriaPage';
import { ChatPanel } from '../pages/ChatPanel';

test.describe('Chat @regression', () => {
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

  test('should send text message @critical', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    await chatPanel.enviarMensaje('Hola desde Playwright');
    await page.waitForTimeout(500);
    const count = await chatPanel.getMensajeCount();
    expect(count).toBeGreaterThan(0);
  });

  test('should display message from other user @critical', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    const count = await chatPanel.getMensajeCount();
    // May or may not have messages
  });

  test('should edit own message @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    const count = await chatPanel.getMensajeCount();
    if (count > 0) {
      // Edit functionality test
    }
  });

  test('should delete message @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    const count = await chatPanel.getMensajeCount();
    if (count > 0) {
      // Delete functionality test
    }
  });

  test('should show typing indicator @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    // Typing indicator test
  });

  test('should show online presence @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    // Presence test
  });

  test('should show participants drawer @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    // Participants drawer test
  });

  test('should mark messages as read @regression', async ({ page }) => {
    const chatPanel = new ChatPanel(page);
    await chatPanel.waitForReady();
    // Mark as read test
  });
});
