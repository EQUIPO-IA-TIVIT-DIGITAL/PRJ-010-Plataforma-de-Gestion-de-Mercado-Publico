# Quickstart: Validación del Rediseño Frontend de MPM

Checklist de validación visual/funcional por pantalla. Correr contra `docker compose up` local (`http://localhost:8181`), no contra `npm run dev` suelto, para reflejar el mismo build que producción (regla ya establecida en este proyecto).

## Base compartida (antes de tocar cualquier pantalla)

- [ ] `StatusBadge` renderiza correctamente las 6 variantes (`neutral`, `info`, `warning`, `success`, `error`, `tertiary`) en un caso de prueba aislado.
- [ ] `PageHeader` renderiza ícono + título + subtítulo + acciones en un caso de prueba aislado, con el chip siempre en `colorPrimary` del theme.
- [ ] `npx tsc --noEmit` sin errores nuevos introducidos por los dos componentes.

## US1 — Licitaciones (P1)

- [ ] Abrir `/licitaciones` en desktop (`> 1024px`): la fila de tarjetas de estadísticas por estado no tiene huecos de alineación, cualquiera sea la cantidad de estados.
- [ ] Reducir el viewport a `< 768px`: el layout colapsa sin scroll horizontal roto ni controles inaccesibles.
- [ ] Abrir el drawer de detalle de una licitación: usa `StatusBadge` para el estado, no un badge propio.
- [ ] Confirmar que la tabla de licitaciones (tarea principal) tiene mayor peso visual que los bloques de estadísticas.
- [ ] Todos los filtros y funcionalidades existentes (búsqueda, filtro por área/estado/tipo, "sin clasificar", seguir licitación) siguen funcionando sin cambios de comportamiento.

## US2 — Análisis (P1)

- [ ] `/analisis`: `StatusBadge` reemplaza `STATUS_CONFIG`; sin emojis en labels ni en opciones de `Select`.
- [ ] `/analisis/:id` (workspace): subir un documento y disparar un análisis funciona igual que antes del rediseño.
- [ ] `/analisis/:id/dashboard`: la información se presenta con jerarquía clara (no lista plana); `ComparativaDocumentos` sigue funcionando.
- [ ] `/analisis/:id/chat`: se percibe integrado visualmente con el resto del módulo, no como panel aislado.
- [ ] Recorrido completo lista → workspace → dashboard → chat sin inconsistencias de header/badge entre pasos.

## US3 — Catálogos (P2)

- [ ] `/catalogos`: los estados/tipos/monedas usan `StatusBadge`, no `STATUS_CONFIG` propio.
- [ ] Un usuario nuevo puede encontrar y entender una categoría de catálogo sin explicación adicional (prueba informal con alguien no involucrado en el desarrollo).

## US4 — Mensajería (P2)

- [ ] `/mensajes`: layout reconstruido sobre `Layout`/`Card`/`List` de Ant Design — cero `div` con `style={{}}` inline de layout estructural.
- [ ] Crear conversación, enviar mensaje, adjuntar archivo, ver indicador de presencia y de "escribiendo...": toda la funcionalidad responde igual que antes del rediseño.
- [ ] SignalR sigue conectado al mismo hub (`/hubs/mensajeria`) sin cambios — verificar en herramientas de red del navegador que la conexión WebSocket se establece igual que antes.
- [ ] Comparar visualmente con Licitaciones/Análisis ya rediseñadas: mismo lenguaje de contenedores, tipografía y espaciado.

## US5 — Ejecutivo, Alertas, Competidores (P3)

- [ ] `/analisis/ejecutivo`: nueva tarjeta "Cobertura de mercado" visible, con `Progress` y tabla de licitaciones sin participación, con link funcional a cada ficha.
- [ ] `/alertas` y `/competidores`: usan `StatusBadge`/`PageHeader` compartidos, sin paleta de color propia.
- [ ] Crear/gestionar una alerta y buscar un competidor siguen funcionando exactamente igual que antes del rediseño.

## Regresión general

- [ ] `dotnet test MPM.sln` sin regresiones (solo aplica si se agregó el endpoint de cobertura de mercado).
- [ ] `npm run test:e2e` (Playwright) sin regresiones en los flujos existentes.
- [ ] Notificaciones (`/notificaciones`) no tiene cambios de contenido, pero hereda automáticamente cualquier ajuste de `AppLayout`/navegación si aplica.
- [ ] Un usuario externo al equipo de desarrollo navega las 8 pantallas en secuencia sin identificar inconsistencias visuales evidentes (SC-005 de spec.md).
