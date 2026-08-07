# syntax=docker/dockerfile:1
# Build ejecutado con el contexto en la raíz del repo: docker build -f services/Ticketing/Api/Dockerfile .
# Nunca se usa "COPY . ." — solo las rutas que este servicio realmente necesita (ver ADR 0007
# y docs/architecture/adr/0007-ia-nunca-en-docker.md: .ia/ jamás se referencia, así que jamás puede entrar aquí).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY services/Directory.Build.props services/Directory.Packages.props ./
COPY services/Core/VenuePulse.Core.csproj Core/
COPY services/Ticketing/Domain/VenuePulse.Ticketing.Domain.csproj Ticketing/Domain/
COPY services/Ticketing/Application/VenuePulse.Ticketing.Application.csproj Ticketing/Application/
COPY services/Ticketing/Infrastructure/VenuePulse.Ticketing.Infrastructure.csproj Ticketing/Infrastructure/
COPY services/Ticketing/Api/VenuePulse.Ticketing.Api.csproj Ticketing/Api/
RUN dotnet restore Ticketing/Api/VenuePulse.Ticketing.Api.csproj

COPY services/Core/ Core/
COPY services/Ticketing/ Ticketing/
RUN dotnet publish Ticketing/Api/VenuePulse.Ticketing.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "VenuePulse.Ticketing.Api.dll"]
