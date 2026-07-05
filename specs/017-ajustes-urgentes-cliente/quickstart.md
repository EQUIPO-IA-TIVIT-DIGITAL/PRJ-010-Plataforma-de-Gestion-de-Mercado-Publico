# Quickstart: Validación — Ajustes Urgentes del Cliente

**Feature**: 017-ajustes-urgentes-cliente | **Date**: 2026-07-01

Guía de validación end-to-end. Contratos en [contracts/api.md](contracts/api.md); estructuras en [data-model.md](data-model.md).

## Prerequisitos

```bash
# Stack completo (API :5001, Web :8181, DB :5433) — requiere .env en el root
docker compose up --build

# O en desarrollo:
dotnet run --project src/MPM.Api        # API :5000
cd src/mpm-web && npm install && npm run dev   # Web :3000
```

Usuario de prueba: los seed de `V042`/`V070` (ver migraciones).

## Escenarios de validación

### 1. Sesión expirada → login (US1)

1. Iniciar sesión. En DevTools → Application → Local Storage, reemplazar `mpm_token` por un token inválido (o esperar la expiración real).
2. Navegar a Licitaciones o Notificaciones.
3. **Esperado**: redirección inmediata a `/login` con el aviso "Tu sesión expiró"; sin pantallas con errores 401; un solo aviso aunque varias llamadas fallen. Tras re-login, todo funciona sin residuos.

### 2. Coherencia documental y comparativa (US2)

1. Usar una licitación de prueba cuya acta declare un documento faltante que SÍ esté entre los archivos subidos al workspace.
2. Ejecutar el análisis y abrir el dashboard.
3. **Esperado**: la tarjeta "Comparativa de documentos" lista requeridos vs. enviados vs. observados con estado por documento; la inconsistencia aparece marcada (estado `inconsistente`, severidad) distinguiendo lo que dice el acta de la evidencia. Con una licitación coherente: sin falsas alarmas. Sin registro de envíos: mensaje "sin información" (no inventa).

### 3. Pantalla de Licitaciones rediseñada (US3)

1. Abrir `/licitaciones`.
2. **Esperado**: un único campo de búsqueda; no existen el toggle "Búsqueda inteligente" ni el botón "Sincronizar"; espaciado compacto (más filas visibles sin scroll). Aplicar filtros → "Reiniciar filtros" los limpia todos y recarga.
3. Datos: verificar licitaciones con fechas 2025-2026. Sync semanal: revisar log de sync (`usp_SyncLog_*`) — backfill `sync_backfill_2025` registrado y próxima ejecución programada a 7 días.

### 4. Análisis: chat en vista propia + PDF (US4)

1. Desde el dashboard de un análisis, pulsar "Abrir chat en vista completa" → `/analisis/:id/chat`.
2. **Esperado**: chat funcional con el mismo contexto; respuestas siempre en Markdown legible (sin ```fences crudos).
3. Exportar PDF desde el dashboard.
4. **Esperado**: PDF con texto seleccionable (probar copiar/pegar), tablas paginadas sin cortes ilegibles, incluye la comparativa de documentos. Probar con un análisis extenso.

### 5. Ajustes de UI (US5)

- `/login`: sin enlace "¿Olvidaste tu contraseña?".
- Sidebar: avatar + nombre + "admin TIVIT"; el correo no aparece.
- `/notificaciones`: eliminar una (desaparece, contador se actualiza) y "Borrar todas" con confirmación.
- `/catalogos`: click en "Licitación Pública", "Trato Directo" y estado "Publicada" → Drawer con explicación en lenguaje simple.
- `/ejecutivo`: revisar jerarquía visual mejorada sin pérdida de contenido.

### 6. Investigación (US6)

- **Esperado**: existe `docs/investigacion-victorias-licitaciones.md` con fuentes de datos, viabilidad, limitaciones y recomendación. `git diff` no muestra cambios de comportamiento del sistema asociados a esta US.

## Tests automatizados

```bash
# Backend: unit + integración (DELETE notificaciones, post-proceso de validación documental)
dotnet test MPM.sln
dotnet test tests/MPM.Modules.Notificaciones.Tests   # si existe; si no, ver tasks
dotnet test tests/MPM.Modules.Analisis.Tests

# Frontend E2E (401→login, licitaciones rediseñada, borrar notificaciones)
cd src/mpm-web && npm run test:e2e
```

## Criterios de cierre (mapa a Success Criteria)

| Escenario | SC |
|-----------|----|
| 1 | SC-001 |
| 2 | SC-002, SC-003 |
| 3 | SC-004, SC-005 |
| 4 | SC-006, SC-007 |
| 5 | SC-008 |
| 6 | SC-009 |
