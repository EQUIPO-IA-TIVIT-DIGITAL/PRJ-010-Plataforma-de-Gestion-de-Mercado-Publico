# Runbook Operativo: Módulo de Mensajería

## 1. SLOs del Módulo

| SLO | Métrica | Umbral | Alerta |
|-----|---------|--------|--------|
| Disponibilidad API | HTTP 5xx rate | < 1% en 5min | PagerDuty/Slack |
| Latencia mensajes | P95 enviar mensaje | < 500ms | Warning > 300ms |
| Latencia listado | P95 listar conversaciones | < 1s | Warning > 700ms |
| SignalR conexiones | Conexiones activas | < 10,000 por instancia | Warning > 8,000 |
| Storage adjuntos | Espacio disponible | > 10% libre | Critical < 5% |
| Redis backplane | Latencia pub/sub | < 50ms | Warning > 30ms |

## 2. Health Checks

### Endpoint: `GET /health`
Verifica API + DB + Redis (ya existe en el proyecto)

### Endpoint: `GET /health/mensajeria`
Verifica tablas de mensajería accesibles + SignalR hub reachable

**Implementación:**
```csharp
[HttpGet("/health/mensajeria")]
public async Task<IActionResult> HealthMensajeria()
{
    try
    {
        await using var conn = _dbFactory.Create();
        await conn.ExecuteAsync("SELECT 1 FROM conversaciones LIMIT 1");
        return Ok(new { status = "healthy", module = "mensajeria" });
    }
    catch (Exception ex)
    {
        return StatusCode(503, new { status = "unhealthy", module = "mensajeria", error = ex.Message });
    }
}
```

## 3. Gestión de Incidentes

| Severidad | Ejemplo | Respuesta | Tiempo |
|-----------|---------|-----------|--------|
| P1 - Crítico | SignalR caído, mensajes no se entregan | Rollback inmediato | < 15min |
| P2 - Alto | Latencia > 5s en mensajes | Escalar instancias | < 1h |
| P3 - Medio | Adjuntos no se pueden subir | Verificar storage | < 4h |
| P4 - Bajo | Presencia desactualizada | Reiniciar servicio presencia | < 24h |

## 4. Monitoreo Específico de Mensajería

| Señal | Fuente | Dashboard |
|-------|--------|-----------|
| Mensajes enviados/minuto | API logs | Grafana |
| Conversaciones activas | DB query | Grafana |
| Conexiones SignalR activas | Hub metrics | Grafana |
| Archivos subidos/día | API logs | Grafana |
| Mensajes no leídos acumulados | DB query | Alerta si > umbral |
| Usuarios con typing activo | Redis pub/sub | Debug |

## 5. Escalabilidad

| Componente | Estrategia |
|------------|-----------|
| API REST | Horizontal (más instancias Docker) |
| SignalR | Sticky sessions + Redis backplane (ya configurado) |
| PostgreSQL | Read replicas para queries de mensajes |
| Storage adjuntos | S3-compatible o volume mount escalable |
| Redis | Cluster mode si conexiones > 50K |

## 6. Plan de Releases

| Tipo | Frecuencia | Validación | Rollback |
|------|-----------|------------|----------|
| Feature | Quincenal | Tests integr + E2E | Revert PR |
| Hotfix | Inmediato | Tests unitarios + smoke | Revert commit |
| Migración DB | Con feature | Dry-run en staging | Script de reversa |

## 7. Versionado

| Componente | Esquema | Ejemplo |
|------------|---------|---------|
| API endpoints | URL versioning (`/api/v1/`) | Ya implementado |
| SignalR Hub | Compatible con API version | v1 |
| DB schema | Migraciones secuenciales (V013+) | Idempotentes |
| Frontend | Semver en `package.json` | 0.1.0 → 0.2.0 |

## 8. Deprecación

| Elemento | Aviso | Ventana | Migración |
|----------|-------|---------|-----------|
| Endpoint obsoleto | 2 sprints | 30 días | Nuevo endpoint + redirect |
| Campo DTO removido | 1 sprint | 15 días | Campo nuevo + deprecado marcado |
| Evento SignalR renombrado | 2 sprints | 30 días | Ambos eventos coexisten |

## 9. Métricas de Evolución

| KPI | Definición | Frecuencia |
|-----|-----------|-----------|
| Mensajes por usuario/día | Actividad promedio | Semanal |
| Conversaciones creadas/semana | Adopción | Semanal |
| % mensajes con adjuntos | Uso de feature | Mensual |
| Tiempo promedio de respuesta | Engagement | Semanal |
| Usuarios activos en chat | Adopción | Semanal |

## 10. Procedimientos de Emergencia

### SignalR caído
1. Verificar logs de SignalR hub
2. Verificar conexión Redis
3. Reiniciar instancias API
4. Si persiste, deshabilitar SignalR temporalmente (fallback a polling)

### Storage lleno
1. Verificar espacio en disco
2. Limpiar adjuntos antiguos (política de retención)
3. Escalar volumen
4. Notificar a usuarios afectados

### Base de datos lenta
1. Verificar queries con EXPLAIN ANALYZE
2. Verificar índices
3. Escalar read replicas
4. Optimizar queries problemáticas

## 11. Contactos de Escalamiento

| Rol | Contacto | Horario |
|-----|----------|---------|
| Tech Lead | manuel.aliaga@tivit.com | 24/7 |
| DevOps | devops@tivit.com | 24/7 |
| DBA | dba@tivit.com | Horario laboral |
