# Tasks: Propuestas

**Spec:** `docs/api-first/propuestas.md`  
**Diseño origen:** `docs/design/flujo-ofertas.md`, Fase 3, D9, D7.5, D7.6 y D7.11  
**Specs relacionadas:** `docs/api-first/censo.md`, `docs/api-first/decisiones.md`  
**Rama:** `036-flujo-comercial-ofertas`  
**Estado:** spec aprobada por negocio  
**Modo:** bundles ejecutables por delivery-agent

## Convenciones de implementación

- Backend real: solución .NET 8 en `MPM.sln`, módulos bajo `src/MPM.Modules.*`, Dapper y
  migraciones SQL embebidas desde `src/MPM.Api/Database/Scripts/*.sql`.
- Respuestas HTTP: reutilizar `ApiResponse<T>` y el middleware de errores existente. No crear
  un formato paralelo para Propuestas.
- Frontend real: React + Ant Design + TanStack Query en `src/mpm-web/src/`; las pruebas de
  navegador viven en `src/mpm-web/e2e/`.
- Storage: reutilizar `IStorageService`, `GcsStorageService` y `LocalStorageService`. La ruta
  persistida debe ser consumible por `DownloadAsync` en ambos modos.
- Catálogos de experiencias, certificaciones y capítulos: corporativos globales de TIVIT, no
  filtrados por tenant. La licitación y sus propuestas deben seguir el aislamiento y el tenant
  resuelto por el pipeline actual.
- La generación no puede embebir nombres, emails ni otros datos de personas de Census. Sólo se
  incorporan texto de catálogo y PDFs de certificaciones.

## Correcciones de consistencia que delivery debe aplicar

1. `V145__Propuestas.sql` es la siguiente migración disponible después de `V144`, no una
   modificación de `V143` o `V144` ya aplicadas.
2. El script actual `V144__Decisiones_GO_NO_GO.sql` ya captura el estado real de la licitación
   con fallback `1`. El borrador `docs/design/tasks-fase2.md` lo marca como pendiente, pero no
   debe repetirse esa corrección en este lote.
3. El `CensusClient` actual ya expone `DownloadCertificationFileAsync`. La sincronización debe
   extenderlo para `intellectual-capital/user-certifications` y reutilizar su token manager,
   retry ante 401 y configuración; no crear un segundo cliente Census.
4. La función actual `usp_LicitacionesDecision_Obtener` no devuelve `notificados`, aunque
   `DecisionHandler.DecisionRow` ya tiene esa propiedad. `V145` debe reemplazar la función,
   incluyendo `notificados` y `notificado_at`, y actualizar el mapeo de dominio.
5. El frontend ya monta `CapacidadesTIVITPanel` y `DecisionGoNoGoPanel` desde
   `src/mpm-web/src/components/LicitacionDetailDrawer.tsx`, y los hooks de decisión están en
   `src/mpm-web/src/hooks/useCenso.ts`. Extender o extraer esos contratos sin duplicar la
   decisión GO/NO GO ni crear otra vista paralela.
6. `MPM.Modules.Notificaciones` almacena `tipo` como texto y no tiene un catálogo SQL de tipos.
   `decision_avisada` se incorpora como constante/contrato y mediante `NotificacionesService`;
   no crear una tabla de tipos ni una mensajería nueva.
7. El análisis actual no ofrece un DTO compartido para todos los requisitos comerciales. La
   recomendación debe leer el análisis V142 mediante su handler/consulta existente o un contrato
   explícito, sin asumir que `CensoHandler.AnalisisRequisitosAsync` ya entrega tecnologías e
   industria.

## Dependencia general

Todos los bundles parten de la spec aprobada y de las migraciones V143/V144 ya presentes. El
Bundle D no tiene trabajo de implementación porque Drive está explícitamente diferido.

## Bundle A: catálogos y sincronización de certificaciones Census

### A1. Crear la base de datos V145 y las semillas corporativas

