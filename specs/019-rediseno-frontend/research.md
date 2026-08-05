# Research: Rediseño Frontend de MPM — Alcance por pantalla

## 1. Paleta y API de `StatusBadge`

**Decision**: Un único componente `StatusBadge` con una prop `variant` de 6 valores semánticos fijos — `neutral | info | warning | success | error | tertiary` — más una prop `label` (texto) y una prop opcional `icon`. Cada variante mapea a un color fijo tomado del theme existente (`main.tsx`), no a un color libre por pantalla.

**Rationale**: La auditoría de esta sesión mostró que las 5 implementaciones existentes (`AnalisisListPage.STATUS_CONFIG`, `CatalogoPage.STATUS_CONFIG`, ternarios inline de `NotificacionesPage`, `<Switch>` de `AlertasPage`, `<Tag color="success"/"error">` de `EjecutivoDashboardPage`) ya convergen, sin coordinación previa, en el mismo set de 5-6 colores base (gris neutro, azul información, ámbar en-progreso/advertencia, verde éxito, rojo error, y morado como categoría terciaria en `CatalogoPage`). Formalizar esas 6 variantes como el vocabulario único del sistema evita inventar una paleta nueva y respeta lo que cada pantalla ya intentaba comunicar.

| Variant | Uso | Color (ya en el theme o a agregar) |
|---|---|---|
| `neutral` | pendiente, sin clasificar, borrador | `colorTextSecondary` / gris `#64748b` |
| `info` | listo, disponible, informativo | `colorInfo` (`#3b82f6`, ya en theme) |
| `warning` | en progreso, analizando, próximo a vencer | `colorWarning` (`#f59e0b`, ya en theme) |
| `success` | completado, aceptada, ganada, activa | `colorSuccess` (`#10b981`, ya en theme) |
| `error` | error, rechazada, perdida, vencida | `colorError` (`#ef4444`, ya en theme) |
| `tertiary` | categoría especial (ej. tipo de licitación, área de negocio) | morado `#8b5cf6` — nuevo token de theme, no existía como color semántico formal |

**Alternatives considered**:
- Mantener `STATUS_CONFIG` por pantalla pero centralizado en un archivo de constantes compartido (sin componente): descartado — no resuelve la inconsistencia de estructura visual (forma del badge, tamaño, icono), solo la de color.
- Usar directamente `<Tag color="...">` de Ant Design sin wrapper propio: descartado — Ant Design acepta cualquier string de color, lo que no impide que cada desarrollador vuelva a hardcodear un hex nuevo; un componente propio con `variant` tipado (union de 6 strings) sí lo impide a nivel de TypeScript.

## 2. API de `PageHeader`

**Decision**: Un componente `PageHeader` con props `icon` (elemento de `@ant-design/icons`), `title`, `subtitle?`, y `actions?` (slot para botones a la derecha, ej. "Sincronizar", "Nueva alerta"). El ícono se renderiza siempre dentro de un chip cuadrado con el color `colorPrimary` del theme (rojo TIVIT) — no un color libre por pantalla como el morado encontrado en `AnalisisListPage`.

**Rationale**: De las 3 estructuras de header encontradas, dos ya comparten la forma (ícono en chip + título), solo divergen en color; la tercera (`NotificacionesPage`, sin ícono) es la excepción, no la regla. Fijar el color del chip al primario de marca (en vez de dejarlo libre) resuelve la inconsistencia sin necesitar una nueva decisión de diseño por pantalla.

**Alternatives considered**: Permitir un color de chip configurable por pantalla (prop `accentColor`): descartado por ahora — reintroduciría la misma superficie de inconsistencia que se está corrigiendo. Si en el futuro se necesita diferenciar módulos por color, se evalúa como cambio de spec aparte.

## 3. Comparativa nueva para Ejecutivo (FR-008)

**Decision**: Agregar una comparativa de **cobertura de mercado por área de negocio**: cuántas licitaciones totales existieron en el área/período (universo, ya calculable vía el mismo mecanismo de búsqueda pública que usa `competidor-mercado.js` de spec 024) frente a cuántas TIVIT efectivamente analizó/ofertó — expresado como porcentaje de cobertura y como lista de licitaciones del área donde TIVIT no participó en absoluto.

