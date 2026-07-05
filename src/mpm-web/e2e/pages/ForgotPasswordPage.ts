import { Page, Locator } from '@playwright/test';

export class ForgotPasswordPage {
  readonly page: Page;
  readonly emailInput: Locator;
  readonly submitButton: Locator;
  readonly backToLoginLink: Locator;
  readonly successMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.emailInput = page.getByTestId('forgot-email');
    this.submitButton = page.getByTestId('forgot-submit');
    this.backToLoginLink = page.getByRole('button', { name: 'Volver al login' });
    this.successMessage = page.getByText('Correo enviado');
  }

  async goto() {
    await this.page.goto('/forgot-password');
    await this.page.waitForSelector('[data-testid="forgot-email"]');
  }

  async submitEmail(email: string) {
    await this.emailInput.fill(email);
    await this.submitButton.click();
  }

  async clickBackToLogin() {
    await this.backToLoginLink.click();
  }
}