**Depends on:** none  
**Estimate:** 4h  
**Work:**

- Crear `src/MPM.Api/Database/Scripts/V145__Propuestas.sql` con las tablas
  `catalogo_experiencias`, `catalogo_certificaciones`, `catalogo_capitulos` y `propuestas`.
- Añadir PK, índices, soft delete, UK de certificación normalizada y UK
  `(licitacion_id, version)` según `propuestas.md`.
- Añadir `notificado_at` a `licitaciones_interes` con `IF NOT EXISTS`.
- Crear los stored procedures/functions de los tres catálogos, sincronización Census, propuestas
  y transiciones de estado. Mantener nombres y parámetros de la sección 6 de la spec.
- Sembrar los 10 capítulos canónicos en orden 1-10 de la base PRJ-001, con carátula fija `TIVIT`.
  La semilla debe ser idempotente y no incluir datos personales.
- Recrear en V145 `usp_LicitacionesDecision_Obtener` con `notificados` y `notificado_at` en su
  resultado, eliminando primero la firma anterior si PostgreSQL no permite cambiar la tabla de
  retorno con `CREATE OR REPLACE`.
- Mantener la migración compatible con el `DatabaseInitializer`, que ordena y registra recursos
  por versión sin transacción global.

**Verify:** Añadir pruebas de contrato SQL que comprueben la presencia del recurso V145, las cuatro
tablas, constraints, las 10 semillas únicas por orden y los campos de decisión. Ejecutar la
migración sobre PostgreSQL de pruebas dos veces y verificar que la segunda ejecución no duplica
ni destruye la semilla.

**Trace:** `propuestas.md` §2, §3, §6 y notas de consistencia 1, 4, 5; `decisiones.md` §2 y nota 2.

### A2. Crear el módulo Propuestas y conectarlo a la solución

**Depends on:** A1  
**Estimate:** 3h  
**Work:**

- Crear `src/MPM.Modules.Propuestas/MPM.Modules.Propuestas.csproj` siguiendo el patrón de
  `MPM.Modules.Colaboracion`, con referencias a `MPM.Core`, `MPM.Shared`,
  `MPM.Modules.Licitaciones`, `MPM.Modules.Censo`, `MPM.Modules.Colaboracion` y
  `MPM.Modules.Notificaciones` sólo donde sean necesarias.
- Crear `ModuleRegistration.cs`, modelos DTO iniciales, `Data/PropuestasHandler.cs` y
  `Data/PropuestasStoredProcedures.cs`.
- Registrar el proyecto en `MPM.sln`, agregar el `ProjectReference` en
  `src/MPM.Api/MPM.Api.csproj` y el `COPY` del csproj en `src/MPM.Api/Dockerfile`.
- Registrar `AddPropuestasModule` en el host web de `src/MPM.Api/Program.cs`, después de los
  módulos de los que dependa. No registrar un worker nuevo: la generación de esta spec es
  síncrona por HTTP.
- Crear `tests/MPM.Modules.Propuestas.Tests/MPM.Modules.Propuestas.Tests.csproj` con xUnit,
  Moq y FluentAssertions, y añadirlo a `MPM.sln`.

**Verify:** `dotnet build MPM.sln` y `dotnet test tests/MPM.Modules.Propuestas.Tests/` pasan con
el contenedor de DI levantado. Verificar que el arranque no crea un registro duplicado del módulo
ni rompe el arranque del worker existente.

**Trace:** `propuestas.md` §1, §5 y §6; estructura real de `MPM.sln`, `Program.cs` y `Dockerfile`.

### A3. Implementar CRUD de los tres catálogos corporativos

**Depends on:** A2  
**Estimate:** 4h  
**Work:**

- Implementar `src/MPM.Modules.Propuestas/Services/PropuestasCatalogoService.cs` y los DTOs
  en `src/MPM.Modules.Propuestas/Models/PropuestasDtos.cs`.
