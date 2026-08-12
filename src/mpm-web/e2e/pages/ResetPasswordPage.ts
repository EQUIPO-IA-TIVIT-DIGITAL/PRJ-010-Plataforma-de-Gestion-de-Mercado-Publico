import { Page, Locator } from '@playwright/test';

export class ResetPasswordPage {
  readonly page: Page;
  readonly passwordInput: Locator;
  readonly confirmPasswordInput: Locator;
  readonly submitButton: Locator;
  readonly backToLoginLink: Locator;
  readonly successMessage: Locator;
  readonly errorMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.passwordInput = page.getByTestId('reset-password');
    this.confirmPasswordInput = page.getByTestId('reset-confirm-password');
    this.submitButton = page.getByTestId('reset-submit');
    this.backToLoginLink = page.getByRole('button', { name: 'Ir al login' });
    this.successMessage = page.getByText('¡Contraseña restablecida!');
    // La pantalla de token inválido no usa <Result> de AntD (rediseño 019): muestra
    // el aviso en un Typography.Text dentro del AuthCard.
    this.errorMessage = page.getByText(/expirado o ya fue utilizado/);
  }

  async goto(token: string) {
    await this.page.goto(`/reset-password/${token}`);
  }

  async resetPassword(password: string, confirmPassword?: string) {
    await this.passwordInput.fill(password);
    await this.confirmPasswordInput.fill(confirmPassword ?? password);
    await this.submitButton.click();
  }

  async clickBackToLogin() {
    await this.backToLoginLink.click();
  }
}
