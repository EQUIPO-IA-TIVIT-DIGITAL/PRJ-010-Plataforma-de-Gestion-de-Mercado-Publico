import { Page, Locator } from '@playwright/test';

export class LoginPage {
  readonly page: Page;
  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;
  readonly errorMessage: Locator;
  readonly rememberCheckbox: Locator;
  readonly forgotPasswordLink: Locator;

  constructor(page: Page) {
    this.page = page;
    this.emailInput = page.getByTestId('login-email');
    this.passwordInput = page.getByTestId('login-password');
    this.submitButton = page.getByTestId('login-submit');
    this.errorMessage = page.locator('.ant-message-error');
    this.rememberCheckbox = page.getByRole('checkbox', { name: 'Recordarme' });
    this.forgotPasswordLink = page.locator('a.ant-typography').filter({ hasText: '¿Olvidaste tu contraseña?' });
  }

  async goto() {
    await this.page.goto('/login');
    await this.page.waitForSelector('[data-testid="login-email"]');
  }

  async login(email: string, password: string) {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.submitButton.click();
  }

  async loginWithRemember(email: string, password: string) {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(password);
    await this.rememberCheckbox.check();
    await this.submitButton.click();
  }

  async loginAndWaitForRedirect(email: string, password: string) {
    await this.login(email, password);
    await this.page.waitForURL(/\/licitaciones/, { timeout: 10000 });
    await this.page.waitForLoadState('networkidle');
  }

  async clickForgotPassword() {
    await this.forgotPasswordLink.click();
  }
}