- Implementar en `Data/PropuestasHandler.cs` las operaciones paginadas de experiencias,
  certificaciones y capítulos, con `q`, `activo`, `conArchivo`, `page` y `size`.
- Crear `src/MPM.Modules.Propuestas/Controllers/PropuestasCatalogosController.cs` con las
  rutas `/api/v1/propuestas/catalogos/...` y `[Authorize]`.
- Aplicar lectura para cualquier JWT y escritura sólo para `Admin` o `SuperAdmin`, sin usar el
  tenant como filtro del contenido corporativo global.
- Normalizar el nombre de certificación de forma determinista en create/sync/update, conservar
  `file_id_census = NULL` cuando no haya archivo y usar soft delete para DELETE.
- Mapear `PRO_001`, `PRO_002`, `VAL_001`, `AUTH_001`, `AUTH_002` y `SYS_001` al contrato actual.

**Verify:** Pruebas unitarias de handler/service y pruebas de controller cubren paginación,
filtros, roles, soft delete, duplicado normalizado de certificación y actualización manual de
`fileIdCensus`. Verificar que el catálogo corporativo no se filtra por tenant, que el request sí
entra con tenant válido y que las respuestas usan `ApiResponse<T>`.

**Trace:** `propuestas.md` §5, catálogos, PRO-R004 a PRO-R007.

### A4. Sincronizar certificaciones desde `user-certifications` de Census

**Depends on:** A3  
**Estimate:** 4h  
**Work:**

- Extender `src/MPM.Modules.Censo/Services/CensusClient.cs` con una operación autenticada para
  `GET /intellectual-capital/user-certifications`, usando la configuración `Censo:*` y el
  `CensusTokenManager` existente.
- Mapear el payload externo a un DTO interno acotado: nombre normalizado, primer `fileId`
  representativo, institución y vigencia si están presentes. No persistir nombres, emails ni
  perfiles de personas de Census.
- Implementar `CensusCertificationSyncService` dentro de
  `src/MPM.Modules.Propuestas/Services/` y el batch upsert mediante
  `usp_CatalogoCertificaciones_SincronizarCensus`. No eliminar filas manuales que no aparezcan
  en Census.
- Exponer `POST /api/v1/propuestas/catalogos/certificaciones/sincronizar-census` con rol
  `Admin,SuperAdmin`, semáforo o streaming apropiado para el payload aproximado de 5,2 MB,
  métricas `procesadas`, `insertadas`, `actualizadas`, `sinArchivo` y `durationMs`.
- Mantener `CEN_002` para fallo persistente de red/auth y registrar logs sin secretos ni payload
  personal completo.

**Verify:** Probar con fixture de payload grande, nombres equivalentes (`ISO/IEC 27001`,
`ISO 27001`, `27001`), múltiples personas para una certificación, certificaciones sin archivo,
filas manuales preservadas y retry de 401. Un test HTTP verifica `AUTH_002` para un usuario sin
rol y que ningún email/nombre de persona se escribe en `catalogo_certificaciones`.

**Trace:** `flujo-ofertas.md` D7.5, D7.11; `propuestas.md` §3, endpoint de sincronización,
PRO-R006 y PRO-R007.

### A5. Implementar recomendaciones de certificaciones y experiencias

**Depends on:** A4  
**Estimate:** 4h  
**Work:**

- Crear `src/MPM.Modules.Propuestas/Services/PropuestasRecomendacionService.cs` y el endpoint
  `POST /api/v1/propuestas/recomendaciones`.
- Resolver requisitos desde el body con precedencia sobre el último análisis comercial V142;
  si no hay ninguno, devolver `PRO_004`.
- Calcular certificaciones sin LLM mediante canonicalización y substring normalizado, aplicando
  exactamente los umbrales `0.8`, `0.5` y `0.3`. No persistir la recomendación.
