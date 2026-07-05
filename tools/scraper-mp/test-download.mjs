import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CARPETA = '/tmp/test-acta';

// Quick test: login + navigate + download acta
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ acceptDownloads: true });
const page = await context.newPage();

try {
  // Login
  await page.goto('https://www.mercadopublico.cl/Home', { waitUntil: 'networkidle', timeout: 30000 });
  await page.click('button.btn.btn-xl.btn-pri');
  await page.waitForTimeout(3000);
  await page.click('#liExtranjero');
  await page.waitForTimeout(1500);
  await page.fill('#username-re', '73058136');
  await page.fill('#password-re', 'Tivit2025.');
  await page.click('#kc-login-re');
  await page.waitForTimeout(5000);

  // Select org
  const hasOrg = await page.locator('#rdbOrg1269309').isVisible().catch(() => false);
  if (hasOrg) {
    await page.click('#rdbOrg1269309');
    await page.waitForTimeout(1000);
    await page.click('div.modal-footer > a[href="#"]');
    await page.waitForTimeout(5000);
  }

  // Navigate to search
  await page.goto('https://www.mercadopublico.cl/BID/Modules/RFB/NEwSearchProcurement.aspx', { waitUntil: 'networkidle', timeout: 30000 });
  await page.waitForTimeout(2000);

  // Select radio + filters
  await page.click('#radLicitacionOfertado');
  await page.selectOption('#cboRegion', ' ');
  await page.selectOption('#cboState', '8');

  const desde = page.locator('#calFrom');
  await desde.click(); await desde.fill(''); await desde.fill('01-01-2026'); await page.keyboard.press('Tab');
  const hasta = page.locator('#calTo');
  await hasta.click(); await hasta.fill(''); await hasta.fill('09-07-2026'); await page.keyboard.press('Tab');

  await page.click('#btnSearch');
  await page.waitForTimeout(5000);

  // Click first result
  const firstLink = page.locator('a[onclick*="OpenGlobalPopup"]').first();
  const pagesBefore = context.pages().length;
  await firstLink.click();
  await page.waitForTimeout(3000);

  const pages = context.pages();
  if (pages.length <= pagesBefore) { throw new Error('No se abrio ficha'); }

  const fichaPage = pages[pages.length - 1];
  console.log('Ficha URL:', fichaPage.url().substring(0, 100));

  // Click Ver Adjuntos
  await fichaPage.click('#imgAdjuntos');
  await page.waitForTimeout(4000);

  const pagesAfter = context.pages();
  if (pagesAfter.length <= pages.length) { throw new Error('No se abrio adjuntos'); }

  const adjPage = pagesAfter[pagesAfter.length - 1];
  console.log('Adj URL:', adjPage.url().substring(0, 100));

  // Find acta
  const actaInfo = await adjPage.evaluate(() => {
    const table = document.getElementById('DWNL_grdId');
    if (!table) return null;
    const rows = table.querySelectorAll('tr');
    for (let i = 1; i < rows.length; i++) {
      const cells = rows[i].querySelectorAll('td');
      if (cells.length < 7) continue;
      const tipo = cells[2]?.textContent?.trim() || '';
      const verBtn = cells[6]?.querySelector('input[type="image"]');
      if (tipo === 'Acta de Evaluación' && verBtn) {
        return { btnId: verBtn.id, nombre: cells[1]?.textContent?.trim(), tipo };
      }
    }
    return null;
  });

  console.log('Acta found:', JSON.stringify(actaInfo));

  if (actaInfo) {
    // Download
    if (!fs.existsSync(CARPETA)) fs.mkdirSync(CARPETA, { recursive: true });

    const downloadPromise = adjPage.waitForEvent('download', { timeout: 30000 });
    await adjPage.locator(`#${actaInfo.btnId}`).click();
    const download = await downloadPromise;
    const filePath = path.join(CARPETA, download.suggestedFilename());
    await download.saveAs(filePath);
    const stats = fs.statSync(filePath);
    console.log('Downloaded:', download.suggestedFilename(), `(${stats.size} bytes) - OK!`);
  }

} catch (e) {
  console.error('ERROR:', e.message);
} finally {
  await browser.close();
}

process.exit(0);
