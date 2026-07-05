# Guía de Solución de Problemas (Troubleshooting)

Esta guía te ayudará a resolver los problemas más comunes al usar el Framework Agéntico de TIVIT Digital.

---

## Diagnóstico Rápido

### Tabla de Síntomas → Soluciones

| Síntoma | Causa probable | Solución rápida |
|---------|----------------|-----------------|
| Framework no responde | Copilot inactivo | Verificar ícono de Copilot en barra inferior |
| Skills no se activan | Workspace incorrecto | Reabrir workspace correcto |
| Backend no arranca | Variables de entorno faltantes | Configurar .env o appsettings |
| Frontend no arranca | Dependencias no instaladas | `npm install` |
| Tests fallan | Servicios no corriendo | Iniciar backend y frontend |
| DB connection error | Cadena de conexión incorrecta | Verificar connection string |
| Puerto en uso | Servicio ya corriendo | Detener proceso o cambiar puerto |
| CORS error | Gateway mal configurado | Revisar configuración de CORS |

---

## Problema 1: Framework No Responde

### Síntomas:
- Escribes en el chat pero no hay respuesta del framework
- El agente no menciona skills del framework
- Respuestas genéricas sin contexto del framework

### Diagnóstico:

**1. Verificar GitHub Copilot**
```
- Mira la barra inferior de VS Code
- Debería haber un ícono de Copilot
- Si está con X roja → Copilot no activo
```

**2. Verificar workspace**
```bash
# En terminal de VS Code
pwd
# Debe mostrar: /home/.../MarcoTrabajoInterno
```

**3. Verificar conexión a internet**
```bash
ping github.com
```

### Soluciones:

**Solución A: Reactivar Copilot**
1. Clic en ícono de Copilot (barra inferior)
2. "Sign in to GitHub"
3. Completar autenticación

**Solución B: Recargar VS Code**
```
Ctrl/Cmd + R
# O cerrar y reabrir VS Code
```

**Solución C: Verificar extensión Copilot**
1. `Ctrl/Cmd + Shift + X` → Extensiones
2. Buscar "GitHub Copilot"
3. Verificar que esté instalado y actualizado
4. Recargar si es necesario

---

## Problema 2: Backend No Arranca

### Síntomas:
- `dotnet run` falla
- Error de conexión a base de datos
- Puerto ya en uso
- Dependencias faltantes

### Diagnóstico:

**Verificar logs de error**:
```bash
dotnet run
# Lee el mensaje de error completo
```

### Solución según error:

#### Error: "Connection string not found"

**Causa**: Falta configuración de base de datos.

**Solución**:
```bash
# Crear archivo .env o appsettings.Development.json
cd backend/

# .env (si el proyecto lo usa)
cat > .env << 'EOF'
ConnectionStrings__Default=Server=localhost;Database=MyDb;User Id=sa;Password=YourPassword;TrustServerCertificate=true
EOF

# O appsettings.Development.json
cat > appsettings.Development.json << 'EOF'
{
  "ConnectionStrings": {
    "Default": "Server=localhost;Database=MyDb;User Id=sa;Password=YourPassword;TrustServerCertificate=true"
  }
}
EOF
```

#### Error: "Port 8080 already in use"

**Causa**: Otro proceso usando el puerto.

**Solución**:
```bash
# Listar procesos en puerto 8080
# Linux/Mac:
lsof -i :8080

# Windows:
netstat -ano | findstr :8080

# Matar el proceso
kill -9 <PID>

# O cambiar puerto en launchSettings.json
```

#### Error: "Package restore failed"

**Causa**: Dependencias NuGet faltantes.

**Solución**:
```bash
dotnet restore
dotnet clean
dotnet build
```

#### Error: "Cannot connect to database"

**Causa**: Base de datos no está corriendo o credenciales incorrectas.

**Solución**:
```bash
# Si usas Docker:
docker ps | grep postgres  # O sql-server, mysql

# Si no está corriendo:
docker-compose up -d db

# Verificar conexión:
# PostgreSQL:
psql -h localhost -U postgres -d mydb

# SQL Server:
sqlcmd -S localhost -U sa -P YourPassword
```

---

## Problema 3: Frontend No Arranca

### Síntomas:
- `npm run dev` falla
- Error "Module not found"
- Puerto 3000 ya en uso
- Dependencias desactualizadas

