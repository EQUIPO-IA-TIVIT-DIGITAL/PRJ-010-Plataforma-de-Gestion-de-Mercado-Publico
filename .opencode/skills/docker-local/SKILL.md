---
name: docker-local
description: 'Docker local development setup: compose configuration, multi-stage builds,
  service networking. Trigger: When setting up Docker for local development or containerizing
  services.'
metadata:
  phase:
  - construction
  layer:
  - backend
  enforcement: recommended
  depends_on: []
  consumed_by:
  - app-bootstrap
  - agent-backend
  agent_roles:
  - delivery-agent
  validation_profile: architecture-consistency
---

## Critical Rules
| Rule | Type | Rationale |
|------|------|-----------|
| Use multi-stage builds | ALWAYS | Smaller production images |
| Never include secrets in Dockerfile | NEVER | Images are shareable |
| Use `.dockerignore` | ALWAYS | Reduce build context size |
| Use named networks for inter-service communication | ALWAYS | Service discovery by name |
| Pin base image versions | ALWAYS | Reproducible builds |
| Use environment variables for configuration | ALWAYS | 12-factor app compliance |

## docker-compose.yml Structure
```yaml
services:
  api:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ConnectionStrings__Default=${DB_CONNECTION}
      - ASPNETCORE_ENVIRONMENT=Development
    depends_on:
      - db
    networks:
      - app-network

  db:
    image: postgres:16
    environment:
      POSTGRES_DB: mydb
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    ports:
      - "5432:5432"
    volumes:
      - db-data:/var/lib/postgresql/data
    networks:
      - app-network

volumes:
  db-data:

networks:
  app-network:
    driver: bridge
```

## Multi-Stage Dockerfile (.NET)
```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS runtime
WORKDIR /app
ENV TZ=UTC
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyApp.dll"]
```

## Multi-Stage Dockerfile (Java)
```dockerfile
FROM maven:3.9-eclipse-temurin-21 AS build
WORKDIR /src
COPY pom.xml .
RUN mvn dependency:resolve
COPY src ./src
RUN mvn package -DskipTests

FROM eclipse-temurin:21-jre
WORKDIR /app
COPY --from=build /src/target/*.jar app.jar
ENTRYPOINT ["java", "-jar", "app.jar"]
```

## Multi-Stage Dockerfile (Python FastAPI)
```dockerfile
FROM python:3.12-slim AS build
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir --target=/install -r requirements.txt

FROM python:3.12-slim
WORKDIR /app
COPY --from=build /install /usr/local/lib/python3.12/site-packages
COPY . .
ENV TZ=UTC
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
```

## Naming Conventions
| Element | Pattern | Example |
|---------|---------|---------|
| Container name | `{project}-{service}` | `myapp-api`, `myapp-db` |
| Network | `{project}-network` | `myapp-network` |
| Volume | `{project}-{data}` | `myapp-db-data` |
| Image tag | `{service}:{version}` | `api:1.0.0` |

## Inter-Service Communication
Use service names within the Docker network:
```
# From 'api' service, reach 'db' service:
ConnectionStrings__Default = "Host=db;Port=5432;Database=mydb;..."
# NOT localhost:5432
```

## Useful Commands
```bash
docker compose up -d         # Start all services
docker compose ps            # Check status
docker compose logs -f api   # Follow API logs
docker compose down          # Stop all
docker compose build --no-cache api  # Rebuild service
```

## Local Development Workflow
1. Copy `.env.example` → `.env.local` and fill values
2. `docker compose up -d`
3. `docker compose ps` — verify all healthy
4. `curl http://localhost:8080/health` — verify API running
5. Run frontend dev server separately: `npm run dev`

## .dockerignore
```
.git
.github
node_modules
.env*
*.md
tests/
docs/
```