- Incorporar el prompt adaptado en un recurso propio del módulo, por ejemplo
  `src/MPM.Modules.Propuestas/Prompts/experience_relevance.txt`, y llamar al proveedor mediante
  `LlmClientResolver`, nunca mediante Gemini directo. Validar JSON, ids del catálogo, scores
  entre 0 y 1 y omitir resultados menores a `0.3`.
- Devolver `PRO_006` cuando el catálogo activo requerido esté vacío y mantener `motivo` sólo
  para experiencias. Añadir límites de tamaño de requisitos y catálogo para evitar requests
  desproporcionados.

**Verify:** Tests unitarios cubren precedencia body/análisis, scoring determinístico, cada borde
de umbral, catálogo vacío, respuesta LLM inválida, experiencia desconocida, no persistencia y
selección de proveedor a través de `LlmClientResolver`. Un test de integración verifica el
contrato JSON completo de la respuesta.

**Trace:** `propuestas.md` endpoint de recomendaciones, PRO-R008 a PRO-R010; `flujo-ofertas.md`
D5, D7.11.

## Bundle B: plantilla, generador DOCX y versionado

### B1. Incorporar y validar la plantilla corporativa

**Depends on:** A2  
**Estimate:** 3h  
**Work:**

- Incorporar la plantilla aprobada como
  `src/MPM.Api/Templates/tivit_proposal_template.docx`, obtenida de la base PRJ-001 y adaptada
  sólo después de validar su versión oficial con negocio.
- Configurar el build/publish para que el archivo llegue al runtime sin depender del directorio
  de trabajo actual. No almacenarlo en GCS ni copiarlo a Drive.
- Crear un `ProposalTemplateProvider` que resuelva la plantilla de forma segura y diferencie
  `PRO_010` de un fallo de procesamiento `PRO_009`.
- Revisar el documento base y retirar cualquier dato real de personas, clientes no autorizados o
  credenciales antes de incorporarlo al repositorio.

**Verify:** Test de existencia y lectura como ZIP OpenXML desde el directorio de ejecución, test
de publicación Docker/.NET que confirme el archivo en la imagen y test de plantilla ausente que
devuelva `PRO_010`. La revisión de contenido confirma carátula `TIVIT` y ausencia de datos
personales de Census.

**Trace:** `propuestas.md` PRO-R001, PRO-R002, PRO-R003 y notas 6, 9.

### B2. Construir el renderizador de los 10 capítulos

**Depends on:** A3, B1  
**Estimate:** 4h  
**Work:**

- Implementar `src/MPM.Modules.Propuestas/Services/DocxProposalGenerator.cs` usando una
  librería OpenXML seleccionada y registrada como decisión técnica de delivery.
- Copiar la plantilla por generación, reemplazar marcadores o bloques definidos por la plantilla
  y emitir los capítulos ordenados por `catalogo_capitulos.orden`.
- Aplicar defaults: todos los capítulos activos cuando `capitulosIds` es omitido; certificaciones
  y experiencias vacías cuando sus listas son omitidas. Completar capítulo 3 con
  `resumen_ejecutivo` V142 si existe.
- Renderizar capítulos 4 y 5 desde snapshots de catálogo seleccionados, no desde una nueva
  consulta implícita a Census. Mantener la carátula con texto fijo `TIVIT`.
- Usar nombres de archivo y contenido escapados, sin aceptar rutas provenientes del request.

**Verify:** Test que abre el DOCX generado como paquete ZIP, comprueba los 10 títulos en el orden
canónico, defaults y selección explícita. Test de seguridad busca emails, nombres de personas de
Census y contenido de la respuesta cruda de Census en todos los XML del documento.

**Trace:** `propuestas.md` §3 capítulo semilla, POST generar y PRO-R002, PRO-R003, PRO-R013.

### B3. Descargar e inyectar PDFs de certificación con resiliencia

**Depends on:** A4, B2  
**Estimate:** 4h  
**Work:**

- Implementar el componente de anexado de certificaciones usando
  `CensusClient.DownloadCertificationFileAsync` y un semáforo de máximo 4 descargas
  concurrentes.
