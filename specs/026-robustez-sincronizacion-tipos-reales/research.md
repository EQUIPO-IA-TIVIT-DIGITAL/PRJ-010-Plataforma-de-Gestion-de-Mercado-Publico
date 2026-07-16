# Feature Research: Robustez de Sincronización y Mapeo de Tipos Reales

Este documento detalla los hallazgos técnicos del análisis del comportamiento de la API oficial de Mercado Público y el flujo del motor de sincronización de C#.

---

## 1. Estructura de la API Oficial de Mercado Público

### A. Endpoint de Listado Masivo Diario
*   **URL**: `https://api.mercadopublico.cl/servicios/v1/publico/licitaciones.json?ticket={ticket}&fecha={fecha}`
*   **Comportamiento**: Retorna un listado de todas las licitaciones publicadas en un día específico.
*   **Limitación de Datos**: Cada elemento del array de listado contiene **únicamente** 4 campos:
    *   `CodigoExterno`
    *   `Nombre`
    *   `CodigoEstado`
    *   `FechaCierre`
    *   *Todos los demás campos (como `Tipo`, `Comprador`, `MontoEstimado`, `Fechas.FechaPublicacion`, etc.) vienen como `null` en el JSON masivo.*
*   **Bypass de Tipo**: Para obtener el tipo real (ej: `LE`, `LP`, `LQ`) de forma nativa sin llamadas extras, analizamos la nomenclatura del `CodigoExterno`: `[CodigoComprador]-[Correlativo]-[TipoLicitacion][Anio]`. La tercera sección del string contiene el tipo real (ej: de `2153-41-LP26` se extrae **`LP`**).

### B. Endpoint de Detalle Individual
*   **URL**: `https://api.mercadopublico.cl/servicios/v1/publico/licitaciones.json?ticket={ticket}&codigo={codigo}`
*   **Comportamiento**: Retorna el JSON completo enriquecido de una sola licitación (incluyendo objeto `Comprador`, `Fechas`, `Descripcion` y `MontoEstimado`).
*   **Problema de Cuota**: Hacer esta llamada individual para cada una de las más de 70,000 licitaciones del historial consumiría la cuota diaria del ticket en pocos minutos.

---

## 2. Errores de Negocio de Mercado Público (10500)

Cuando el servidor de compras públicas detecta consultas paralelas o rate-limit por IP/Ticket, no arroja un status HTTP `429 Too Many Requests`. En su lugar, responde con un código **`200 OK`** cuyo cuerpo es el siguiente JSON de error de negocio:
```json
{
  "Codigo": 10500,
  "Mensaje": "Lo sentimos. Hemos detectado que existen peticiones simultáneas."
}
```
*   **Impacto**: Al responder con HTTP 200, el cliente original de C# no lanzaba excepciones de red, deserializaba el JSON y retornaba un listado nulo. Esto provocaba que el sincronizador marcara el día completo falsamente como "sin datos" y se saltara las licitaciones de ese periodo.
*   **Corrección**: Añadimos la función `ValidarRespuestaJson` en C# para interceptar campos `Codigo > 200` y lanzar una `HttpRequestException` (simulando status 429) para activar los reintentos automáticos programados con retraso.

---

## 3. Arquitectura del Enriquecimiento de Alertas y Acoplamiento

Para poblar el Organismo, la Unidad Técnica y el Monto de las licitaciones relevantes (las que activan alertas comerciales):
*   **Desafío**: La clase `AlertasMatchingService` pertenece al módulo de `Modules.Alertas` y no tiene referencias de proyecto al módulo `Modules.Licitaciones` para evitar dependencias circulares (prohibidas en .NET).
*   **Solución**: 
    1. Inyectamos `IHttpClientFactory` y `IConfiguration` directamente en `AlertasMatchingService.cs` para hacer consultas HTTP directas e independientes al endpoint de detalle de la API oficial de Mercado Público.
    2. Agregamos el método `ActualizarLicitacionEnCalienteAsync` en `AlertasHandler.cs` (módulo de Alertas) para realizar una actualización SQL directa en la tabla de PostgreSQL mediante Dapper.
    3. Usamos la sintaxis `with` de C# en el record posicional `LicitacionParaMatching` para propagar los nuevos datos en memoria durante el flujo del motor de alertas.
