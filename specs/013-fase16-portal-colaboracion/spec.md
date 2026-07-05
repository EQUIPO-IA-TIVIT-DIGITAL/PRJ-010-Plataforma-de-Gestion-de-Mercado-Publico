# Feature Specification: Fase 16 — Portal de Revisión Externa

**Feature Branch**: `013-fase16-portal-colaboracion`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 12 (Noviembre 2026)
**Impacto**: Medio | **Complejidad**: Media | **Depende de**: Fase 8

---

## Contexto

El análisis de licitaciones a veces necesita ser revisado por stakeholders externos: socios tecnológicos, subcontratistas, directivos sin acceso al sistema, o el área legal. Esta fase crea links compartibles con tiempo de expiración que dan acceso de solo-lectura a un análisis específico, sin requerir login.

---

## User Stories

### User Story 1 — Compartir análisis con link seguro (Priority: P1)

El analista quiere que un socio tecnológico revise el análisis de una licitación para cotizar su parte, sin darle acceso completo al sistema.

**Why this priority**: Actualmente se exportan PDFs manualmente o se reenvían capturas de pantalla. Un link dinámico siempre tiene la versión más actualizada.

**Independent Test**: El analista genera un link de compartir para el análisis de licitación X. El socio abre el link en incógnito (sin estar logueado) y ve el análisis completo con los datos del acta. El link expira en 7 días.

**Acceptance Scenarios**:
1. **Given** el usuario está en el dashboard de análisis, **When** hace clic en "Compartir", **Then** se genera un link único con expiración configurable (1/3/7/30 días).
2. **Given** el link es abierto por alguien sin cuenta, **When** accede, **Then** ve el análisis en modo lectura: evaluación, puntajes, conclusiones y KPIs — sin acceso a otras licitaciones.
3. **Given** el link expirado, **When** alguien intenta acceder, **Then** ve un mensaje de "Este análisis ya no está disponible" con instrucciones para solicitar uno nuevo.
4. **Given** el usuario revocó el link manualmente, **When** alguien intenta acceder, **Then** el acceso es denegado inmediatamente.

---

### User Story 2 — Historial de accesos al link (Priority: P2)

El analista quiere saber si el socio al que le envió el link realmente lo revisó.

**Why this priority**: Permite hacer seguimiento de si el material fue revisado antes de una reunión.

**Independent Test**: El sistema muestra que el link fue abierto 3 veces desde 2 IPs distintas en las últimas 24 horas.

**Acceptance Scenarios**:
1. **Given** un link fue abierto, **When** el dueño del link revisa el panel, **Then** ve: fecha/hora de cada acceso, país de origen (por IP) y cuántas veces fue abierto.

---

## Funcionalidades principales

- Generación de tokens únicos UUID con expiración configurable
- Vista pública de análisis (sin layout interno, branding TIVIT)
- Revocación manual de links activos
- Historial de accesos por link (IP, timestamp, user-agent)
- Panel de links compartidos: activos, expirados, revocados
- Opcional: protección adicional con pin de 6 dígitos
- Sin leak de datos sensibles: el link no expone otros análisis ni datos de usuarios

## Definición de Hecho

- [ ] Generación de link con UUID y expiración
- [ ] Vista pública de análisis funcional sin login
- [ ] Revocación manual inmediata
- [ ] Registro de accesos (IP, timestamp)
- [ ] Panel de gestión de links en el frontend
- [ ] Expiración automática verificada en cada acceso
