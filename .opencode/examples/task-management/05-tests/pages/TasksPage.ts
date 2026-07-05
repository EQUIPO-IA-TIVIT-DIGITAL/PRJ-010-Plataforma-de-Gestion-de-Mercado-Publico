import { Page, Locator } from '@playwright/test';

export class TasksPage {
  readonly page: Page;
  readonly pageTitle: Locator;
  readonly createButton: Locator;
  readonly tableRows: Locator;
  readonly searchInput: Locator;
  readonly nameInput: Locator;
  readonly descriptionInput: Locator;
  readonly prioritySelect: Locator;
  readonly assignedSelect: Locator;
  readonly submitButton: Locator;
  readonly cancelButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.pageTitle = page.getByTestId('page-title');
    this.createButton = page.getByTestId('btn-create');
    this.tableRows = page.getByTestId('data-table').locator('tbody tr');
    this.searchInput = page.getByTestId('input-search');
    this.nameInput = page.getByTestId('input-name');
    this.descriptionInput = page.getByTestId('input-description');
    this.prioritySelect = page.getByTestId('select-status');
    this.assignedSelect = page.getByTestId('select-assigned');
    this.submitButton = page.getByTestId('btn-submit');
    this.cancelButton = page.getByTestId('btn-cancel');
  }

  async navigate() {
    await this.page.goto('/tasks');
  }

  async createTask(data: { title: string; description?: string; priority?: string }) {
    await this.createButton.click();
    await this.nameInput.fill(data.title);
    if (data.description) {
      await this.descriptionInput.fill(data.description);
    }
    if (data.priority) {
      await this.prioritySelect.click();
      await this.page.getByRole('option', { name: data.priority }).click();
    }
    await this.submitButton.click();
  }

  async editFirstTask(update: { title?: string; description?: string }) {
    const editButton = this.tableRows.first().getByTestId('btn-edit');
    await editButton.click();
    if (update.title) {
      await this.nameInput.clear();
      await this.nameInput.fill(update.title);
    }
    if (update.description) {
      await this.descriptionInput.clear();
      await this.descriptionInput.fill(update.description);
    }
    await this.submitButton.click();
  }

  async deleteFirstTask() {
    const deleteButton = this.tableRows.first().getByTestId('btn-delete');
    await deleteButton.click();
    await this.page.getByText('OK').click();
  }

  async getRowCount(): Promise<number> {
    return this.tableRows.count();
  }

  async search(query: string) {
    await this.searchInput.fill(query);
    await this.searchInput.press('Enter');
  }
}