- Definir antes de codificar la representación compatible con Word para un PDF dentro del DOCX:
  objeto PDF embebido, anexo conservando bytes originales u otra alternativa aprobada. La
  elección debe quedar documentada en el PR del delivery porque la conversión DOCX a PDF está
  fuera de alcance.
- Para cada certificación con `file_id_census`, preservar el PDF original como parte del
  documento y/o anexo según la decisión, junto con el texto del catálogo. Si la descarga falla,
  incluir la certificación como texto y una advertencia visible, sin abortar toda la generación.
- Contabilizar `certificacionesSinPdf`, diferenciar fallo de Census (`CEN_002`) de fallo de
  OpenXML (`PRO_009`) y no registrar bytes de documentos en logs.

**Verify:** Tests con cuatro o más descargas confirman el límite de concurrencia, PDF válido,
`fileId` inválido, timeout, respuesta no-PDF y fallo parcial. Se verifica que el DOCX final sigue
siendo legible, contiene la advertencia y que `certificacionesSinPdf` coincide con el resultado.

**Trace:** `flujo-ofertas.md` D7.5; `propuestas.md` POST generar, PRO-R015 y nota 10.

### B4. Generar, almacenar y versionar la propuesta

**Depends on:** A5, B3  
**Estimate:** 4h  
**Work:**

- Implementar `PropuestaService` y el endpoint
  `POST /api/v1/licitaciones/{codigoExterno}/propuestas/generar`.
- Resolver la licitación y exigir decisión `go` vigente antes de generar. Devolver `LIC_001`,
  `PRO_003`, `PRO_002` o `PRO_006` según la spec.
- Validar que todos los ids existan y estén activos, tomar snapshots JSONB de capítulos,
  certificaciones y experiencias, y obtener `version = max(version) + 1` dentro de una operación
  segura frente a requests concurrentes. La UK `(licitacion_id, version)` es la última barrera.
- Generar en memoria, subir con `IStorageService` a una ruta estable como
  `propuestas/{licitacionId}/v{version}`, y guardar `ruta_archivo`, `generado_por`,
  `generado_at` y `estado = generada`. Si falla la persistencia después de subir, limpiar el
  objeto o dejar un log operativo recuperable sin exponerlo al cliente.
- Mantener la selección humana: nunca auto-rellenar certificaciones o experiencias sólo porque
  fueron recomendadas.

**Verify:** Tests de service/controller cubren decisión `go` y `no_go`, catálogo inactivo,
versiones 1 y N, dos generaciones concurrentes, storage local/GCS simulado, rollback de upload,
usuario tomado del JWT y respuesta con resumen y `rutaDescarga`. Un test confirma que no se puede
generar sin decisión formal.

**Trace:** `propuestas.md` endpoint generar, state flow, PRO-R011 a PRO-R015 y §7 DTOs.

### B5. Exponer archivo, historial y transiciones de estado

**Depends on:** B4  
**Estimate:** 3h  
**Work:**

- Crear o completar `PropuestasController` con:
  `GET /api/v1/licitaciones/{codigoExterno}/propuestas/{propuestaId}/archivo`,
  `GET /api/v1/licitaciones/{codigoExterno}/propuestas` y
  `PATCH /api/v1/licitaciones/{codigoExterno}/propuestas/{propuestaId}/estado`.
- Descargar mediante `IStorageService`, validar que el id pertenece al código externo y devolver
  `application/vnd.openxmlformats-officedocument.wordprocessingml.document` con el nombre
  `Propuesta_{codigo}_v{version}.docx`.
- Implementar historial paginado, filtro por estado y orden más reciente primero.
- Implementar únicamente las transiciones `generada -> enviada|descartada` y
  `enviada -> descartada`; devolver `PRO_008` para cualquier otra.
- Aplicar `[Authorize]`, tenant resuelto, `PRO_001`, `LIC_001` y protección contra path
  traversal o descarga de una ruta ajena a la propuesta.

