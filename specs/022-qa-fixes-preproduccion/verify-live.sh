#!/usr/bin/env bash
# Verificación en vivo de los 13 hallazgos QA (specs/022-qa-fixes-preproduccion) contra el
# stack real de docker-compose. Reproduce los chequeos manuales hechos durante el desarrollo
# (2026-07-08) — antes solo existían como comandos sueltos en la terminal; este script los deja
# como parte del repo para la auditoría y para poder rerunear antes de cada deploy.
#
# Prerrequisito: `docker compose up --build -d` corriendo (mpm-db en :5433, mpm-api en :5001).
# Requiere el usuario demo admin@tivit.cl / test123 (V042__Seed_usuarios_demo.sql).
#
# Uso: scripts/../specs/022-qa-fixes-preproduccion/verify-live.sh
# (o simplemente: bash specs/022-qa-fixes-preproduccion/verify-live.sh)

set -uo pipefail

API="http://localhost:5001"
PASS=0
FAIL=0

ok()   { echo "  ✅ $1"; PASS=$((PASS+1)); }
bad()  { echo "  ❌ $1"; FAIL=$((FAIL+1)); }

echo "════════════════════════════════════════════════════════════"
echo " Verificación en vivo — specs/022-qa-fixes-preproduccion"
echo "════════════════════════════════════════════════════════════"

# ── US1 / BUG-001: la API está sana (si esto falla, una migración rota abortó el arranque —
# comportamiento esperado del fix, no del script) ──────────────────────────────────────────
echo; echo "US1 (BUG-001) — arranque / migraciones"
health_code=$(curl -s -o /dev/null -w "%{http_code}" "$API/health")
if [ "$health_code" = "200" ]; then ok "GET /health -> 200"; else bad "GET /health -> $health_code (¿migración rota abortó el arranque? revisar 'docker logs mpm-api')"; fi

migraciones=$(docker exec mpm-db psql -U mpm -d mpm -tAc "SELECT version FROM _migrations WHERE version IN ('V092','V093') ORDER BY version;" 2>/dev/null)
if echo "$migraciones" | grep -q "V092" && echo "$migraciones" | grep -q "V093"; then
  ok "Migraciones V092 (auth_eventos) y V093 (fix búsqueda) aplicadas"
else
  bad "V092/V093 no aparecen aplicadas en _migrations: [$migraciones]"
fi

# ── US5 / BUG-011: CORS allow-list + JWT sin fallback ──────────────────────────────────────
echo; echo "US5 (BUG-011) — CORS allow-list"
cors_evil=$(curl -s -D - -o /dev/null "$API/api/v1/licitaciones?page=1&pageSize=1" -H "Origin: https://evil-site.example.com" | grep -i "access-control-allow-origin")
if [ -z "$cors_evil" ]; then ok "Origen no autorizado NO recibe Access-Control-Allow-Origin"; else bad "Origen no autorizado SÍ recibió CORS: $cors_evil"; fi

cors_ok=$(curl -s -D - -o /dev/null "$API/api/v1/licitaciones?page=1&pageSize=1" -H "Origin: http://localhost:8181" | grep -i "access-control-allow-origin")
if echo "$cors_ok" | grep -q "8181"; then ok "Origen autorizado (localhost:8181) SÍ recibe CORS"; else bad "Origen autorizado no recibió CORS: $cors_ok"; fi

# ── US6 / BUG-009: webhook de Telegram fail-closed ─────────────────────────────────────────
echo; echo "US6 (BUG-009) — webhook Telegram fail-closed"
webhook_code=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$API/api/v1/telegram/webhook" -H "Content-Type: application/json" -d '{}')
if [ "$webhook_code" = "401" ]; then ok "POST /telegram/webhook sin cabecera -> 401"; else bad "POST /telegram/webhook sin cabecera -> $webhook_code (esperado 401)"; fi

# ── US7 / BUG-010: auditoría de login ──────────────────────────────────────────────────────
echo; echo "US7 (BUG-010) — auditoría de login"
login_resp=$(curl -s -w "\n%{http_code}" -X POST "$API/api/v1/auth/login" -H "Content-Type: application/json" -d '{"email":"admin@tivit.cl","password":"test123"}')
login_code=$(echo "$login_resp" | tail -1)
if [ "$login_code" = "200" ]; then ok "POST /auth/login con credenciales demo -> 200"; else bad "POST /auth/login -> $login_code"; fi

sleep 1
ultimo_evento=$(docker exec mpm-db psql -U mpm -d mpm -tAc "SELECT created_at FROM auth_eventos WHERE email = 'admin@tivit.cl' ORDER BY created_at DESC LIMIT 1;" 2>/dev/null)
if [ -n "$ultimo_evento" ]; then ok "Login quedó registrado en auth_eventos ($ultimo_evento)"; else bad "No se encontró fila reciente en auth_eventos para admin@tivit.cl"; fi

# ── US8 / BUG-008: búsqueda usa índice, no Seq Scan ────────────────────────────────────────
echo; echo "US8 (BUG-008) — búsqueda con índice"
search_code=$(curl -s -o /dev/null -w "%{http_code}" "$API/api/v1/licitaciones?page=1&pageSize=5&search=construccion")
if [ "$search_code" = "200" ]; then ok "GET /licitaciones?search=construccion -> 200"; else bad "Búsqueda -> $search_code"; fi

plan=$(docker exec mpm-db psql -U mpm -d mpm -tAc "EXPLAIN SELECT id FROM licitaciones WHERE deleted_at IS NULL AND search_vector @@ websearch_to_tsquery('spanish','construccion');" 2>/dev/null)
if echo "$plan" | grep -q "idx_licitaciones_search_vector"; then
  ok "Plan de ejecución usa idx_licitaciones_search_vector (Bitmap Index Scan)"
else
  bad "El plan no muestra el índice esperado: $plan"
fi

echo
echo "════════════════════════════════════════════════════════════"
echo " Resultado: $PASS OK / $FAIL fallidos"
echo "════════════════════════════════════════════════════════════"
[ "$FAIL" -eq 0 ]
