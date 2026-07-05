#!/usr/bin/env node

import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import readline from 'readline';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const CARPETA_SALIDA = path.join(__dirname, 'descargas');

let browser = null;
let context = null;
let page = null;
let loginConfirmado = false;
let licitacionActual = 1;

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
  prompt: '\n> '
});

async function inicializarNavegador() {
  console.log('\nIniciando navegador...');
  browser = await chromium.launch({ headless: false });
  context = await browser.newContext({ acceptDownloads: true });
  page = await context.newPage();
  await page.setViewportSize({ width: 1920, height: 1080 });
  console.log('Navegador abierto\n');
}

function getDocTipo(href, texto) {
  const h = (href || '').toLowerCase();
  const t = (texto || '').toLowerCase();
  if (t.includes('base') || h.includes('bases')) return 'BASES';
  if (t.includes('acta')) return 'ACTA';
  if (t.includes('resolu')) return 'RESOLUCION';
  if (t.includes('anexo')) return 'ANEXO';
  if (t.includes('contrato')) return 'CONTRATO';
  if (t.includes('tecnico') || t.includes('especificacion')) return 'ESPEC_TECNICA';
  if (t.includes('.pdf') || h.includes('.pdf')) return 'PDF';
  if (h.includes('.doc') || h.includes('.xls') || h.includes('.xlsx')) return 'DOC';
  if (h.includes('downloadfile') || h.includes('download') || h.includes('descargar')) return 'DESCARGA';
  if (h.includes('file') || h.includes('archivo') || h.includes('documento') || h.includes('adjunt')) return 'DESCARGA';
  if (t.includes('archivo') || t.includes('documento') || t.includes('adjunt') || t.includes('descargar')) return 'DESCARGA';
  return 'OTRO';
}

function mostrarAyuda() {
  console.log(`
============================================================
SCRAPER MERCADO PUBLICO - TIVIT
TU NAVEGAS, YO EXTRAIGO
============================================================
Comandos:

  login         Abre el navegador y la pagina de inicio
  listo        Confirma que ya iniciaste sesion
  nueva        Cambia a la ultima ventana nueva abierta
  tabs         Lista todas las ventanas/pestanas abiertas
  tab [n]      Cambia a la ventana numero [n]
  extraer      EXTRAE la pagina actual (datos + docs + HTML)
  links        Muestra TODOS los enlaces de la pagina
  capturar     Guarda screenshot de la pagina actual
  contar [n]   Muestra o cambia el numero de extraccion
  listar       Muestra las extracciones realizadas
  ver [n]      Muestra detalle de la extraccion [n]
  donde        Muestra URL y titulo de la pagina actual
  abrir [url]  Navega a una URL especifica
  html         Guarda la pagina actual como HTML
  estado       Muestra el estado de la sesion
  ayuda        Esta lista
  salir        Cierra el navegador y termina

FLUJO: Tu navegas -> cuando estes en una vista que quieres
       guardar, escribe "extraer"
============================================================
  `);
}

async function cmdLogin() {
  console.log('\nAbriendo pagina de Mercado Publico...');
  await page.goto('https://www.mercadopublico.cl/', { waitUntil: 'networkidle' });
  await page.setViewportSize({ width: 1920, height: 1080 });
  console.log('Pagina principal abierta');
  console.log('URL: ' + page.url());
  console.log('\nInicia sesion manualmente en el navegador');
  console.log('Cuando termines, escribe "listo"\n');
}

async function cmdListo() {
  const url = page.url();
  const esLogin = url.includes('login') || url.includes('auth') || url.includes('heimdall');
  if (esLogin) {
    console.log('Aun no has iniciado sesion. URL actual: ' + url.substring(0, 80));
  } else {
    loginConfirmado = true;
    console.log('Sesion activa. URL: ' + page.url().substring(0, 80));
  }
}

