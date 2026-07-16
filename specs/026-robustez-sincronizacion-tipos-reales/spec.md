# Feature Specification: Robustez de Sincronización y Mapeo de Tipos Reales por API

**Feature Branch**: `026-robustez-sincronizacion-tipos-reales`

**Created**: 2026-07-15

**Status**: Approved

**Input**: User requests: 
1. "puede ser desde el 1ro de enero de 2025? nesesitamos todos... y el segundo scrapper general..."
2. "la base de C# agrupa bajo Licitacion... hay forma de filtrar esto para tener datos reales?"
3. "frecuencia de ejecucion: diario a la medianoche filtrando dia anterior..."
4. "se ha perdido: Organismo, Unidad Tecnica, Link para ver directo en mercado publico..."
5. "enriquecer en caliente via API de detalle solo las licitaciones de interes..."

## Contexto — qué existe hoy

La sincronización histórica y diaria en C# (`SyncEngineService.cs`) descarga de la API oficial de Mercado Público el listado diario mediante `servicios/v1/publico/licitaciones.json?ticket={ticket}&fecha={fecha}`. 

Dado que el JSON de listado diario de la API oficial es extremadamente minimalista (solo incluye `CodigoExterno`, `Nombre`, `CodigoEstado` y `FechaCierre`), el backend original:
- Hardcodeaba el campo `tipo` a la palabra genérica `"Licitacion"`.
- Insertaba campos enriquecidos (`organismo`, `unidad_tecnica`, `monto_estimado`, `link`) como `null` en la base de datos PostgreSQL.
- En el `ON CONFLICT DO UPDATE` de `MergeLicitaciones`, sobreescribía la columna `raw_data` con este JSON resumido, lo cual borraba la información estructurada que el Scraper de Playwright ya había extraído para las licitaciones en las que TIVIT ofertaba.
- Si la API oficial arrojaba un error de negocio del portal (como peticiones simultáneas, Código `10500`), el cliente HTTP no arrojaba excepción (ya que responde con status HTTP 200), interpretando erróneamente el día completo como "sin datos" y saltándoselo en silencio.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mapeo de Tipos de Licitación Reales de Mercado Público (Priority: P1)
Un usuario consulta el buscador de licitaciones y en la columna/filtro de "Tipo" ve los códigos oficiales reales del portal (ej: `LE`, `LP`, `LQ`, `LR`, `CO`, `CA`, `TD`) en lugar del texto genérico `"Licitacion"`.
*   **Why this priority**: Permite a los analistas de negocio filtrar por tamaño y tipo de compra pública real de forma inmediata.
*   **Independent Test**: Consultar las licitaciones de 2025/2026 en PostgreSQL agrupadas por tipo, verificando que existan miles de registros mapeados bajo abreviaciones oficiales en lugar de `"Licitacion"`.

### User Story 2 - Resguardo de Datos Ricos de Playwright en Actualizaciones (Priority: P1)
Las licitaciones enriquecidas previamente por el Scraper de Playwright (con adjuntos y ofertas de competidores) mantienen intactos sus datos (organismo, unidad técnica y link directo) tras las corridas del sincronizador de C# de la API masiva.
*   **Why this priority**: Evita la pérdida regresiva de datos de inteligencia competitiva crítica recopilados mediante web scraping.
*   **Independent Test**: Modificar el stored procedure para aplicar `COALESCE` en el `ON CONFLICT DO UPDATE` y evaluar mediante un sync de prueba que los campos ricos y el `raw_data` enriquecido no se borren en PostgreSQL.

### User Story 3 - Enriquecimiento en Caliente para Alertas (Priority: P2)
Cuando una licitación general (API masiva) hace match con una alerta activa de TIVIT (ej: palabra clave "valvulas"), el backend gatilla de forma automática una petición individual a la API de detalle para completar su Organismo, Unidad Técnica, Descripción y Monto en caliente, permitiendo que la notificación al Account Manager llegue 100% llena.
*   **Why this priority**: Brinda una experiencia de usuario rica y detallada para las licitaciones de interés comercial, sin quemar las cuotas de la API oficial para el resto de licitaciones.
*   **Independent Test**: Probar una alerta de prueba en local y confirmar que el registro insertado en base de datos y la notificación por email contengan el nombre del organismo real.

### User Story 4 - Robustez ante el Error de Concurrencia de la API (10500) (Priority: P2)
El worker no se salta días por errores temporales de peticiones simultáneas (`10500`). El cliente HTTP intercepta este error de negocio y lo procesa como una excepción de reintento progresivo programado.
*   **Why this priority**: Garantiza la completitud absoluta de las licitaciones del backfill histórico.
*   **Independent Test**: Simular un error `10500` en la respuesta JSON y comprobar que C# activa la cola de reintentos con retraso en vez de loguear "Día X: sin datos".

