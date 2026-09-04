import { Page, Locator } from '@playwright/test';

export class PropuestasPanel {
  readonly page: Page;
  readonly selectDestinatarios: Locator;
  readonly avisarButton: Locator;
  readonly obtenerRecomendacionesButton: Locator;
  readonly selectCertificaciones: Locator;
  readonly selectExperiencias: Locator;
  readonly generarPropuestaButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.selectDestinatarios = page.getByTestId('select-destinatarios-decision');
    this.avisarButton = page.getByTestId('btn-avisar-decision');
    this.obtenerRecomendacionesButton = page.getByTestId('btn-obtener-recomendaciones');
    this.selectCertificaciones = page.getByTestId('select-certificaciones-propuesta');
    this.selectExperiencias = page.getByTestId('select-experiencias-propuesta');
    this.generarPropuestaButton = page.getByTestId('btn-generar-propuesta');
  }

  async pedirRecomendaciones() {
    await this.obtenerRecomendacionesButton.click();
  }

  async generarPropuesta() {
    await this.generarPropuestaButton.click();
  }

  async avisar() {
    await this.avisarButton.click();
  }
}