async function cmdTabs() {
  const pages = context.pages();
  console.log('\nVENTANAS/PESTANAS ABIERTAS:');
  pages.forEach((p, i) => {
    const esActiva = p === page;
    console.log('  ' + (i + 1) + '. ' + (esActiva ? '[ACTIVA] ' : '') + p.url().substring(0, 70));
  });
  console.log('Total: ' + pages.length + '\n');
}

async function cmdNueva() {
  const pages = context.pages();
  if (pages.length === 0) {
    console.log('No hay ventanas abiertas');
    return;
  }
  const ultima = pages[pages.length - 1];
  page = ultima;
  await page.setViewportSize({ width: 1920, height: 1080 });
  console.log('Cambiado a la ultima ventana');
  console.log('URL: ' + page.url().substring(0, 80));
  const title = await page.title();
  console.log('Titulo: ' + title);
}

async function cmdTab(idx) {
  const pages = context.pages();
  const num = parseInt(idx);
  if (isNaN(num) || num < 1 || num > pages.length) {
    console.log('Numero invalido. Rango: 1-' + pages.length + ' (usa "tabs" para ver lista)');
    return;
  }
  page = pages[num - 1];
  await page.setViewportSize({ width: 1920, height: 1080 });
  console.log('Cambiado a ventana #' + num);
  console.log('URL: ' + page.url().substring(0, 80));
}

async function cmdDonde() {
  if (!page) {
    console.log('Navegador cerrado');
    return;
  }
  const url = page.url();
  const title = await page.title();
  const pages = context.pages();
  const posicion = pages.findIndex(p => p === page) + 1;
  console.log('\nPAGINA ACTUAL:');
  console.log('  Ventana: ' + posicion + '/' + pages.length);
  console.log('  Titulo: ' + title);
  console.log('  URL: ' + url);
}

async function cmdAbrir(url) {
  if (!url) {
    console.log('Uso: abrir [url]');
    return;
  }
  console.log('\nNavegando a: ' + url.substring(0, 80) + '...');
  await page.goto(url, { waitUntil: 'networkidle', timeout: 30000 });
  await page.setViewportSize({ width: 1920, height: 1080 });
  console.log('Pagina cargada');
  await cmdDonde();
}

async function cmdLinks() {
  console.log('\nTodos los enlaces de la pagina actual:');
  const links = await page.$$eval('a[href]', anchors => {
    return anchors
      .filter(a => {
        const href = (a.href || '');
        return href && href !== '#' && href !== 'javascript:void(0);' && href !== 'javascript:;';
      })
      .map(a => ({
        href: a.href,
        texto: a.innerText?.trim() || '',
        clase: a.className || '',
        visible: a.offsetParent !== null
      }));
  });

  if (links.length === 0) {
    console.log('  No se encontraron enlaces');
  } else {
    links.forEach((l, i) => {
      const visible = l.visible ? '' : ' [oculto]';
      const texto = (l.texto || 'sin texto').substring(0, 40);
      console.log('  ' + (i + 1) + '. [' + texto + ']' + visible);
      console.log('     ' + l.href.substring(0, 80));
    });
    console.log('\nTotal enlaces: ' + links.length);
  }

  console.log('\nBuscando botones y elementos de descarga...');
  const botones = await page.$$eval('input[type="button"], input[type="submit"], button', els => {
    return els.map(e => ({
      tipo: e.type || e.tagName,
      texto: e.value || e.innerText?.trim() || '',
      clase: e.className || '',
      onclick: e.onclick ? '[tiene onclick]' : ''
    })).filter(e => e.texto || e.onclick);
  });

  if (botones.length > 0) {
    console.log('\nBotones encontrados:');
    botones.forEach((b, i) => {
      console.log('  ' + (i + 1) + '. [' + (b.texto || 'sin texto').substring(0, 40) + '] ' + b.clase + ' ' + b.onclick);
    });
  }
  console.log('');
}

async function cmdCapturar() {
  const carpeta = path.join(CARPETA_SALIDA, 'extraccion-' + licitacionActual);
  if (!fs.existsSync(carpeta)) fs.mkdirSync(carpeta, { recursive: true });
  const screenshot = path.join(carpeta, 'screenshot-' + Date.now() + '.png');
  await page.screenshot({ path: screenshot, fullPage: true });
  console.log('\nScreenshot guardado: ' + screenshot);
}

