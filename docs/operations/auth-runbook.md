# Runbook Operativo: Módulo de Autenticación

## Información General

| Campo | Valor |
|-------|-------|
| Módulo | Auth |
| Servicio | MPM.Api |
| Prefijo API | `/api/v1/auth` |
| Health Check | `/health/auth` |
| Endpoints | 4 (Login, ForgotPassword, ValidateResetToken, ResetPassword) |
| TBHash | bcrypt factor 11 (pgcrypto) |
| JWT | HS256, 8 horas expiración |

## Endpoints

| Método | URL | Descripción | Autenticación |
|--------|-----|-------------|---------------|
| `POST` | `/api/v1/auth/login` | Iniciar sesión | No |
| `POST` | `/api/v1/auth/forgot-password` | Solicitar token de reset | No |
| `GET` | `/api/v1/auth/validate-reset-token/{token}` | Validar token | No |
| `POST` | `/api/v1/auth/reset-password` | Restablecer contraseña | No |
| `GET` | `/health/auth` | Health check | No |

## Seguridad

### Contraseñas
- **Hash**: bcrypt factor 11 usando `pgcrypto` (función `crypt()`)
- **Algoritmo**: `$2a$11$...`
- **Mínimo**: 6 caracteres
- **Reset token**: UUID sin guiones, expira en 1 hora
- **Invalidación**: Al solicitar nuevo reset, se invalidan tokens anteriores

### JWT Token
- **Algoritmo**: HS256
- **Expiración**: 8 horas
- **Claims**: `user_id`, `tenant_id`, `username`, `tenant_name`, `role[]`
- **Issuer**: Configurable (`JWT:Issuer`)

### Protecciones
- **No enumeración de usuarios**: Forgot-password siempre retorna éxito
- **Soft delete**: Usuarios eliminados no pueden autenticarse
- **Usuarios inactivos**: `activo = false` bloquea login

## Configuración

### Variables de Entorno

| Variable | Descripción | Ejemplo |
|----------|-------------|---------|
| `ConnectionStrings__PostgreSQL` | Cadena de conexión PostgreSQL | `Host=db;Port=5432;Database=mpm;Username=mpm;Password=***` |
| `JWT__Secret` | Clave secreta JWT (mín. 32 chars) | Generar con `openssl rand -base64 48` |
| `JWT__Issuer` | Emisor del token | `TIVIT.MPM` |
| `Smtp__Host` | Servidor SMTP | `smtp.gmail.com` |
| `Smtp__Port` | Puerto SMTP | `587` |
| `Smtp__Username` | Usuario SMTP | `notifications@tivit.cl` |
| `Smtp__Password` | Contraseña SMTP | `***` |
| `Smtp__FromEmail` | Email remitente | `noreply@mpm.cl` |
| `Smtp__FromName` | Nombre remitente | `MPM - Mercado Público` |
| `Smtp__EnableSsl` | Habilitar SSL | `true` |
| `App__BaseUrl` | URL base del frontend | `http://localhost:5173` |
| `App__LogResetToken` | Loguear token en desarrollo | `true` |

### Usuarios Demo

| Email | Password | Roles | Tenant |
|-------|----------|-------|--------|
| `admin@tivit.cl` | `test123` | SuperAdmin | TIVIT Chile |
| `analista@tivit.cl` | `test123` | Analista | TIVIT Chile |

## Base de Datos

### Tablas

| Tabla | Propósito |
|-------|-----------|
| `usuarios` | Usuarios del sistema con credenciales |
| `password_reset_tokens` | Tokens temporales para recuperación |

### Procedures

| Objeto | Tipo | Uso |
|--------|------|-----|
| `usp_Auth_ValidarUsuario` | Procedure | Validar credenciales y obtener datos |
| `usp_Auth_ForgotPassword` | Procedure | Generar token de reset |
| `usp_Auth_ActualizarPassword` | Procedure | Restablecer contraseña con token |

### Índices

| Índice | Tabla | Propósito |
|--------|-------|-----------|
| `idx_usuarios_email` | usuarios | Búsqueda rápida por email |
| `idx_usuarios_tenant` | usuarios | Filtrado por tenant |
| `idx_usuarios_activo` | usuarios | Filtrado de usuarios activos |
| `idx_password_reset_tokens_token` | password_reset_tokens | Búsqueda por token |
| `idx_password_reset_tokens_email` | password_reset_tokens | Búsqueda por email |
| `idx_password_reset_tokens_expires_at` | password_reset_tokens | Limpieza de tokens expirados |

## Troubleshooting

### Síntoma: Login falla con credenciales válidas

1. Verificar que el usuario existe: `SELECT * FROM usuarios WHERE email = 'admin@tivit.cl' AND deleted_at IS NULL`
2. Verificar que está activo: `SELECT activo FROM usuarios WHERE email = 'admin@tivit.cl'`
3. Verificar el hash de contraseña: `SELECT password_hash FROM usuarios WHERE email = 'admin@tivit.cl'`
4. Probar hash manualmente: `SELECT crypt('test123', password_hash) = password_hash FROM usuarios WHERE email = 'admin@tivit.cl'`

### Síntoma: Reset password token no funciona

1. Verificar que el token existe: `SELECT * FROM password_reset_tokens WHERE token = '...'`
2. Verificar que no ha expirado: `SELECT * FROM password_reset_tokens WHERE token = '...' AND expires_at > NOW()`
3. Verificar que no ha sido usado: `SELECT * FROM password_reset_tokens WHERE token = '...' AND used_at IS NULL`
4. En desarrollo, los tokens se loguean en la consola del backend

### Síntoma: Email de reset no llega

1. Verificar configuración SMTP en `appsettings.json`
2. Si `Smtp:Host` está vacío, el email se loguea en vez de enviar (modo desarrollo)
3. Verificar credenciales SMTP
4. Verificar que el puerto 587 no esté bloqueado

### Síntoma: JWT token inválido

1. Verificar que `JWT:Secret` sea el mismo en todos los servicios
2. Verificar que `JWT:Issuer` coincida
3. Verificar que el reloj del servidor esté sincronizado (NTP)
4. Verificar que el token no haya expirado (8 horas)

## SLOs

| Métrica | Objetivo |
|---------|----------|
| Disponibilidad API | 99.9% |
| Latencia login | < 500ms (P95) |
| Latencia forgot-password | < 300ms (P95) |
| Latencia reset-password | < 300ms (P95) |
| Token válido | 8 horas |
| Reset token válido | 1 hora |

## Alarmas

| Condición | Severidad | Acción |
|-----------|-----------|--------|
| `/health/auth` devuelve 503 | Critical | Verificar PostgreSQL |
| Múltiples logins fallidos | Warning | Posible brute-force, considerar rate limiting |
| Reset tokens expirando sin uso | Info | Verificar configuración de email |
| Latencia > 2s en login | Warning | Verificar índices y hash bcrypt |