# syntax=docker/dockerfile:1
# Build ejecutado con el contexto en la raíz del repo: docker build -f src/Services/Ticketing/Api/Dockerfile .
# Nunca se usa "COPY . ." — solo las rutas que este servicio realmente necesita (ver ADR 0007
# y docs/architecture/adr/0007-ia-nunca-en-docker.md: .ia/ jamás se referencia, así que jamás puede entrar aquí).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Directory.Build.props src/Directory.Packages.props ./
COPY src/Core/VenuePulse.Core/VenuePulse.Core.csproj Core/VenuePulse.Core/
COPY src/Services/Ticketing/Domain/VenuePulse.Ticketing.Domain.csproj Services/Ticketing/Domain/
COPY src/Services/Ticketing/Application/VenuePulse.Ticketing.Application.csproj Services/Ticketing/Application/
COPY src/Services/Ticketing/Infrastructure/VenuePulse.Ticketing.Infrastructure.csproj Services/Ticketing/Infrastructure/
COPY src/Services/Ticketing/Api/VenuePulse.Ticketing.Api.csproj Services/Ticketing/Api/
RUN dotnet restore Services/Ticketing/Api/VenuePulse.Ticketing.Api.csproj

COPY src/Core/VenuePulse.Core/ Core/VenuePulse.Core/
COPY src/Services/Ticketing/ Services/Ticketing/
RUN dotnet publish Services/Ticketing/Api/VenuePulse.Ticketing.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "VenuePulse.Ticketing.Api.dll"]