**Verify:** Tests de API cubren binario y headers, propuesta inexistente/sin archivo, código
externo que no coincide, historial paginado, cada transición válida e inválida y descarga de
versiones anteriores. El test abre el stream devuelto y confirma magic/ZIP DOCX.

**Trace:** `propuestas.md` endpoints de archivo, historial y estado; state flow §4 y PRO_001,
PRO_008.

## Bundle C: avisos GO/NO GO e integración frontend

### C1. Completar el contrato de decisión para avisos

**Depends on:** A1, A2  
**Estimate:** 4h  
**Work:**

- Consumir la corrección de V145 para que `usp_LicitacionesDecision_Obtener` devuelva
  `notificados` y `notificado_at`; la función y el cambio de firma se crean una sola vez en A1.
- Actualizar `src/MPM.Modules.Colaboracion/Data/DecisionHandler.cs`, `DecisionDto.cs` y
  `DecisionService.cs` para mapear `notificado_at` y deserializar JSONB sin perder compatibilidad
  con filas antiguas `NULL`.
- Añadir el contrato `decision_avisada` en el módulo de avisos, sin nuevo catálogo SQL, y
  preparar un servicio/adaptador testeable que delegue en el `NotificacionesService` existente.
- No modificar la semántica de decisión humana, snapshot IA, re-decisión o motivo obligatorio
  de NO GO.

**Verify:** Regresión de `tests/MPM.Modules.Colaboracion.Tests/Services/DecisionServiceTests.cs`
y prueba de mapeo con `notificados = NULL` y JSONB válido. Un test de migración comprueba que la
nueva función puede ser llamada por el handler anterior y por el contrato enriquecido.

**Trace:** `decisiones.md` §2, DEC-R008 a DEC-R010; `propuestas.md` §4, nota 1; código actual de
`DecisionHandler` y `DecisionService`.

### C2. Implementar el endpoint de avisos a personas elegidas

**Depends on:** C1, B5  
**Estimate:** 4h  
**Work:**

- Implementar `POST /api/v1/licitaciones/{codigoExterno}/decision/{decisionId}/avisar` en el
  módulo Propuestas, validando licitación, decisión `go|no_go` y correspondencia del
  `decisionId` con la fila `licitaciones_interes`.
- Validar 1-50 emails, formato, trim y duplicados; devolver `PRO_007`, `PRO_011` o `PRO_012`
  según corresponda.
- Crear una notificación in-app por destinatario mediante `NotificacionesService.CrearAsync`,
  con tipo `decision_avisada`, título/mensaje claros y metadata `codigoExterno`, `decision` y un
  identificador de lote.
- Sólo después de completar el envío lógico, reemplazar `notificados` y escribir
  `notificado_at` mediante `usp_LicitacionesDecision_ActualizarNotificados`. Re-avisar reemplaza
  la lista y vuelve a enviar, sin interpretar la lista como broadcast.
- Definir y probar el comportamiento ante fallo parcial: no responder éxito con una lista no
  persistida; registrar el lote y devolver error controlado sin filtrar destinatarios.

**Verify:** Tests de servicio/API cubren GO y NO GO, lista vacía, email inválido, límite 50,
decision inexistente, `decisionId` ajeno, re-aviso que reemplaza la lista, metadata por usuario,
fallo del servicio de notificaciones y actualización de `notificado_at`. Prueba de integración
confirma que no se notifica a usuarios no seleccionados.

**Trace:** `propuestas.md` endpoint avisar, state flow de avisos, PRO-R016 a PRO-R018;
`notificaciones.md` §2 y §4.

### C3. Añadir tipos y hooks frontend sin duplicar contratos existentes

**Depends on:** A5, B5, C2  
**Estimate:** 3h  
**Work:**

- Crear `src/mpm-web/src/types/propuestas.ts` con DTOs de catálogos, recomendación, generación,
  historial, estado y avisos. Exportarlos desde `src/mpm-web/src/types/index.ts` si corresponde.
