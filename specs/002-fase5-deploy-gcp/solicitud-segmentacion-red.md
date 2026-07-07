# Respuesta a Nicolás — Segmentación de red, sin IP pública, Cloud SQL privado

**Estado**: la versión anterior de este documento (borrador proactivo pidiendo 2 segmentos) **no se envió** — quedó superada por la respuesta real de Nicolás del 2026-07-06 a `solicitud-consultor-cloud.md`, que ya exige la segmentación y agrega dos restricciones adicionales. Este documento reemplaza esa versión con la respuesta que sí corresponde enviar.

## Contexto — email de Nicolás Valdivia, 2026-07-06 10:02

Respondiendo a la solicitud de recursos para `mpm-prod` (VM, Service Accounts, firewall, Cloud SQL), Nicolás planteó 3 bloqueos:

1. **No usar la VPC default** — cada ambiente debe tener su propio segmento de red (VPC/subnet custom), la default "es solo de muestra".
2. **La VM no puede tener IP pública** — exponer servicios únicamente vía Load Balancer, nunca por IP directa de la VM. Preguntó explícitamente para qué necesitábamos la IP pública.
3. **Cloud SQL no puede tener IP pública ni usar `0.0.0.0/0`** — más estricto que la corrección que ya teníamos planeada (quitar solo el `0.0.0.0/0`); ahora se exige IP privada de punta a punta.

Esto invalida partes de `solicitud-consultor-cloud.md` (pedía VM con IP externa y firewall TCP:80 abierto a todo origen) y del plan de TLS con certbot en la VM (ver `research.md` secciones 5, 5b, y "Actualización 2026-07-06" en la sección de Cloud SQL).

---

## Mensaje a enviar

> Hola Nicolás, gracias por el detalle — tiene sentido, ajustamos la solicitud:
>
> **1. VPC custom (no default)**
> Necesitamos que generes una VPC dedicada para MPM (no la default), con estos segmentos:
> - **CU010 PRD** → `10.0.0.0/24` — donde va la VM `mpm-prod`.
> - **CU010 QA** → `10.0.1.0/24` — reservado a futuro, no hay ambiente QA todavía pero preferimos no tener que re-segmentar después.
>
> **2. VM sin IP pública**
> Entendido, no la necesitamos. La IP pública la pedimos originalmente para exponer el frontend/API vía HTTP(S) directo — lo correcto es hacerlo con un **HTTPS Load Balancer** externo apuntando a la VM como backend, como indicas. ¿Nos ayudas a crear ese Load Balancer, o prefieres que lo levantemos nosotros una vez tengamos la VPC y la VM? Para el acceso administrativo (SSH/deploy) vamos a usar **IAP TCP forwarding**, así que en vez de una regla de firewall abierta a internet en el puerto 22, necesitaríamos:
> - Firewall: permitir `35.235.240.0/20` (rango de IAP) hacia el puerto 22 de `mpm-prod`.
> - Firewall: permitir `130.211.0.0/22` y `35.191.0.0/16` (rangos de health check de Google) hacia el puerto del backend, para el Load Balancer.
>
> **3. Cloud SQL sin IP pública**
> De acuerdo, entendemos que no basta con sacar el `0.0.0.0/0`. Para migrar `mpm-db` a **Private IP** vamos a necesitar que asignes un rango de **Private Services Access** (peering) para la VPC nueva — ¿nos indicas qué rango usar según la convención de TIVIT, o lo definimos nosotros? Si necesitas que propongamos uno: `10.0.8.0/24` (separado del segmento de la VM).
>
> Quedamos atentos a como prefieras avanzar — feliz de agendar una llamada corta si es más rápido que por correo.
>
> Gracias por el apoyo.

---

## Razonamiento (para referencia futura)

- **/24 por ambiente** sigue siendo el dimensionamiento razonable para la VPC de cómputo — no cambia por las nuevas restricciones.
- **Load Balancer en vez de IP pública en la VM**: es un requisito no negociable de infraestructura TIVIT, no una preferencia de MPM. Cambia el diseño de TLS: certbot en la VM ya no es viable (necesita ser alcanzable directamente), el certificado gestionado va en el Load Balancer y sigue bloqueado por la falta de dominio (ver spec — pendiente, no bloqueante para levantar el LB con HTTP mientras tanto).
- **IAP en vez de SSH directo**: consecuencia directa de no tener IP pública en la VM; es el mecanismo estándar de GCP para esto, no requiere abrir puertos a internet.
- **Rango separado para Cloud SQL Private Services Access**: Google gestiona este rango como un peering, no es parte del subnet de la VM — por eso se pide como una asignación distinta (`10.0.8.0/24` es una propuesta, no una convención fija; Nicolás puede ajustarla).

## Seguimiento

- [ ] Enviar el mensaje (reemplaza el envío del borrador anterior, que no se llegó a mandar).
- [ ] Decidir con Nicolás quién crea el Load Balancer (su equipo o nosotros) y registrar la IP/dominio final aquí.
- [ ] Cuando Nicolás confirme los CIDR de VPC/subnet/Private Services Access, actualizar `research.md` (secciones 5, 5b) y `docker-compose.prod.yml` con los valores reales.
- [ ] Actualizar `quickstart.md` (prerrequisitos y Escenario 1) una vez el Load Balancer esté definido — hoy sigue asumiendo IP externa + certbot en la VM, que ya no aplica.
- [ ] Si el cliente confirma un ambiente QA/staging real para MPM, actualizar `spec.md`/`plan.md` — hoy `002-fase5-deploy-gcp` solo cubre PRD.
