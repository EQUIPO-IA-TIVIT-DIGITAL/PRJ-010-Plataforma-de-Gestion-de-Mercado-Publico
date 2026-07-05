import { Page, Locator } from '@playwright/test';

export class ChatPanel {
  readonly page: Page;
  readonly chatHeader: Locator;
  readonly chatHeaderTitle: Locator;
  readonly participantesBtn: Locator;
  readonly mensajeList: Locator;
  readonly mensajeBubbles: Locator;
  readonly mensajeInput: Locator;
  readonly enviarBtn: Locator;
  readonly adjuntarBtn: Locator;
  readonly typingIndicator: Locator;
  readonly presenciaBadge: Locator;

  constructor(page: Page) {
    this.page = page;
    this.chatHeader = page.getByTestId('chat-header');
    this.chatHeaderTitle = page.getByTestId('chat-header-title');
    this.participantesBtn = page.getByTestId('btn-participantes');
    this.mensajeList = page.getByTestId('mensaje-list');
    this.mensajeBubbles = page.getByTestId('mensaje-bubble');
    this.mensajeInput = page.getByTestId('mensaje-input');
    this.enviarBtn = page.getByTestId('btn-enviar');
    this.adjuntarBtn = page.getByTestId('btn-adjuntar');
    this.typingIndicator = page.getByTestId('typing-indicator');
    this.presenciaBadge = page.getByTestId('presencia-badge');
  }

  async waitForReady() {
    await this.page.waitForLoadState('networkidle');
  }

  async enviarMensaje(texto: string) {
    await this.mensajeInput.fill(texto);
    await this.enviarBtn.click();
  }

  async getMensajeCount(): Promise<number> {
    return await this.mensajeBubbles.count();
  }

  async getUltimoMensaje(): Promise<string> {
    const last = this.mensajeBubbles.last();
    return await last.textContent() || '';
  }

  async editarMensaje(index: number, nuevoTexto: string) {
    const bubble = this.mensajeBubbles.nth(index);
    await bubble.hover();
    await bubble.locator('.ant-dropdown-trigger').click();
    await this.page.getByText('Editar').click();
    await this.mensajeInput.fill(nuevoTexto);
    await this.enviarBtn.click();
  }

  async eliminarMensaje(index: number) {
    const bubble = this.mensajeBubbles.nth(index);
    await bubble.hover();
    await bubble.locator('.ant-dropdown-trigger').click();
    await this.page.getByText('Eliminar').click();
    await this.page.getByRole('button', { name: /aceptar|confirmar/i }).click();
  }

  async adjuntarArchivo(ruta: string) {
    await this.adjuntarBtn.click();
    await this.page.setInputFiles('input[type="file"]', ruta);
  }

  async isTypingVisible(): Promise<boolean> {
    return await this.typingIndicator.isVisible();
  }

  async getPresenciaEstado(): Promise<string> {
    const badge = this.presenciaBadge;
    const status = await badge.getAttribute('data-status');
    return status || 'offline';
  }
}