- Crear `src/mpm-web/src/hooks/usePropuestas.ts` para catálogos, recomendaciones, generación,
  archivo, historial y estado.
- Extraer a `src/mpm-web/src/hooks/useDecisiones.ts` o extender cuidadosamente
  `useCenso.ts` para el POST/GET de decisión y avisos, evitando dos cache keys para la misma
  decisión.
- Reutilizar `apiClient.ts`, `ApiResponse` y la codificación existente de `codigoExterno`.
- Modelar errores `PRO_003`, `PRO_006`, `PRO_007`, `PRO_008`, `PRO_010`, `PRO_011` y `PRO_012`
  para que la UI muestre acciones recuperables.

**Verify:** `npm run build` pasa con TypeScript estricto. Tests de contrato del hook, usando
mock/fake del `apiClient`, comprueban rutas, payloads, invalidación de caché después de generar,
cambiar estado o avisar, y que los hooks existentes de Censo/decisión siguen funcionando.

**Trace:** `propuestas.md` §5 y §7; estructura actual de `src/mpm-web/src/hooks/useCenso.ts`,
`types/licitacion.ts` y `lib/apiClient.ts`.

### C4. Integrar selección, generación, historial y avisos en la ficha

**Depends on:** C3  
**Estimate:** 4h  
**Work:**

- Crear `src/mpm-web/src/components/PropuestaPanel.tsx` y, si se mantiene separado,
  `DecisionAvisarModal.tsx`.
- Integrar el panel en `LicitacionDetailDrawer.tsx` después de la decisión actual, mostrando
  recomendaciones como sugerencias seleccionables, los 10 capítulos activos, certificaciones
  con/sin archivo y experiencias manuales.
- Habilitar generación sólo con GO y selección explícita del usuario. Mostrar versión, estado,
  conteos, advertencias de PDFs y botón de descarga mediante el endpoint binario.
- Mostrar historial con acciones de marcar enviada/descartada según la matriz de estados.
- En GO y NO GO, permitir seleccionar manualmente emails de personas visibles en el match Census
  y añadir destinatarios válidos; el modal debe distinguir recomendación IA de decisión humana.
- Mantener Ant Design y los patrones visuales existentes; no crear una página paralela ni
  controles que no llamen a un endpoint real.

**Verify:** Añadir `src/mpm-web/e2e/specs/propuestas.spec.ts` cubriendo: catálogo vacío con
mensaje accionable, recomendación, selección humana, bloqueo sin GO, generación y descarga,
historial/versionado, aviso GO y aviso NO GO con motivo ya registrado. Verificar con un click real
que cada botón cambia estado o muestra error del backend.

**Trace:** `propuestas.md` state flow y endpoints de recomendaciones/generación/avisos;
`flujo-ofertas.md` §2, §3 D9 y §11.7; componentes actuales del drawer.

### C5. Cerrar el slice end-to-end de Propuestas

**Depends on:** B5, C2, C4  
**Estimate:** 4h  
**Work:**

- Añadir `tests/MPM.Tests/Integration/PropuestasApiTests.cs` o ampliar el proyecto de integración
  existente para cubrir el flujo completo con PostgreSQL y storage local controlado.
- Ejecutar una prueba con una licitación real o fixture equivalente: decisión GO, recomendación,
  generación v1, descarga DOCX, generación v2, historial, cambio de estado y aviso a dos
  destinatarios.
- Ejecutar la variante NO GO: motivo obligatorio, aviso permitido, generación rechazada con
  `PRO_003`.
- Inspeccionar el DOCX como paquete OpenXML, verificar capítulos, anexos/fallback, ausencia de
  datos personales Census y `ruta_archivo` resoluble.
- Ejecutar `dotnet test MPM.sln`, `dotnet build MPM.sln` y `npm run build`; corregir contratos,
  wiring, documentación XML y selectores que fallen. No añadir dependencias ni configuración de
  Google Drive.

