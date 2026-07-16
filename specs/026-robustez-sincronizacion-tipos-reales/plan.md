# Feature Implementation Plan: Robustez de Sincronización y Mapeo de Tipos Reales

Este plan detalla los componentes modificados y los pasos de verificación seguidos para implementar y validar la robustez del sincronizador y el enriquecimiento de alertas.

---

## 1. Cambios Propuestos e Implementados

### A. Módulo de Licitaciones (C#)
*   **[`ApiMpService.cs`](file:///C:/Users/menca/Desktop/CU010%20-%20Mercado%20P%C3%BAblico/src/MPM.Modules.Licitaciones/Services/ApiMpService.cs)**:
    *   Agregar `ParseTipoDesdeCodigo` para extraer el tipo de licitación real analizando el `CodigoExterno` de Mercado Público.
    *   Implementar `ValidarRespuestaJson` para analizar dinámicamente el cuerpo de las respuestas HTTP de la API oficial y lanzar una `HttpRequestException` (que simula el status 429) si Mercado Público responde con el error de peticiones simultáneas `10500`.
    *   Ajustar `MapToLicitacionRaw` para inyectar la fecha de consulta (`date`) como fallback en `fecha_publicacion`.

### B. Base de Datos (PostgreSQL)
*   **[`V105__Update_usp_SyncEngine_MergeLicitaciones_tipo_do_update.sql`](file:///C:/Users/menca/Desktop/CU010%20-%20Mercado%20P%C3%BAblico/src/MPM.Api/Database/Scripts/V105__Update_usp_SyncEngine_MergeLicitaciones_tipo_do_update.sql)**:
    *   Actualizar `usp_SyncEngine_MergeLicitaciones` para incluir `tipo` y `fecha_publicacion` en el `ON CONFLICT DO UPDATE`.
*   **[`V106__Protect_MergeLicitaciones_rich_data.sql`](file:///C:/Users/menca/Desktop/CU010%20-%20Mercado%20P%C3%BAblico/src/MPM.Api/Database/Scripts/V106__Protect_MergeLicitaciones_rich_data.sql)**:
    *   Actualizar el stored procedure para resguardar los datos ricos (usando `COALESCE` para `organismo`, `unidad_tecnica`, `monto_estimado` y `link`) e impedir que las actualizaciones masivas de la API los pisen con nulos.
    *   Proteger `raw_data` de ser sobreescrito si ya cuenta con información enriquecida (que contenga el objeto `"Comprador"`).

### C. Módulo de Alertas (C#)
*   **[`AlertasHandler.cs`](file:///C:/Users/menca/Desktop/CU010%20-%20Mercado%20P%C3%BAblico/src/MPM.Modules.Alertas/Data/AlertasHandler.cs)**:
    *   Agregar el método `ActualizarLicitacionEnCalienteAsync` para ejecutar un query de actualización directo en la base de datos local usando Dapper.
*   **[`AlertasMatchingService.cs`](file:///C:/Users/menca/Desktop/CU010%20-%20Mercado%20P%C3%BAblico/src/MPM.Modules.Alertas/Services/AlertasMatchingService.cs)**:
    *   Inyectar `IHttpClientFactory` y `IServiceProvider`.
    *   En `EvaluarUnaLicitacionAsync`, si una licitación hace match de alertas y su campo `Organismo` es nulo, llamar en caliente al endpoint de detalle oficial de la API.
    *   Guardar la información enriquecida (organismo, unidad técnica, monto estimado, descripción, raw_data) usando el handler y actualizar el record posicional en memoria `licitacion` usando la sintaxis `with`.

---

## 2. Plan de Verificación

*   **Paso 1**: Compilar el código de C# localmente para confirmar que no existan errores sintácticos ni dependencias cruzadas/circulares de proyectos.
*   **Paso 2**: Reconstruir y levantar los servicios en Docker Compose (`docker compose build api; docker compose up -d api`).
*   **Paso 3**: Verificar que las migraciones `V105` y `V106` se hayan aplicado correctamente.
*   **Paso 4**: Lanzar una sincronización de fondo y realizar un query de agrupación en PostgreSQL para validar que los tipos reales (ej: `LE`, `LP`, `LQ`) aumenten y los registros genéricos disminuyan.
*   **Paso 5**: Verificar que los links directos de la ficha se autogeneren y se guarden correctamente.
*   **Paso 6**: Confirmar que los organismos y unidades técnicas se enriquezcan y resguarden ante conflictos de base de datos.
