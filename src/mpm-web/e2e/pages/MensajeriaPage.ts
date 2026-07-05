import { Page, Locator } from '@playwright/test';

export class MensajeriaPage {
  readonly page: Page;
  readonly conversacionList: Locator;
  readonly conversacionItems: Locator;
  readonly buscarInput: Locator;
  readonly crearConversacionBtn: Locator;
  readonly crearModal: Locator;
  readonly noLeidosBadges: Locator;

  constructor(page: Page) {
    this.page = page;
    this.conversacionList = page.getByTestId('conversacion-list');
    this.conversacionItems = page.getByTestId('conversacion-item');
    this.buscarInput = page.getByTestId('conversacion-search');
    this.crearConversacionBtn = page.locator('button').filter({ hasText: /nueva/i }).first();
    this.crearModal = page.locator('.ant-modal');
    this.noLeidosBadges = page.locator('.ant-badge-count');
  }

  async navigate() {
    await this.page.goto('/mensajes');
    await this.page.waitForLoadState('networkidle');
  }

  async waitForReady() {
    await this.page.waitForLoadState('networkidle');
  }

  async getConversacionCount(): Promise<number> {
    return await this.conversacionItems.count();
  }

  async selectConversacion(index: number) {
    await this.conversacionItems.nth(index).click();
  }

  async buscarConversacion(texto: string) {
    await this.buscarInput.fill(texto);
    await this.buscarInput.press('Enter');
  }

  async abrirCrearConversacion() {
    await this.crearConversacionBtn.click();
    await this.crearModal.waitFor({ state: 'visible' });
  }

  async getNoLeidosCount(index: number): Promise<number> {
    const badge = this.noLeidosBadges.nth(index);
    const text = await badge.textContent();
    return text ? parseInt(text, 10) : 0;
  }
}