async function descargarDocumentos(enlaces, carpetaDocs) {
  let descargados = 0;

  for (const doc of enlaces) {
    const nombreBase = (doc.texto || 'documento').replace(/[/\\?%*:|"<>]/g, '_').trim() || 'documento';
    const esPDF = doc.href.toLowerCase().includes('.pdf') || nombreBase.toLowerCase().includes('.pdf');
    const esDOC = doc.href.toLowerCase().includes('.doc') || doc.href.toLowerCase().includes('.xls') || nombreBase.toLowerCase().includes('.doc');
    let ext = '.html';
    if (esPDF) ext = '.pdf';
    else if (esDOC) ext = '.xlsx';
    const nombreArchivo = nombreBase.substring(0, 60) + ext;
    const rutaArchivo = path.join(carpetaDocs, nombreArchivo);

    try {
      console.log('  Descargando [' + doc.tipo + ']: ' + nombreBase.substring(0, 40) + '...');

      if (doc.href.startsWith('javascript:') || doc.href === '#') {
        console.log('    Enlace javascript, intentando hacer clic...');
        const hrefLimpio = doc.href.replace('javascript:', '');
        try {
          await page.evaluate((js) => { eval(js); }, doc.href);
          await page.waitForTimeout(1000);
        } catch (e) {
          console.log('    No se pudo ejecutar javascript');
        }
        continue;
      }

      const response = await page.goto(doc.href, { timeout: 25000, waitUntil: 'domcontentloaded' });
      if (response && response.ok()) {
        const buffer = await response.body();
        if (buffer.length > 100) {
          fs.writeFileSync(rutaArchivo, buffer);
          console.log('    Guardado: ' + nombreArchivo + ' (' + buffer.length.toLocaleString() + ' bytes)');
          descargados++;
        } else {
          console.log('    Archivo vacio o muy pequeno, posible redirect');
        }
      }
    } catch (e) {
      console.log('    Error: ' + e.message.substring(0, 50));
    }
  }
  return descargados;
}

async function extraerPaginaActual() {
  if (!loginConfirmado) {
    console.log('\nPrimero inicia sesion con "login" y confirma con "listo"');
    return;
  }

  const url = page.url();
  if (!url || url.includes('mercadopublico.cl') === false) {
    console.log('\nNo estas en Mercado Publico. URL: ' + url);
    return;
  }

  const num = licitacionActual;
  const carpetaBase = path.join(CARPETA_SALIDA, 'extraccion-' + num);
  const carpetaDocs = path.join(carpetaBase, 'documentos');

  if (!fs.existsSync(CARPETA_SALIDA)) fs.mkdirSync(CARPETA_SALIDA, { recursive: true });
  if (!fs.existsSync(carpetaBase)) fs.mkdirSync(carpetaBase, { recursive: true });
  if (!fs.existsSync(carpetaDocs)) fs.mkdirSync(carpetaDocs, { recursive: true });

  console.log('\nEXTRAENDO pagina actual (extraccion #' + num + ')');
  console.log('URL: ' + url.substring(0, 80));
  console.log('Carpeta: ' + carpetaBase);

  try {
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(2000);

    const titulo = await page.title();
    const urlActual = page.url();

    console.log('\nExtrayendo datos estructurados...');

    const datos = await page.evaluate(() => {
      const texto = document.body.innerText || '';
      const lineas = texto.split('\n').map(l => l.trim()).filter(Boolean);

      const getValue = (etiqueta) => {
        const idx = lineas.findIndex(l => l.includes(etiqueta));
        return idx >= 0 && idx + 1 < lineas.length ? lineas[idx + 1].trim() : null;
      };

      const getBlock = (desdeEtiqueta, maxLineas = 30) => {
        const idx = lineas.findIndex(l => l.includes(desdeEtiqueta));
        if (idx < 0) return null;
        const fragmentos = [];
        for (let i = idx + 1; i < Math.min(lineas.length, idx + maxLineas); i++) {
          const l = lineas[i];
          if (l && !l.includes('Subir') && l.trim() !== '') {
            fragmentos.push(l.trim());
          }
          if (l.includes('Subir')) break;
        }
        return fragmentos.filter(Boolean).join('\n').trim() || null;
      };

      const getAllLinks = () => {
        return Array.from(document.querySelectorAll('a[href], input[type="button"], input[type="submit"], button'))
          .map(el => {
            const tag = el.tagName.toLowerCase();
            const href = tag === 'a' ? el.href : '';
            const texto = (el.innerText || el.value || '').trim();
            return {
              tag,
              href,
              texto,
              clase: el.className || ''
            };
          })
          .filter(l => l.href || l.texto);
      };

      const getAdjuntosSection = () => {
        const secciones = [];
        const elementos = document.querySelectorAll('[id*="gvFiles"], [id*="GridFiles"], [id*="Files"], .archivosadjuntos, #adjuntos, [class*="adjunt"], [id*="Documentos"]');
        elementos.forEach(el => {
          const enlaces = el.querySelectorAll('a');
          const botones = el.querySelectorAll('input, button');
          if (enlaces.length > 0 || botones.length > 0) {
            secciones.push({
              selector: el.id || el.className,
              links: Array.from(enlaces).map(a => ({
                href: a.href,
                texto: a.innerText?.trim() || ''
              })),
              botones: Array.from(botones).map(b => ({
                tipo: b.type || b.tagName,
                texto: b.value || b.innerText?.trim() || '',
                onclick: b.onclick ? '[onclick]' : ''
              }))
            });
          }
        });
        return secciones;
      };

      return {
        tituloPagina: document.title,
        nombre: getValue('Nombre de la licitaci'),
        estado: getValue('Estado:'),
        tipo: getValue('Tipo de licitaci'),
        tipoConvocatoria: getValue('Tipo de convocatoria:'),
        moneda: getValue('Moneda:'),
        organismo: {
          razonSocial: getValue('Raz'),
          rut: getValue('R.U.T.:'),
          unidad: getValue('Unidad de compra:'),
          direccion: getValue('Direcci'),
          comuna: getValue('Comuna:'),
          region: lineas.find(l => l.includes('Regi') && l.includes('licitaci'))?.replace(/Regi.*licitaci/gi, '').trim() || null,
        },
        fechas: {
          publicacion: getValue('Fecha de Publicaci'),
          cierre: getValue('Fecha de cierre'),
          adjudicacion: getValue('Fecha de Adjudicaci'),
          preguntas: {
            inicio: getValue('Fecha inicio de preguntas:'),
            fin: getValue('Fecha final de preguntas:'),
          },
          aperturaTecnica: getValue('Fecha de acto de apertura t'),
          aperturaEconomica: getValue('Fecha de acto de apertura econ'),
        },
        monto: {
          totalEstimado: getValue('Monto Total Estimado:'),
          fuente: getValue('Fuente de financiam'),
        },
        duracion: {
          meses: getValue('Tiempo del Contrato'),
          renovacion: getValue('Contrato con Renovaci'),
          pagos: getValue('Plazos de pago:'),
        },
        responsables: {
          pago: getValue('NombreResponsablePago'),
          contrato: getValue('NombreResponsableContrato'),
        },
        reclamoInfo: lineas.find(l => l.includes('Reclamos recibidos')) || null,
        contenidoBases: getBlock('1. Caracter'),
        organismoDemandante: getBlock('2. Organismo'),
        etapasPlazos: getBlock('3. Etapas y plazos'),
        antecedentesOferta: getBlock('4. Antecedentes para incluir'),
        requisitosContratar: getBlock('5. Requisitos para contratar'),
        criteriosEvaluacion: getBlock('6. Criterios de evaluaci'),
        montosDuracion: getBlock('7. Montos y duraci'),
        garantias: getBlock('8. Garant'),
        requerimientosTecnicos: getBlock('9. Requerimientos t'),
        demandasTribunal: getBlock('10. Demandas ante'),
        allLinks: getAllLinks(),
        adjuntosSection: getAdjuntosSection(),
        rawText: texto.substring(0, 100000),
      };
    });

    datos.urlOriginal = urlActual;
    datos.fechaExtraccion = new Date().toISOString();

    const archivoDatos = path.join(carpetaBase, 'datos.json');
    fs.writeFileSync(archivoDatos, JSON.stringify(datos, null, 2));
    console.log('Datos guardados: datos.json');

    console.log('\nBuscando y descargando documentos adjuntos...');

    const enlacesDocs = [];
    const todosLinks = await page.$$eval('a[href]', anchors => {
      return anchors
        .filter(a => {
          const href = (a.href || '');
          const texto = (a.innerText || '').toLowerCase();
          return href &&
                 href !== '#' &&
                 href !== 'javascript:void(0);' &&
                 href !== 'javascript:;' &&
                 !href.includes('mercadopublico.cl/Home') &&
                 !href.includes('#tab');
        })
        .map(a => ({
          href: a.href,
          texto: (a.innerText || '').trim(),
          tipo: (() => {
            const h = (a.href || '').toLowerCase();
            const t = (a.innerText || '').toLowerCase();
            if (t.includes('base')) return 'BASES';
            if (t.includes('acta')) return 'ACTA';
            if (t.includes('resolu')) return 'RESOLUCION';
            if (t.includes('anexo')) return 'ANEXO';
            if (t.includes('contrato')) return 'CONTRATO';
            if (t.includes('tecnico') || t.includes('especificacion')) return 'ESPEC_TECNICA';
            if (h.includes('.pdf') || t.includes('.pdf')) return 'PDF';
            if (h.includes('.doc') || h.includes('.xls')) return 'DOC';
            if (h.includes('downloadfile') || h.includes('download') || h.includes('file')) return 'DESCARGA';
            if (t.includes('archivo') || t.includes('documento') || t.includes('adjunt') || t.includes('descargar')) return 'DESCARGA';
            return 'OTRO';
          })()
        }));
    });

    enlacesDocs.push(...todosLinks.filter(l => l.tipo !== 'OTRO'));
    enlacesDocs.push(...todosLinks.filter(l => l.tipo === 'OTRO'));

    let docsDescargados = 0;
    if (enlacesDocs.length === 0) {
      console.log('No se encontraron documentos de descarga');
      console.log('Usa el comando "links" para ver todos los enlaces disponibles');
      console.log('Enlaces totales en pagina: ' + (datos.allLinks?.length || 0));
    } else {
      console.log('Documentos detectados: ' + enlacesDocs.length);
      console.log('Prioridad: BASES > ACTA > RESOLUCION > ANEXO > ESPEC_TECNICA > PDF > DOC > DESCARGA');
      docsDescargados = await descargarDocumentos(enlacesDocs, carpetaDocs);
      console.log('Documentos descargados: ' + docsDescargados);
    }

    console.log('\nGuardando HTML completo...');
    const htmlFile = path.join(carpetaBase, 'pagina-completa.html');
    const content = await page.content();
    fs.writeFileSync(htmlFile, content);
    console.log('HTML guardado: pagina-completa.html');

    console.log('\nGuardando screenshot...');
    const screenshotFile = path.join(carpetaBase, 'screenshot.png');
    await page.screenshot({ path: screenshotFile, fullPage: true });
    console.log('Screenshot guardado: screenshot.png');

    console.log('\nCarpeta: ' + carpetaBase);
    console.log('=== EXTRACCION #' + num + ' COMPLETA ===');

    const archivoInfo = path.join(CARPETA_SALIDA, '_info.json');
    const info = fs.existsSync(archivoInfo) ? JSON.parse(fs.readFileSync(archivoInfo)) : { extracciones: [] };
    info.extracciones.push({
      numero: num,
      url: urlActual,
      titulo: titulo,
      nombre: datos.nombre,
      estado: datos.estado,
      docsDescargados: docsDescargados,
      fecha: datos.fechaExtraccion,
      carpeta: carpetaBase
    });
    fs.writeFileSync(archivoInfo, JSON.stringify(info, null, 2));

    licitacionActual++;

  } catch (e) {
    console.log('Error: ' + e.message);
  }
}

async function cmdHtml() {
  if (!page) {
    console.log('Navegador cerrado');
    return;
  }
  const carpeta = path.join(CARPETA_SALIDA, 'extraccion-' + licitacionActual);
  if (!fs.existsSync(carpeta)) fs.mkdirSync(carpeta, { recursive: true });
  const content = await page.content();
  const archivo = path.join(carpeta, 'pagina-actual-' + Date.now() + '.html');
  fs.writeFileSync(archivo, content);
  console.log('\nHTML guardado: ' + archivo);
}

function listarExtraidos() {
  const archivoInfo = path.join(CARPETA_SALIDA, '_info.json');
  if (!fs.existsSync(archivoInfo)) {
    console.log('\nNo hay extracciones aun\n');
    return;
  }
  const info = JSON.parse(fs.readFileSync(archivoInfo));
  console.log('\nEXTRACCIONES REALIZADAS:');
  if (!info.extracciones || info.extracciones.length === 0) {
    console.log('(ninguna aun)\n');
    return;
  }
  for (const ex of info.extracciones) {
    console.log('\n  #' + ex.numero);
    console.log('  Nombre: ' + (ex.nombre || '?'));
    console.log('  Estado: ' + (ex.estado || '?'));
    console.log('  URL: ' + (ex.url || '?'));
    console.log('  Docs: ' + (ex.docsDescargados || 0));
    console.log('  Fecha: ' + ex.fecha);
  }
  console.log('');
}

function verDetalle(num) {
  if (!num) {
    console.log('Uso: ver [n]');
    return;
  }
  const archivoInfo = path.join(CARPETA_SALIDA, '_info.json');
  if (!fs.existsSync(archivoInfo)) {
    console.log('No hay extracciones');
    return;
  }
  const info = JSON.parse(fs.readFileSync(archivoInfo));
  const ex = info.extracciones.find(e => e.numero === parseInt(num));
  if (!ex) {
    console.log('Extraccion #' + num + ' no existe');
    return;
  }

  const datosPath = path.join(ex.carpeta, 'datos.json');
  if (!fs.existsSync(datosPath)) {
    console.log('Archivo datos.json no encontrado');
    return;
  }

  const datos = JSON.parse(fs.readFileSync(datosPath));
  console.log('\nDETALLE EXTRACCION #' + ex.numero);
  console.log('  Nombre: ' + (datos.nombre || '?'));
  console.log('  Estado: ' + (datos.estado || '?'));
  console.log('  Organismo: ' + (datos.organismo?.razonSocial || '?'));
  console.log('  RUT: ' + (datos.organismo?.rut || '?'));
  console.log('  Region: ' + (datos.organismo?.region || '?'));
  console.log('  Monto: ' + (datos.monto?.totalEstimado || '?') + ' ' + (datos.moneda || ''));
  console.log('  Duracion: ' + (datos.duracion?.meses || '?'));
  console.log('  Fecha pub: ' + (datos.fechas?.publicacion || '?'));
  console.log('  Fecha cierre: ' + (datos.fechas?.cierre || '?'));
  console.log('  Fecha adjud: ' + (datos.fechas?.adjudicacion || '?'));
  console.log('  URL: ' + (datos.urlOriginal || '?'));
  if (datos.contenidoBases) {
    console.log('\n  CONTENIDO BASES:');
    console.log('  ' + datos.contenidoBases.substring(0, 300));
  }
  if (datos.criteriosEvaluacion) {
    console.log('\n  CRITERIOS:');
    console.log('  ' + datos.criteriosEvaluacion);
  }
  if (datos.allLinks && datos.allLinks.length > 0) {
    console.log('\n  ENLACES (' + datos.allLinks.length + '):');
    datos.allLinks.slice(0, 15).forEach((l, i) => {
      console.log('  ' + (i + 1) + '. [' + l.tag + '] ' + (l.texto || 'sin texto').substring(0, 30) + ' -> ' + l.href.substring(0, 60));
    });
  }
  if (datos.adjuntosSection && datos.adjuntosSection.length > 0) {
    console.log('\n  SECCIONES DE ADJUNTOS:');
    datos.adjuntosSection.forEach((s, i) => {
      console.log('  Seccion ' + (i + 1) + ': ' + s.selector);
      console.log('    Links: ' + s.links.length);
      console.log('    Botones: ' + s.botones.length);
    });
  }
}

function cmdEstado() {
  const pages = context?.pages() || [];
  console.log('\nESTADO:');
  console.log('  Navegador: ' + (browser ? 'ABIERTO' : 'CERRADO'));
  if (page) {
    console.log('  Ventana: ' + (pages.findIndex(p => p === page) + 1) + '/' + pages.length);
    console.log('  URL: ' + page.url().substring(0, 80));
    console.log('  Sesion: ' + (loginConfirmado ? 'ACTIVA' : 'NO'));
  }
  console.log('  Extracciones: ' + (licitacionActual - 1) + '\n');
}

async function cmdSalir() {
  console.log('\nCerrando navegador...');
  if (browser) await browser.close();
  console.log('Listo. Archivos en: ' + CARPETA_SALIDA);
  process.exit(0);
}

async function procesarComando(input) {
  const partes = input.trim().split(/\s+/);
  const cmd = (partes[0] || '').toLowerCase();
  const arg = partes.slice(1).join(' ');

  switch (cmd) {
    case 'ayuda':
    case 'help':
    case '?':
      mostrarAyuda();
      break;
    case 'login':
      await cmdLogin();
      break;
    case 'listo':
      await cmdListo();
      break;
    case 'nueva':
      await cmdNueva();
      break;
    case 'tabs':
      await cmdTabs();
      break;
    case 'tab':
      await cmdTab(arg);
      break;
    case 'extraer':
      await extraerPaginaActual();
      break;
    case 'contar':
      if (arg) {
        const nuevoNum = parseInt(arg);
        if (!isNaN(nuevoNum) && nuevoNum > 0) {
          licitacionActual = nuevoNum;
          console.log('\nExtraccion #: ' + licitacionActual);
        }
      } else {
        console.log('\nExtraccion actual: #' + licitacionActual);
      }
      break;
    case 'capturar':
      await cmdCapturar();
      break;
    case 'listar':
    case 'ls':
      listarExtraidos();
      break;
    case 'ver':
      verDetalle(arg);
      break;
    case 'donde':
      await cmdDonde();
      break;
    case 'abrir':
      await cmdAbrir(arg);
      break;
    case 'links':
      await cmdLinks();
      break;
    case 'html':
      await cmdHtml();
      break;
    case 'estado':
      cmdEstado();
      break;
    case 'salir':
    case 'exit':
    case 'quit':
      await cmdSalir();
      break;
    default:
      if (input.trim()) {
        console.log('Comando no reconocido. Escribe "ayuda".');
      }
  }
}

async function main() {
  console.log(`
============================================================
SCRAPER MERCADO PUBLICO - TIVIT
TU NAVEGAS, YO EXTRAIGO
============================================================
  `);

  console.log('Carpeta de salida: ' + CARPETA_SALIDA);
  console.log('');

  if (!fs.existsSync(CARPETA_SALIDA)) fs.mkdirSync(CARPETA_SALIDA, { recursive: true });

  await inicializarNavegador();
  mostrarAyuda();

  console.log('Escribe "login" para abrir el navegador\n');

  rl.prompt();

  rl.on('line', async (input) => {
    await procesarComando(input);
    rl.prompt();
  });

  rl.on('close', () => {
    console.log('\nSesion cerrada');
    if (browser) browser.close();
    process.exit(0);
  });
}

main().catch(e => {
  console.error('Error fatal:', e);
  if (browser) browser.close();
  process.exit(1);
});