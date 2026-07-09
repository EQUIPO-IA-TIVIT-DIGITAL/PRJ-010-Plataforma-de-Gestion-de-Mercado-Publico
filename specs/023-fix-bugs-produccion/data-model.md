# Data Model: Corrección urgente de bugs detectados en producción

No se agregan tablas ni columnas nuevas. Se modifica el comportamiento de un stored procedure existente.

## `alertas_destinatarios` (existente, `V079__Create_Alertas.sql`)

| Columna | Tipo | Cambio |
|---|---|---|
| `id` | `BIGSERIAL PRIMARY KEY` | sin cambios |
| `usuario_id` | `VARCHAR(100) NOT NULL UNIQUE` | sin cambios |
| `telegram_chat_id` | `VARCHAR(50)` | sin cambios |
| `es_account_manager_gobierno` | `BOOLEAN NOT NULL DEFAULT FALSE` | **comportamiento de escritura corregido**: `usp_AlertasDestinatarios_GuardarChatId` ahora la setea `TRUE` en vez de dejarla en su default |

## `usp_AlertasDestinatarios_GuardarChatId` (V096, reemplaza la versión de V079)

**Antes**:
```sql
INSERT INTO alertas_destinatarios (usuario_id, telegram_chat_id)
VALUES (p_usuario_id, p_telegram_chat_id)
ON CONFLICT (usuario_id) DO UPDATE
    SET telegram_chat_id = p_telegram_chat_id, updated_at = CURRENT_TIMESTAMP;
```

**Después**:
```sql
INSERT INTO alertas_destinatarios (usuario_id, telegram_chat_id, es_account_manager_gobierno)
VALUES (p_usuario_id, p_telegram_chat_id, TRUE)
ON CONFLICT (usuario_id) DO UPDATE
    SET telegram_chat_id = p_telegram_chat_id,
        es_account_manager_gobierno = TRUE,
        updated_at = CURRENT_TIMESTAMP;
```

## `conversaciones` / `usp_Conversaciones_Crear` (existente, sin cambios de esquema ni de SQL del procedure)

El `CHECK CONSTRAINT conversaciones_tipo_check` nunca fue el problema — el frontend ya enviaba `'directo'`/`'grupal'` correctamente. El fix real de BUG-014 es de tipado de parámetros del lado del cliente Dapper/Npgsql (ver `research.md` R1):

- `src/MPM.Modules.Mensajeria/Data/ConversacionHandler.cs` — `CrearAsync` ahora especifica `dbType` explícito para `p_tipo`, `p_asunto`, `p_licitacion_id`, `p_creador_id`.
- `src/MPM.Modules.Mensajeria/Data/MensajeriaStoredProcedures.cs` — `CrearConversacion` ahora castea `@p_participante_ids::jsonb` explícitamente en el SQL del `CALL`.

No se toca `usp_Conversaciones_Crear` en sí (`V019__Create_usp_Conversaciones_Crear.sql`) — el procedure ya estaba correcto, el problema era cómo Dapper armaba los parámetros al invocarlo.
