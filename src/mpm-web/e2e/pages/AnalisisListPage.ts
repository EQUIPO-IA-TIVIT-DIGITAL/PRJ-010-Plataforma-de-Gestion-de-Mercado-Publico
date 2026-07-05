import { Page, Locator, expect } from '@playwright/test';

export class AnalisisListPage {
  readonly page: Page;
  readonly title: Locator;
  readonly newWorkspaceButton: Locator;
  readonly searchInput: Locator;
  readonly statusFilter: Locator;
  readonly workspaceCards: Locator;
  readonly modal: Locator;
  readonly nombreInput: Locator;
  readonly submitButton: Locator;
  readonly cancelButton: Locator;
  readonly emptyState: Locator;
  readonly pagination: Locator;

  constructor(page: Page) {
    this.page = page;
    this.title = page.getByRole('heading', { name: /análisis de licitaciones/i });
    this.newWorkspaceButton = page.locator('button').filter({ hasText: /nuevo workspace/i }).first();
    this.searchInput = page.getByPlaceholder(/buscar/i).first();
    this.statusFilter = page.locator('.ant-select').filter({ hasText: /estado/i }).first();
    this.workspaceCards = page.locator('.mpm-workspace-card');
    this.modal = page.locator('.ant-modal').filter({ hasText: /nuevo workspace/i });
    this.nombreInput = this.modal.locator('input').first();
    this.submitButton = this.modal.locator('button').filter({ hasText: /crear workspace/i });
    this.cancelButton = this.modal.locator('button').filter({ hasText: /cancelar/i });
    this.emptyState = page.locator('.ant-empty');
    this.pagination = page.locator('.ant-pagination');
  }

  async goto() {
    await this.page.goto('/analisis');
    await this.waitForReady();
  }

  async waitForReady() {
    await expect(this.title).toBeVisible({ timeout: 15000 });
  }

  async openNewWorkspaceModal() {
    await this.newWorkspaceButton.click();
    await expect(this.modal).toBeVisible();
  }

  async createWorkspace(nombre: string) {
    await this.openNewWorkspaceModal();
    await this.nombreInput.fill(nombre);
    await this.submitButton.click();
    await expect(this.modal).toBeHidden({ timeout: 10000 });
  }

  async getWorkspaceCardCount(): Promise<number> {
    return await this.workspaceCards.count();
  }

  async clickWorkspaceByName(nombre: string) {
    await this.workspaceCards.filter({ hasText: nombre }).first().click();
  }

  async searchByText(text: string) {
    await this.searchInput.fill(text);
    await this.page.waitForTimeout(500);
  }
}
