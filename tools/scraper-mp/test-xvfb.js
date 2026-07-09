// Prueba rápida: Chromium headed dentro de Xvfb (sin tocar Mercado Público).
// Valida que el framebuffer virtual da un perfil de renderizado "headed"
// sin necesitar una pantalla física — la mitigación para reCAPTCHA en Cloud Run.
const { chromium } = require('playwright');

(async () => {
    console.log('MP_HEADLESS:', process.env.MP_HEADLESS);
    console.log('DISPLAY:', process.env.DISPLAY || '(no set)');

    const browser = await chromium.launch({ headless: false });
    const page = await browser.newPage();
    await page.goto('about:blank');

    // Verificar que navigator.webdriver no está forzado a true (huella headless)
    const webdriverFlag = await page.evaluate(() => navigator.webdriver);
    console.log('navigator.webdriver:', webdriverFlag);

    // Verificar que el viewport renderiza correctamente
    const viewport = page.viewportSize();
    console.log('viewport:', JSON.stringify(viewport));

    await browser.close();
    console.log('✅ Chromium headed dentro de Xvfb: OK');
})().catch(e => {
    console.error('❌ FAIL:', e.message);
    process.exit(1);
});
