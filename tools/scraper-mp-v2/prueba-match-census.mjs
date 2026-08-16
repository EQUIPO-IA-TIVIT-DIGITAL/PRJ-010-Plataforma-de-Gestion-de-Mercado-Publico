// prueba-match-census.mjs — prueba de rendimiento del match multi-skill contra Census.
// Simula el flujo real de MPM (Fase 2): expansión catálogo-first → consultas paralelas
// (semáforo 8) → dedup por email → cobertura. Mide tiempos exactos con performance.now().
//
// Uso: node prueba-match-census.mjs
// (usa credenciales de servicio de Census vía --user/--pass o variables CENSUS_*)

import { fileURLToPath } from 'url';
import path from 'path';
import dotenv from 'dotenv';
const __dirname = path.dirname(fileURLToPath(import.meta.url));
dotenv.config({ path: path.join(__dirname, '.env'), override: false });
dotenv.config({ path: path.join(__dirname, '..', '..', '.env'), override: false });
import { launch, close, esperarConDelay } from './modulos/browser.js';

const BASE = 'http://136.115.20.85/b-side';
const USER = process.argv.find(a => a.startsWith('--user='))?.split('=')[1] ?? 'SERVICE.KNOWLEDGE-USER-001@TIVIT.COM';
const PASS = process.argv.find(a => a.startsWith('--pass='))?.split('=')[1] ?? '';
const PAIS = 'Chile';
const CONCURRENCIA = 8;

// Skills/conceptos típicos de la licitación de antivirus/seguridad (lo que pediría el match real)
const CONCEPTOS = [
  'antivirus', 'firewall', 'SIEM', 'EDR', 'endpoint security', 'network security',
  'cloud security', 'threat intelligence', 'SOC', 'ISO 27001',
];

// ── utilidades ────────────────────────────────────────────────────────────────
function normalizar(s) {
  return (s || '')
    .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim();
}
function tokenSet(s) { return new Set(normalizar(s).split(' ').filter(Boolean)); }
function tokenSetRatio(a, b) {
  const A = tokenSet(a), B = tokenSet(b);
  if (!A.size || !B.size) return 0;
  let inter = 0; for (const t of A) if (B.has(t)) inter++;
  const union = new Set([...A, ...B]).size;
  return (inter / union) * 100;
}
async function pool(urls, concurrencia, fetchFn) {
  const tiempos = new Map();
  const resultados = new Map();
  let idx = 0;
  const workers = Array.from({ length: Math.min(concurrencia, urls.length) }, async () => {
    while (idx < urls.length) {
      const i = idx++;
      const url = urls[i];
      const t0 = performance.now();
      try { const r = await fetchFn(url); tiempos.set(i, { ms: performance.now() - t0, status: r.status }); resultados.set(i, r); }
      catch (e) { tiempos.set(i, { ms: performance.now() - t0, status: 'ERR' }); }
    }
  });
  await Promise.all(workers);
  return { tiempos, resultados };
}

