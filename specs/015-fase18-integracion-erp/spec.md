# Feature Specification: Fase 18 — Integración ERP (SAP / Oracle)

**Feature Branch**: `015-fase18-integracion-erp`
**Created**: 2026-06-24
**Status**: Planned
**Semana estimada**: Semana 14 (Diciembre 2026)
**Impacto**: Medio | **Complejidad**: Muy Alta | **Depende de**: Fase 7

---

## Contexto

Cuando TIVIT gana una licitación, el proceso actual requiere ingresar manualmente los datos del contrato en el ERP (SAP o el sistema de gestión de proyectos que use TIVIT Chile). Esta fase crea un puente automático: licitación ganada en MPM → proyecto creado en ERP, eliminando doble ingreso y riesgo de error.

---

## User Stories

### User Story 1 — Creación automática de proyecto en ERP al ganar (Priority: P1)

El Project Manager de TIVIT necesita que cuando se marca una licitación como ganada, el sistema automáticamente cree el proyecto en SAP con los datos básicos ya cargados.

**Why this priority**: El ingreso manual al ERP toma 2-3 horas y es propenso a errores. La automatización elimina el costo y los errores de transcripción.

**Independent Test**: Al marcar una licitación como "Ganada" en el pipeline de MPM, en menos de 5 minutos aparece un proyecto nuevo en SAP con: nombre, organismo cliente, monto del contrato, código de licitación y responsable asignado.

**Acceptance Scenarios**:
1. **Given** una oportunidad marcada como "Ganada", **When** el usuario confirma el resultado, **Then** MPM envía un webhook al ERP con los datos del contrato y el ERP crea el proyecto automáticamente.
2. **Given** el ERP responde con el código de proyecto creado, **When** MPM recibe la confirmación, **Then** el código ERP queda vinculado a la oportunidad y visible en el pipeline.
3. **Given** el ERP no está disponible, **When** falla la integración, **Then** el sistema reintenta 3 veces y si falla notifica al admin para intervención manual — sin perder los datos.

---

### User Story 2 — Sincronización de estado de proyecto (Priority: P2)

El gerente de proyectos quiere que el avance de un contrato activo en el ERP se refleje en MPM, para tener una vista unificada.

**Why this priority**: Sin sincronización, MPM y el ERP tienen estados desactualizados entre sí, generando confusión.

**Independent Test**: Cuando el proyecto en SAP pasa a estado "En ejecución", MPM actualiza automáticamente el estado de la oportunidad a "Contrato activo".

**Acceptance Scenarios**:
1. **Given** un proyecto en SAP cambia de estado, **When** SAP envía un webhook a MPM, **Then** el estado del pipeline se actualiza y el responsable recibe notificación.

---

## Funcionalidades principales

- Módulo `MPM.Integrations.ERP` con adaptadores por sistema (SAP/Oracle/Genérico)
- Webhook outbound: MPM → ERP al marcar "Ganada"
- Webhook inbound: ERP → MPM para sincronización de estado
- Configuración de URL, credenciales y mapping de campos por empresa
- Cola de reintentos con backoff exponencial para fallos de ERP
- Log de integraciones: éxitos, fallos, payloads enviados/recibidos
- Modo "simulación": enviar sin ejecutar, para testing antes de go-live
- UI: configuración de integración en panel admin, historial de sincronizaciones

## Definición de Hecho

- [ ] Webhook outbound a ERP al ganar licitación
- [ ] Código de proyecto ERP vinculado en pipeline
- [ ] Cola de reintentos con notificación de fallo
- [ ] Webhook inbound de actualización de estado
- [ ] Log de integraciones en panel admin
- [ ] Modo simulación para testing