**Rationale**: Es la comparativa de mayor valor con menor costo de implementación: la spec 024 (ya cerrada en local, pendiente de deploy) construyó exactamente esta capacidad (`competidores_actividad_mercado`, búsqueda pública sin login) para el caso "un competidor específico vs. el mercado" — extenderla a "TIVIT vs. el mercado" reutiliza la misma infraestructura sin inventar una fuente de datos nueva. Responde directamente al objetivo de negocio detrás de FR-008 ("detectar brechas", el mismo lenguaje usado en spec 031 para la actividad de mercado de competidores).

**Necesita backend nuevo**: Sí, un endpoint de solo lectura (`GET /api/v1/analisis/ejecutivo/cobertura-mercado`) y un stored procedure nuevo (`usp_AnalisisEjecutivo_CoberturaMercado`, convención ya establecida) que agregue sobre `licitaciones` + `licitaciones_ofertas` filtrando por área de negocio — sin tabla nueva, sin migración de esquema más allá de la consulta. Esto resuelve como **no violación** los dos ítems condicionales marcados en el Constitution Check de plan.md (Principios II y III): el procedimiento nuevo sigue la convención existente.

**Alternatives considered**:
- Evolución temporal del win rate (mes a mes) en vez de cobertura de mercado: descartado como comparativa *principal* por ahora — es una mejora válida pero de menor impacto de negocio inmediato que "qué se nos está escapando"; queda como candidato para una iteración futura de Ejecutivo, no bloqueante para esta spec.
- Comparativa de monto ofertado promedio TIVIT vs. competidores por licitación: descartado por ahora — los datos existen (`licitaciones_ofertas`) pero requiere una vista más compleja (distribución, no solo cobertura) que excede el alcance "al menos una comparativa nueva" pedido en FR-008.

## 4. Necesidad de librería de gráficos nueva

**Decision**: No se agrega ninguna librería de gráficos. La comparativa de cobertura de mercado se representa con `Progress` (porcentaje de cobertura) y `Table`/`Empty` (listado de licitaciones sin participación) — ambos ya en uso en `EjecutivoDashboardPage.tsx` hoy.

**Rationale**: Cumple la restricción de la spec de no agregar dependencias nuevas salvo justificación puntual — en este caso, la comparativa elegida no la necesita. Si una iteración futura de Ejecutivo agrega series temporales (ver alternativa descartada arriba), ahí sí podría justificarse `@ant-design/plots` (misma familia que Ant Design, integración nativa) en vez de una librería genérica.

## 5. Reconstrucción de Mensajería (US4) sin romper tiempo real

**Decision**: `MensajeriaPage.tsx` y sus subcomponentes (`ChatPanel`, `ChatHeader`, `MensajeList`, `MensajeInput`, `TypingIndicator`, `ConversacionList`, `CrearConversacionModal`, `ParticipantesDrawer`) se reconstruyen usando `Layout`/`Card`/`List` de Ant Design en vez de `div`+estilos inline, pero **reutilizan exactamente los mismos hooks de datos** (`useChatLogic`, `usePresencia`) sin modificar su contrato ni el hub de SignalR al que se conectan.

**Rationale**: El Principio VI de la constitución (Real-Time via SignalR + Redis Backplane) exige que los módulos con tiempo real se conecten al hub existente, no que implementen mecanismos propios — la reconstrucción es exclusivamente de la capa de presentación (JSX/estilos), separando limpiamente "cómo se ve" de "cómo se obtienen y sincronizan los datos". Esto también reduce el riesgo de regresión: los hooks ya probados no cambian.

**Alternatives considered**: Reescribir también la capa de hooks junto con la UI, aprovechando el rediseño para simplificar el código de mensajería: descartado — mezclar refactor de lógica con rediseño visual en la misma historia aumenta el riesgo sin necesidad (spec.md FR-002/FR-009 exigen preservar toda la funcionalidad existente); si el código de hooks necesita refactor, es una historia técnica aparte.
