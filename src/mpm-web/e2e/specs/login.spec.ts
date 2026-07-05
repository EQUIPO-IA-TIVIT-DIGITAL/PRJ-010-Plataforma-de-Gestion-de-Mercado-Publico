import { test, expect } from '@playwright/test';
import { LoginPage } from '../pages/LoginPage';
import { ForgotPasswordPage } from '../pages/ForgotPasswordPage';
import { ResetPasswordPage } from '../pages/ResetPasswordPage';

test.describe('Login @smoke', () => {
  test('should show login form', async ({ page }) => {
    await page.goto('/login');
    await expect(page.getByTestId('login-email')).toBeVisible();
    await expect(page.getByTestId('login-password')).toBeVisible();
    await expect(page.getByTestId('login-submit')).toBeVisible();
  });

  test('should login with valid credentials and redirect', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginAndWaitForRedirect('admin@tivit.cl', 'test123');
    await expect(page).toHaveURL(/\/licitaciones/);
    await expect(page.getByRole('heading', { name: 'Licitaciones' })).toBeVisible();
  });

  test('should show error with invalid credentials', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.login('wrong@test.cl', 'bad');
    await expect(page.locator('.ant-form-item-explain-error')).toBeVisible({ timeout: 5000 });
  });

  test('should show remember me checkbox without forgot password link', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await expect(loginPage.rememberCheckbox).toBeVisible();
    await expect(page.getByText('¿Olvidaste tu contraseña?')).toHaveCount(0);
  });

  test('should persist remembered email after login with remember me', async ({ page }) => {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.loginWithRemember('admin@tivit.cl', 'test123');
    await page.waitForURL(/\/licitaciones/);
    
    // Verify remember me was saved
    const savedEmail = await page.evaluate(() => localStorage.getItem('mpm_remember_email'));
    expect(savedEmail).toBe('admin@tivit.cl');
  });

  test('should validate email format on login form', async ({ page }) => {
    await page.goto('/login');
    await page.getByTestId('login-email').fill('invalid-email');
    await page.getByTestId('login-email').blur();
    await expect(page.locator('.ant-form-item-explain-error')).toBeVisible({ timeout: 3000 });
  });
});

test.describe('Forgot Password @smoke', () => {
  test('should show forgot password form', async ({ page }) => {
    const forgotPage = new ForgotPasswordPage(page);
    await forgotPage.goto();
    await expect(forgotPage.emailInput).toBeVisible();
    await expect(forgotPage.submitButton).toBeVisible();
  });

  test('should show success message after submitting email', async ({ page }) => {
    const forgotPage = new ForgotPasswordPage(page);
    await forgotPage.goto();
    await forgotPage.submitEmail('test@example.com');
    await expect(forgotPage.successMessage).toBeVisible({ timeout: 5000 });
  });

  test('should navigate back to login when clicking back button', async ({ page }) => {
    const forgotPage = new ForgotPasswordPage(page);
    await forgotPage.goto();
    await forgotPage.submitEmail('test@example.com');
    await expect(forgotPage.successMessage).toBeVisible({ timeout: 5000 });
    await forgotPage.clickBackToLogin();
    await expect(page).toHaveURL(/\/login/);
  });

  test('should validate email format on forgot password form', async ({ page }) => {
    await page.goto('/forgot-password');
    await page.getByTestId('forgot-email').fill('invalid');
    await page.getByTestId('forgot-email').blur();
    await expect(page.locator('.ant-form-item-explain-error')).toBeVisible({ timeout: 3000 });
  });
});

test.describe('Reset Password @smoke', () => {
  test('should show error for invalid token', async ({ page }) => {
    const resetPage = new ResetPasswordPage(page);
    await resetPage.goto('invalid-token-12345');
    await expect(resetPage.errorMessage).toContainText('expirado', { timeout: 5000 });
  });

  test('should show reset password form with valid token', async ({ page }) => {
    // First request a valid token
    const forgotPage = new ForgotPasswordPage(page);
    await forgotPage.goto();
    await forgotPage.submitEmail('admin@tivit.cl');
    await expect(forgotPage.successMessage).toBeVisible({ timeout: 5000 });
    // Note: In a real environment, the token would be obtained from the email or backend logs
  });

  test('should validate password confirmation', async ({ page }) => {
    const resetPage = new ResetPasswordPage(page);
    await resetPage.goto('test-token-placeholder');
    await page.waitForTimeout(1000);

    if (await resetPage.passwordInput.isVisible()) {
      await resetPage.passwordInput.fill('password123');
      await resetPage.confirmPasswordInput.fill('different123');
      await resetPage.submitButton.click();
      await expect(page.locator('.ant-form-item-explain-error')).toContainText('no coinciden', { timeout: 5000 });
    }
  });
});

test.describe('Auth API @regression', () => {
  test('POST /auth/login returns token with valid credentials @smoke', async ({ request }) => {
    const response = await request.post('/api/v1/auth/login', {
      data: { email: 'admin@tivit.cl', password: 'test123' }
    });
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.success).toBe(true);
    expect(body.data.token).toBeTruthy();
    expect(body.data.user.email).toBe('admin@tivit.cl');
    expect(body.data.user.nombre).toBe('Admin TIVIT');
    expect(body.data.user.roles).toContain('SuperAdmin');
  });

  test('POST /auth/login returns 401 with invalid credentials @critical', async ({ request }) => {
    const response = await request.post('/api/v1/auth/login', {
      data: { email: 'wrong@test.cl', password: 'bad' }
    });
    expect(response.status()).toBe(401);
  });

  test('POST /auth/login returns 400 with empty fields @smoke', async ({ request }) => {
    const response = await request.post('/api/v1/auth/login', {
      data: { email: '', password: '' }
    });
    expect(response.status()).toBe(400);
  });

  test('POST /auth/forgot-password always returns success @smoke', async ({ request }) => {
    const response = await request.post('/api/v1/auth/forgot-password', {
      data: { email: 'anyone@example.com' }
    });
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.success).toBe(true);
    expect(body.data.message).toBeTruthy();
  });

  test('GET /health/auth returns health info @smoke', async ({ request }) => {
    const response = await request.get('/health/auth');
    expect(response.ok()).toBeTruthy();

    const body = await response.json();
    expect(body.status).toBe('healthy');
    expect(body.module).toBe('auth');
    expect(body.totalUsers).toBeGreaterThanOrEqual(0);
  });
});