### Diagnóstico:

**Verificar error**:
```bash
cd frontend/
npm run dev
# Lee el mensaje de error
```

### Solución según error:

#### Error: "Cannot find module 'X'"

**Causa**: Dependencias no instaladas.

**Solución**:
```bash
# Instalar dependencias
npm install

# O con pnpm (si el proyecto lo usa)
pnpm install

# Limpiar cache si persiste
rm -rf node_modules package-lock.json
npm install
```

#### Error: "Port 3000 already in use"

**Causa**: Otro proceso usando el puerto.

**Solución**:
```bash
# Opción 1: Matar proceso
# Linux/Mac:
lsof -i :3000
kill -9 <PID>

# Windows:
netstat -ano | findstr :3000
taskkill /PID <PID> /F

# Opción 2: Cambiar puerto
# Editar package.json o crear .env:
PORT=3001
```

#### Error: "Failed to compile"

**Causa**: Error de sintaxis en código TypeScript/React.

**Solución**:
```bash
# Ver detalles del error en terminal
# Corregir archivo indicado
# El framework puede ayudar:
```

En chat:
```
"Hay un error de compilación en [archivo]: [mensaje de error]"
```

---

## Problema 4: Base de Datos

### Síntomas:
- Tabla no existe
- Stored procedure no encontrado
- Error de sintaxis SQL
- Datos no se guardan

### Diagnóstico:

**Verificar conexión**:
```sql
-- SQL Server:
SELECT @@VERSION;

-- PostgreSQL:
SELECT version();

-- MySQL:
SELECT VERSION();
```

### Soluciones:

#### Tabla no existe

**Causa**: Migración no ejecutada.

**Solución**:
```bash
# Si el framework generó scripts SQL
cd database/migrations/

# Ejecutar scripts en orden
# SQL Server:
sqlcmd -S localhost -U sa -P YourPassword -i 001_create_tables.sql

# PostgreSQL:
psql -h localhost -U postgres -d mydb -f 001_create_tables.sql
```

#### Stored procedure no encontrado

**Causa**: SP no creado aún.

**Solución**:
```sql
-- Verificar que existe:
-- SQL Server:
SELECT name FROM sys.procedures WHERE name LIKE 'SP_Tasks%';

-- PostgreSQL:
SELECT routine_name FROM information_schema.routines WHERE routine_name LIKE 'sp_tasks%';

-- Si no existe, ejecutar script de creación
```

#### Error de permisos

**Causa**: Usuario de DB sin permisos suficientes.

**Solución**:
```sql
-- SQL Server: Otorgar permisos
USE MyDatabase;
GRANT EXECUTE TO [username];

-- PostgreSQL:
GRANT ALL PRIVILEGES ON DATABASE mydb TO username;
```

---

## Problema 5: Tests Fallan

### Síntomas:
- `npx playwright test` falla
- Tests E2E timeout
- Elementos no encontrados
- API tests fallan

### Diagnóstico:

**Ver reporte**:
```bash
npx playwright test
npx playwright show-report
```

### Soluciones:

#### Timeout en tests E2E

**Causa**: Backend/Frontend no están corriendo.

**Solución**:
```bash
# Terminal 1: Backend
cd backend/
dotnet run

# Terminal 2: Frontend
cd frontend/
npm run dev

# Terminal 3: Tests
cd tests/e2e/
npx playwright test
```

#### Elemento no encontrado

**Causa**: Selector cambió o elemento no carga a tiempo.

**Solución**:
```typescript
// Aumentar timeout para elemento específico
await page.waitForSelector('[data-testid="task-list"]', { 
  timeout: 10000 
});
```

En chat:
```
"El test de [funcionalidad] falla porque no encuentra [elemento]"
```

#### Tests de API fallan

**Causa**: Backend no responde o endpoint cambió.

**Solución**:
```bash
# Verificar que backend corre
curl http://localhost:8080/api/tasks

# Verificar Swagger
# Abrir: http://localhost:8080/swagger
```

---

## Problema 6: Docker

### Síntomas:
- `docker-compose up` falla
- Contenedores no arrancan
- Red de Docker con problemas
- Volúmenes con permisos incorrectos

### Diagnóstico:

**Ver logs**:
```bash
docker-compose logs
docker ps -a
```

### Soluciones:

#### Contenedores no arrancan

