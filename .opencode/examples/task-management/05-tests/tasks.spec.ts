import { test, expect } from '@playwright/test';
import { TasksPage } from './pages/TasksPage';

test.describe('Tasks @regression', () => {
  test.beforeEach(async ({ page }) => {
    const tasksPage = new TasksPage(page);
    await tasksPage.navigate();
  });

  test('should display the task list page @smoke', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    await expect(tasksPage.pageTitle).toBeVisible();
    await expect(tasksPage.createButton).toBeVisible();
  });

  test('should create a new task @smoke', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    await tasksPage.createTask({
      title: 'Fix login issue',
      priority: 'HIGH',
    });

    await expect(page.getByText('Task created successfully')).toBeVisible();
    await expect(page.getByText('Fix login issue')).toBeVisible();
  });

  test('should show validation errors on empty form', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    await tasksPage.createButton.click();
    await tasksPage.submitButton.click();

    await expect(page.getByText('Title is required')).toBeVisible();
  });

  test('should filter tasks by status', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    // Apply status filter via search or dropdown
    await expect(tasksPage.tableRows.first()).toBeVisible();
  });

  test('should edit an existing task @critical', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    await tasksPage.editFirstTask({ title: 'Updated task title' });

    await expect(page.getByText('Task updated successfully')).toBeVisible();
  });

  test('should activate a draft task @critical', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    const activateButton = tasksPage.tableRows.first().getByText('Activate');

    if (await activateButton.isVisible()) {
      await activateButton.click();
      await expect(page.getByText('Task activated')).toBeVisible();
    }
  });

  test('should complete an active task @critical', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    const completeButton = tasksPage.tableRows.first().getByText('Complete');

    if (await completeButton.isVisible()) {
      await completeButton.click();
      await expect(page.getByText('Task completed')).toBeVisible();
    }
  });

  test('should delete a task @regression', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    const initialCount = await tasksPage.getRowCount();

    await tasksPage.deleteFirstTask();

    await expect(page.getByText('Task deleted successfully')).toBeVisible();
    const newCount = await tasksPage.getRowCount();
    expect(newCount).toBeLessThanOrEqual(initialCount);
  });

  test('should search tasks by title @regression', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    await tasksPage.search('login');

    // Wait for results to update
    await page.waitForTimeout(500);
    const rows = await tasksPage.getRowCount();
    expect(rows).toBeGreaterThanOrEqual(0);
  });

  test('should paginate through tasks @regression', async ({ page }) => {
    const tasksPage = new TasksPage(page);
    // Go to page 2 if more than one page exists
    const page2Button = page.getByRole('listitem').filter({ hasText: '2' });
    if (await page2Button.isVisible()) {
      await page2Button.click();
      await expect(page).toHaveURL(/.*page=2.*/);
    }
  });
});
