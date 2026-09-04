# 6. Administración: usuarios, roles y logs

> Sección visible solo para **Administrador** y **Super Admin** (menú lateral →
> **Administración**).

## Usuarios del sistema

Pantalla **Administración → Usuarios**: tabla con todos los usuarios
(nombre, correo, rol, estado, último acceso).

### Crear un usuario

1. Botón **Nuevo usuario**.
2. Completa: nombre, correo, **rol** y **contraseña inicial** (mínimo 6 caracteres).
3. **Crear usuario**.

El usuario nuevo puede iniciar sesión de inmediato con esa contraseña; el sistema
le pedirá cambiarla en su primer ingreso (Mi perfil → Cambiar contraseña).

> Si el correo no se configura para envíos, usa el botón **Recuperar** de la fila
> para reenviar el correo de recuperación cuando el SMTP esté activo.

### Roles disponibles

| Rol | Qué puede hacer | Quién lo puede crear |
|-----|-----------------|----------------------|
| **Usuario** | Toda la plataforma | Administrador y Super Admin |
| **Analista** | Toda la plataforma | Administrador y Super Admin |
| **Administrador** | Todo + usuarios y logs | Solo Super Admin |
| **Super Admin** | Todo, incluido gestionar administradores y el motor de IA | Solo Super Admin |

### Otras acciones

- **Desactivar / Activar**: bloquea o restaura el acceso de un usuario. El
  usuario desactivado no puede iniciar sesión (sus datos se conservan).
- **Rol**: cambia el rol de un usuario (respetando la jerarquía anterior).
- **Acc. gobierno**: marca a un usuario como *account manager de gobierno* —
  recibe las alertas dirigidas a cuentas gubernamentales.
- **Recuperar**: envía el correo de recuperación de contraseña.

> No puedes desactivar tu propia cuenta ni cambiarte el rol a ti mismo (protección
> para no quedarte sin acceso).

## Logs y actividad del sistema

Pantalla **Administración → Logs y actividad**: todo lo que el sistema hace solo.

### Resumen del sistema
Tarjetas con el estado general: último inicio de sesión, última sincronización,
última corrida del scraper, última extracción de documentos y proveedor de IA activo.

### Pestañas de logs
| Pestaña | Qué registra |
|---------|--------------|
| Inicios de sesión | Quién entró, cuándo, desde qué IP |
| Sincronizaciones | Ciclos de actualización de licitaciones (registros, creados, errores) |
| Scraper | Corridas de descarga de documentos y actas |
| Extracción | Extracción de documentos por licitación (éxito/fallo) |
| Proveedor IA | Historial de cambios del motor de IA (quién, cuándo) |

Cada registro tiene un estado (éxito/fallo/parcial) y un detalle técnico
colapsable, útil si participas en el desarrollo del sistema.

## Cambiar el motor de IA (solo Super Admin)

Pantalla **Administración → Admin IA**: switch entre Google (Gemini) y Qwen
(infraestructura privada). El cambio aplica a los análisis siguientes, sin
reiniciar el sistema. El historial de cambios queda en Logs → Proveedor IA.

---

← [Volver al manual](README.md)