**Causa**: Puerto ya en uso o configuración incorrecta.

**Solución**:
```bash
# Ver puertos en uso
docker ps

# Detener todos los contenedores
docker-compose down

# Limpiar y reiniciar
docker-compose down -v
docker-compose up -d
```

#### Error de red Docker

**Causa**: Red Docker corrupta.

**Solución**:
```bash
# Recrear red
docker-compose down
docker network prune
docker-compose up -d
```

#### Volúmenes con permisos

**Causa**: Usuario sin permisos en volúmenes.

**Solución**:
```bash
# Dar permisos a carpeta local
sudo chown -R $USER:$USER ./data

# O en docker-compose.yml:
# volumes:
#   - ./data:/data:z  # SELinux
```

---

## Problema 7: Autenticación y Permisos

### Síntomas:
- 401 Unauthorized
- 403 Forbidden
- Token inválido
- CORS error

### Diagnóstico:

**Ver error en consola del navegador** (F12 → Console).

### Soluciones:

#### 401 Unauthorized

**Causa**: Token ausente o expirado.

**Solución**:
```javascript
// Verificar que el token se envía
// En hooks de React:
const token = localStorage.getItem('token');
// Verificar que no esté expirado

// Reloguear si es necesario
```

#### 403 Forbidden

**Causa**: Usuario sin permisos para la acción.

**Solución**:
```
- Verificar rol del usuario en DB
- Verificar políticas de autorización en backend
- Consultar con admin si necesitas permisos
```

#### CORS Error

**Causa**: Backend no permite requests desde el frontend.

**Solución en Backend (.NET)**:
```csharp
// En Program.cs:
builder.Services.AddCors(options => {
    options.AddPolicy("AllowFrontend", policy => {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

app.UseCors("AllowFrontend");
```

---

## Problema 8: Debugging

### Skills no se activan correctamente

**Síntoma**: El framework no ejecuta la skill esperada.

**Solución**:
```
"Solo necesito el backend, no toques el frontend"
"Actualiza solo el campo [X], no cambies nada más"
```

### Cambios no se reflejan

**Causa**: Cache de build o navegador.

**Solución**:
```bash
# Backend:
dotnet clean
dotnet build

# Frontend:
rm -rf node_modules/.vite  # O .next, .nuxt según framework
npm run dev

# Navegador:
Ctrl/Cmd + Shift + R  # Hard refresh
```

---

## Escalamiento

### Cuándo contactar soporte

Si después de seguir esta guía el problema persiste:

**Problemas técnicos**:
- [Manuel Aliaga](mailto:manuel.aliaga@tivit.com)
- Incluye: logs de error, pasos para reproducir, código relevante

**Problemas de diseño/arquitectura**:
- [Miguel Martinez](mailto:miguel.martinez@tivit.com)
- Incluye: contexto del módulo, decisiones tomadas

### Información útil para soporte

Al reportar un problema, incluye:

```
1. ¿Qué intentabas hacer?
2. ¿Qué escribiste en el chat?
3. ¿Qué respondió el framework?
4. ¿Qué error viste? (copia completa)
5. ¿Logs de backend/frontend?
6. ¿Sistema operativo y versiones?
```

---

## Comandos Útiles de Diagnóstico

### Verificación de entorno

```bash
# Versiones instaladas
node --version
npm --version
dotnet --version
docker --version
git --version

# Estado de servicios
docker ps
dotnet --list-sdks
```

### Limpieza completa

```bash
# Backend (.NET)
cd backend/
dotnet clean
rm -rf bin/ obj/
dotnet restore
dotnet build

# Frontend
cd frontend/
rm -rf node_modules/ package-lock.json
npm install

# Docker
docker-compose down -v
docker system prune -f
```

---

## Referencias Adicionales

- [QUICKSTART.md](QUICKSTART.md) — Primer módulo paso a paso
- [WORKFLOW-GUIDE.md](WORKFLOW-GUIDE.md) — Workflows detallados
- [FAQ.md](FAQ.md) — Preguntas frecuentes
- [SKILLS-MANIFEST.md](../framework/SKILLS-MANIFEST.md) — Catálogo de skills

---

**¿Resolvió tu problema esta guía?**  
Si encontraste un problema no documentado, por favor repórtalo a [Manuel Aliaga](mailto:manuel.aliaga@tivit.com) para agregar la solución aquí.
