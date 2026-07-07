FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

COPY src/MPM.Api/MPM.Api.csproj src/MPM.Api/
COPY src/MPM.Shared/MPM.Shared.csproj src/MPM.Shared/
COPY src/MPM.Core/MPM.Core.csproj src/MPM.Core/
COPY src/MPM.Modules.Licitaciones/MPM.Modules.Licitaciones.csproj src/MPM.Modules.Licitaciones/
COPY src/MPM.Modules.Catalogo/MPM.Modules.Catalogo.csproj src/MPM.Modules.Catalogo/
COPY src/MPM.Modules.Mensajeria/MPM.Modules.Mensajeria.csproj src/MPM.Modules.Mensajeria/
COPY src/MPM.Modules.Auth/MPM.Modules.Auth.csproj src/MPM.Modules.Auth/
COPY src/MPM.Modules.Analisis/MPM.Modules.Analisis.csproj src/MPM.Modules.Analisis/
COPY src/MPM.Modules.Notificaciones/MPM.Modules.Notificaciones.csproj src/MPM.Modules.Notificaciones/
COPY src/MPM.Modules.Alertas/MPM.Modules.Alertas.csproj src/MPM.Modules.Alertas/
COPY MPM.sln .
RUN dotnet restore src/MPM.Api/MPM.Api.csproj

COPY src/ src/
RUN dotnet publish src/MPM.Api/MPM.Api.csproj -c Release -o out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install Node.js 20 LTS (required by ScraperBackgroundService to run agente-mp.js)
RUN apt-get update && \
    apt-get install -y --no-install-recommends curl ca-certificates && \
    curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y --no-install-recommends nodejs && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

# Install scraper Node dependencies (cached layer — only rebuilds when package.json changes)
COPY tools/scraper-mp/package.json tools/scraper-mp/package-lock.json /app/tools/
RUN cd /app/tools && npm ci --omit=dev

# Install Playwright Chromium browser + required OS libraries
RUN cd /app/tools && npx playwright install chromium --with-deps

# Copy scraper source files (node_modules excluded via .dockerignore)
COPY tools/scraper-mp/ /app/tools/

COPY --from=build /app/out .
RUN mkdir -p /app/uploads/mensajeria
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
ENTRYPOINT ["dotnet", "MPM.Api.dll"]