let browser, context, page;
try {
  const inst = await launch(true, null);
  browser = inst.browser; context = inst.context; page = inst.page;
  await page.goto(`${BASE}/swagger/index.html`, { waitUntil: 'domcontentloaded', timeout: 45000 }).catch(() => {});
  await esperarConDelay(1500);

  // ── 1) Auth Census (desde el navegador, transporte que pasa el WAF) ──
  const tAuth = performance.now();
  const auth = await page.evaluate(async ({ url, user, pass }) => {
    const resp = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ Username: user, Password: pass }) });
    return { status: resp.status, body: await resp.text() };
  }, { url: `${BASE}/external-auth/token`, user: USER, pass: PASS });
  const msAuth = performance.now() - tAuth;
  if (auth.status !== 200 && auth.status !== 201) { console.log('[MATCH] AUTH falló:', auth.status, auth.body.substring(0, 120)); process.exit(1); }
  const tokens = JSON.parse(auth.body);
  console.log(`[MATCH] AUTH: ${auth.status} en ${msAuth.toFixed(0)} ms (accessToken ${tokens.accessToken.length} chars)`);

  const fetchCensus = (url) => page.evaluate(async ({ url, at, st }) => {
    const resp = await fetch(url, { headers: { Authorization: `Bearer ${at}`, 'x-security': st } });
    const text = await resp.text();
    return { status: resp.status, body: text };
  }, { url, at: tokens.accessToken, st: tokens.securityToken ?? '' });

  // ── 2) Catálogo desde census/knowledge (refrescable, capa 1 de expansión) ──
  const tCat = performance.now();
  const catResp = await fetchCensus(`${BASE}/census/knowledge`);
  const catMs = performance.now() - tCat;
  const cat = JSON.parse(catResp.body);
  const types = [];
  const techs = new Set();
  for (const grupo of cat) for (const cate of (grupo.categories ?? [])) for (const t of (cate.types ?? [])) {
    types.push({ name: t.name, techs: (t.knowledge ?? []).map(k => k.name) });
    (t.knowledge ?? []).forEach(k => techs.add(k.name));
  }
  console.log(`[MATCH] CATÁLOGO: ${catMs.toFixed(0)} ms — ${types.length} types, ${techs.size} tecnologías`);

  // ── 3) Expansión catálogo-first (sin IA): concepto → tecnologías ──
  const tExp = performance.now();
  const expandidos = CONCEPTOS.map(concepto => {
    // Capa 1: fuzzy contra types (el type ES el concepto amplio: "Front-END", "Testes e QA"...)
    let mejor = null, mejorScore = 0;
    for (const t of types) {
      const s = tokenSetRatio(concepto, t.name);
      if (s > mejorScore) { mejorScore = s; mejor = t; }
    }
    if (mejor && mejorScore >= 80) return { concepto, via: `type: ${mejor.name} (${mejorScore.toFixed(0)}%)`, tecnologias: mejor.techs.slice(0, 4) };
    // Capa 2: match directo contra tecnología
    let can = null, canScore = 0;
    for (const t of techs) { const s = tokenSetRatio(concepto, t); if (s > canScore) { canScore = s; can = t; } }
    if (can && canScore >= 80) return { concepto, via: `tech: ${can} (${canScore.toFixed(0)}%)`, tecnologias: [can] };
    return { concepto, via: 'sin match en catálogo (fallback IA pendiente)', tecnologias: [concepto] };
  });
  const msExp = performance.now() - tExp;
  expandidos.forEach(e => console.log(`[MATCH]   ${e.concepto.padEnd(20)} → ${e.via.padEnd(45)} → [${e.tecnologias.join(', ')}]`));
  console.log(`[MATCH] EXPANSIÓN: ${msExp.toFixed(1)} ms (catálogo local, sin IA)`);

  // ── 4) Consultas paralelas a Census (semáforo 8) con medición ──
  const urls = [];
  for (const e of expandidos) for (const t of e.tecnologias)
    urls.push(`${BASE}/services/knowledge/technologies/users?technologyName=${encodeURIComponent(t)}&workCountry=${encodeURIComponent(PAIS)}`);
  // certificaciones como consultas de certifications
  for (const c of ['ISO 27001']) urls.push(`${BASE}/services/knowledge/certifications/users?certificationName=${encodeURIComponent(c)}&workCountry=${encodeURIComponent(PAIS)}`);

  console.log(`[MATCH] Consultas a Census: ${urls.length} (concurrencia ${CONCURRENCIA})`);
  const tTotal = performance.now();
  const { tiempos, resultados } = await pool(urls, CONCURRENCIA, fetchCensus);
  const msTotal = performance.now() - tTotal;

  // ── 5) Dedup por email + cobertura (desde los bodies ya obtenidos) ──
  const personas = new Map();
  let errores = 0;
  const bodies = [];
  for (let i = 0; i < urls.length; i++) {
    const t = tiempos.get(i);
    if (t.status !== 200) { errores++; continue; }
    const resp = resultados.get(i);
    try {
      const arr = JSON.parse(resp.body);
      const termino = decodeURIComponent(new URL(urls[i]).searchParams.get('technologyName') || new URL(urls[i]).searchParams.get('certificationName'));
      for (const p of arr) {
        if (!personas.has(p.userEmail)) personas.set(p.userEmail, { name: p.userName, skills: [] });
        personas.get(p.userEmail).skills.push(termino);
      }
      bodies.push({ termino, count: arr.length });
    } catch { errores++; }
  }

  // ── 6) Reporte ──
  console.log('\n=== REPORTE DE RENDIMIENTO ===');
  console.log(`Consultas: ${urls.length} · Concurrencia: ${CONCURRENCIA}`);
  console.log(`Tiempo total consultas (paralelo): ${msTotal.toFixed(0)} ms`);
  console.log(`Por consulta: min=${Math.min(...[...tiempos.values()].map(t => t.ms)).toFixed(0)} ms · max=${Math.max(...[...tiempos.values()].map(t => t.ms)).toFixed(0)} ms · avg=${([...tiempos.values()].reduce((a, t) => a + t.ms, 0) / tiempos.size).toFixed(0)} ms`);
  console.log(`Errores: ${errores}/${urls.length}`);
  console.log(`Personas únicas (dedup por email): ${personas.size}`);
  console.log(`Cobertura por consulta: ${bodies.map(b => `${b.termino}=${b.count}`).join(', ')}`);
  const top = [...personas.entries()].map(([email, p]) => ({ email, name: p.name, skills: p.skills.length })).sort((a, b) => b.skills - a.skills).slice(0, 5);
  console.log('Top candidatos por cobertura:');
  top.forEach(c => console.log(`  ${c.skills}/10 skills — ${c.name} (${c.email})`));
  console.log(`\nTiempos totales: auth=${msAuth.toFixed(0)}ms · catálogo=${catMs.toFixed(0)}ms · expansión=${msExp.toFixed(1)}ms · consultas=${msTotal.toFixed(0)}ms`);
  console.log(`SIMULACIÓN CACHE: una 2ª licitación con los mismos skills haría 0 consultas (resultados por tecnología cacheados 24h)`);
} catch (e) {
  console.log('[MATCH] ERROR', e.message);
  process.exit(1);
} finally {
  await close(browser, context, page).catch(() => {});
}
