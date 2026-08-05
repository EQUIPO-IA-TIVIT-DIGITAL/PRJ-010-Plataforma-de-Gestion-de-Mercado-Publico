import { Page, Locator } from '@playwright/test';

// spec 031 (US5): panel de "marcar de interés" dentro del drawer de detalle de licitación
export class LicitacionInteresPanel {
  readonly page: Page;
  readonly marcarInteresButton: Locator;
  readonly panel: Locator;
  readonly comentarioInput: Locator;
  readonly alertaEstadoCambio: Locator;

  constructor(page: Page) {
    this.page = page;
    this.marcarInteresButton = page.getByTestId('btn-marcar-interes');
    this.panel = page.getByTestId('panel-interes');
    this.comentarioInput = page.getByTestId('input-comentario-interes');
    this.alertaEstadoCambio = page.getByTestId('alerta-estado-cambio');
  }

  async marcarInteres() {
    await this.marcarInteresButton.click();
  }

  async esperarPanelListo(timeout = 60000) {
    // el análisis + la conversación se crean en background (workspace+conversación) -- puede
    // tardar unos segundos, no es instantáneo (ver contracts/colaboracion-interes.md)
    await this.comentarioInput.waitFor({ state: 'visible', timeout });
  }

  async comentar(texto: string) {
    await this.comentarioInput.fill(texto);
    await this.comentarioInput.press('Enter');
  }
}
