import { Page, Locator } from '@playwright/test';

export class LicitacionesPage {
  readonly page: Page;
  readonly title: Locator;
  readonly table: Locator;
  readonly rows: Locator;
  readonly syncButton: Locator;
  readonly smartSearchButton: Locator;
  readonly searchInput: Locator;
  readonly resetFiltersButton: Locator;
  readonly pagination: Locator;

  constructor(page: Page) {
    this.page = page;
    this.title = page.getByRole('heading', { name: 'Licitaciones' });
    this.table = page.getByTestId('licitaciones-table');
    this.rows = page.locator('.ant-table-tbody tr');
    this.syncButton = page.locator('button').filter({ hasText: /sincronizar/i });
    this.smartSearchButton = page.locator('button').filter({ hasText: /búsqueda inteligente/i });
    this.searchInput = page.getByTestId('filter-busqueda');
    this.resetFiltersButton = page.getByTestId('filter-reset');
    this.pagination = page.locator('.ant-pagination');
  }

  async waitForReady() {
    await this.page.waitForLoadState('networkidle');
    await this.table.waitFor({ state: 'visible', timeout: 15000 });
  }

  async getRowCount(): Promise<number> {
    return await this.rows.count();
  }

  async clickFirstRow() {
    await this.rows.first().click();
    await this.page.waitForSelector('.ant-drawer', { timeout: 5000 });
  }
}