**Verify:** El slice pasa en entorno local reproducible, los tests de backend y Playwright están
verdes, la migración parte desde una base con V144 aplicada y el smoke test confirma que no existe
endpoint, secreto, dependencia o llamada a Drive en este lote.

**Trace:** Todos los criterios de aceptación de `propuestas.md`; criterio de corte E2E de
`flujo-ofertas.md` §7.

## Bundle D: Google Drive, diferido

**Tareas ejecutables:** ninguna en esta entrega.

La exportación a Google Drive queda fuera del alcance conforme a `propuestas.md` §1 Excluded y
nota 8. El Bundle B sólo usa GCS en producción o `LocalStorageService` en desarrollo mediante
`IStorageService`. No crear OAuth de Drive, service account, carpetas, scopes, endpoints ni
dependencias para resolverlo parcialmente.

El trabajo futuro debe reabrirse como una spec separada cuando negocio decida carpeta individual
versus compartida y el owner confirme el modelo de autenticación.

## Decisiones abiertas remanentes

1. **Plantilla oficial:** negocio debe confirmar que `tivit_proposal_template.docx` de PRJ-001 es
   la versión corporativa vigente y entregar la semilla final de texto de los 10 capítulos.
2. **Representación del PDF dentro del DOCX:** delivery debe seleccionar y documentar si se usa
   objeto PDF embebido, anexo preservando bytes u otra técnica compatible con Word. La conversión
   DOCX a PDF sigue fuera de alcance.
3. **Canal de aviso:** el contrato aprobado garantiza notificación in-app mediante V064. Email
   real o canales adicionales requieren confirmar si se conectan al canal existente de Alertas;
   no deben inventarse en C2.
4. **Owner y aprobador:** confirmar por HITL la persona responsable de validar plantilla, anexos
   PDF y canal de avisos. No inferirlo del autor del framework ni de los autores de estos docs.
5. **Drive:** destino individual o carpeta compartida, autenticación y permisos quedan para la
   futura Fase 3.5.

## Riesgos de implementación

- **PDF/OpenXML:** Word no trata un PDF como texto DOCX nativo. Una técnica incorrecta puede
  producir un archivo que descarga pero no abre o pierde el PDF original.
- **Payload Census:** `user-certifications` ronda 5,2 MB y puede cambiar de forma. Un parser que
  materialice personas completas o haga un upsert por cada registro puede consumir memoria,
  tiempo y conexiones innecesarias.
- **Datos corporativos:** la plantilla y las semillas PRJ-001 pueden contener datos reales o
  texto no aprobado. La revisión de contenido debe ocurrir antes de empaquetarlas.
- **Concurrencia de versiones:** dos generaciones simultáneas pueden calcular el mismo máximo.
  La transacción y la UK `(licitacion_id, version)` deben formar una operación con retry seguro.
- **Fallos parciales de avisos:** Notificaciones y decisión usan operaciones separadas. Sin una
  estrategia de lote y error explícita, `notificados` puede no representar lo realmente enviado.
- **Census y WAF/token:** la integración depende de `HttpClient`, credenciales de servicio,
  renovación ante 401 y configuración por entorno. Nunca poner credenciales en el repositorio.
- **Tiempo objetivo:** generación, hasta cinco PDFs y escritura en GCS deben mantenerse dentro de
  la meta de 10 segundos o producir una señal operativa clara, sin convertir silenciosamente el
  endpoint en un workflow asíncrono no especificado.

## Resumen de ejecución

| Bundle | Tareas | Horas estimadas | Dependencia de cierre |
|---|---:|---:|---|
| A. Catálogos y Census | 5 | 19h | A1-A5 en orden |
| B. DOCX y versionado | 5 | 18h | A3/A4/A5 según cada tarea |
| C. Avisos y frontend | 5 | 19h | B5 y C2 antes del slice final |
| D. Drive | 0 | 0h | Diferido, nueva spec |
| **Total ejecutable** | **15** | **56h** | C5 verde |