### Edge Cases

*   **Códigos de Estado No Estándar (Violación de Clave Foránea)**: Cuando la API oficial de Mercado Público reporta códigos de estado de licitación inexistentes en la tabla local de catálogo `estados_licitacion` (ej. códigos temporales u ocultos), la clave foránea `licitaciones_codigo_estado_fkey` bloqueará el lote completo de inserción. Para evitar esto, el stored procedure valida la existencia del código en la tabla `estados_licitacion` y asigna `1` (Publicada) como fallback silencioso.

---

## Requirements *(mandatory)*

*   **FR-001**: El sistema DEBE extraer el tipo real del código externo de la licitación (ej: de `2153-41-LP26` extraer `LP`).
*   **FR-002**: El stored procedure `usp_SyncEngine_MergeLicitaciones` DEBE usar `COALESCE` en el `ON CONFLICT DO UPDATE` para no sobreescribir con nulos los campos estructurados enriquecidos.
*   **FR-003**: El stored procedure DEBE proteger `raw_data` de no ser pisado si ya contiene la sección `"Comprador"` del scraper.
*   **FR-004**: El backend de Alertas DEBE enriquecer en caliente vía API de detalle individual aquellas licitaciones que activen alertas, actualizando PostgreSQL y el DTO en memoria.
*   **FR-005**: `ApiMpService.cs` DEBE validar el cuerpo del JSON y arrojar una excepción simulando status 429 si detecta un campo `Codigo` mayor a 200 (como `10500`).
*   **FR-006**: El worker de sincronización general DEBE configurarse para ejecución diaria de medianoche con una ventana deslizante de 3 días.
*   **FR-007**: El stored procedure DEBE validar que el `codigo_estado` del lote exista en `estados_licitacion`, aplicando un fallback defensivo al código `1` (Publicada) para evitar fallos de clave foránea.

---

## Success Criteria *(mandatory)*

*   **SC-001**: Las licitaciones del buscador de TIVIT reflejan sus tipos oficiales de Mercado Público y contienen su link de acceso directo autogenerado.
*   **SC-002**: El backfill histórico desde el `01-01-2025` se ejecuta de forma desatendida en background en PostgreSQL sin saltarse días por errores `10500`.
*   **SC-003**: Las alertas gatilladas a los usuarios de TIVIT por email/Telegram contienen el Organismo y la Unidad Técnica correspondientes.

---

## Glosario de Tipos de Licitación (para uso en Frontend)

A continuación se detalla la clasificación oficial del portal de compras públicas de Chile (ChileCompra) que se debe utilizar para renderizar leyendas, tooltips, etiquetas o modales en la interfaz de React:

| Código | Modalidad / Nombre Oficial | Rango de Monto Estimado (UTM) | Descripción y Uso en Frontend |
| :---: | :--- | :--- | :--- |
| **CA** | **Compra Ágil** | $\le$ 30 UTM | Proceso simplificado de cotizaciones en línea. Dirigido a compras de menor cuantía. |
| **TD** | **Trato Directo** | Excepcional | Contratación directa sin licitación por causales fundadas en la ley (ej. emergencia, exclusividad). |
| **CO** | **Convenio Marco** | Catálogo Directo | Tienda virtual de ChileCompra donde los organismos adquieren directamente de un catálogo pre-adjudicado. |
| **LE** | **Licitación Pública Menor** | < 100 UTM | Convocatoria pública abierta para contratos menores a 100 UTM. |
| **LP** | **Licitación Pública Media** | 100 - 1.000 UTM | Convocatoria estándar para contratos medianos de bienes o servicios. |
| **LQ** | **Licitación Pública Mayor** | 1.000 - 2.000 UTM | Convocatoria pública para contratos de volumen intermedio alto. |
| **LR** | **Licitación Pública Grande** | > 2.000 UTM | Convocatorias públicas complejas para grandes contratos gubernamentales (alta cuantía). |
| **LS** | **Licitación de Servicios** | Variable | Contratación de servicios de consultoría, asesorías de software, auditorías o servicios profesionales. |
| **L** / **B** / **R** | **Obras Públicas / Suministros** | Variable | Licitaciones enfocadas en infraestructura pública (vial, portuaria) o suministro de insumos complejos. |
| **E** / **I** | **Especiales / Internacionales** | Variable | Convocatorias de organismos multilaterales o licitaciones con bases especiales de financiamiento. |

