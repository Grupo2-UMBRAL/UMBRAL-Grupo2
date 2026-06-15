import { test, expect } from '@playwright/test';
import path from 'path';

test('verify landing and login page', async ({ page }) => {
  const screenshotDir = path.resolve('.');
  const consoleErrors = [];
  const networkErrors = [];

  // Track console errors
  page.on('console', msg => {
    if (msg.type() === 'error') {
      consoleErrors.push(msg.text());
    }
  });

  // Track failed network requests
  page.on('requestfailed', request => {
    networkErrors.push(`${request.method()} ${request.url()}: ${request.failure()?.errorText || 'Unknown error'}`);
  });

  page.on('response', response => {
    const status = response.status();
    if (status >= 400) {
      networkErrors.push(`${response.request().method()} ${response.url()}: HTTP ${status}`);
    }
  });

  console.log('\n--- Navegando a la Página de Inicio ---');
  await page.goto('http://127.0.0.1:3000/');
  
  // Wait for React to render
  await page.waitForSelector('h1');
  
  // Verify main title contains UMBRAL
  const heading = page.locator('h1');
  await expect(heading).toContainText(/Control/i);

  // Take screenshot of landing
  const landingPath = path.join(screenshotDir, 'screenshot-landing.png');
  await page.screenshot({ path: landingPath });
  console.log(`✅ Captura de pantalla de la página de inicio guardada en: ${landingPath}`);

  console.log('\n--- Haciendo clic en Iniciar Sesión ---');
  // Click on "Iniciar Sesión" (Login) button
  const loginButton = page.locator('button:has-text("Ingresar"), a:has-text("Ingresar")').first();
  await loginButton.click();

  console.log('Esperando redirección a Keycloak...');
  // Wait for the URL to change to Keycloak redirect
  await page.waitForURL(url => url.href.includes('/auth/realms/umbral'));

  console.log('Verificando página de login de Keycloak...');
  // Wait for Keycloak login page to load its form elements
  await page.waitForSelector('#username');
  await page.waitForSelector('#password');
  await page.waitForSelector('#kc-login');

  // Verify elements are visible
  const usernameInput = page.locator('#username');
  const passwordInput = page.locator('#password');
  const submitButton = page.locator('#kc-login');

  await expect(usernameInput).toBeVisible();
  await expect(passwordInput).toBeVisible();
  await expect(submitButton).toBeVisible();

  // Take screenshot of login page
  const loginPath = path.join(screenshotDir, 'screenshot-login.png');
  await page.screenshot({ path: loginPath });
  console.log(`✅ Captura de pantalla de la página de login guardada en: ${loginPath}`);

  // Print diagnostics
  console.log('\n--- Diagnóstico de la Ejecución ---');
  if (consoleErrors.length > 0) {
    console.log(`⚠️ Errores de consola detectados (${consoleErrors.length}):`);
    consoleErrors.forEach(err => console.log(`  - ${err}`));
  } else {
    console.log('✅ Cero errores de consola detectados.');
  }

  if (networkErrors.length > 0) {
    console.log(`⚠️ Fallos de red / recursos no encontrados (${networkErrors.length}):`);
    networkErrors.forEach(err => console.log(`  - ${err}`));
  } else {
    console.log('✅ Todos los recursos (CSS, JS, imágenes) cargados correctamente (0 fallos).');
  }

  // Expect no styling resources failed to load (no patternfly or login.css 404s)
  const hasStyleErrors = networkErrors.some(err => err.includes('.css'));
  expect(hasStyleErrors).toBe(false);
  console.log('\n🎉 ¡Prueba completada con éxito! Todos los estilos y navegación funcionan.');
});
