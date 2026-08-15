# syntax=docker/dockerfile:1
# Build ejecutado con el contexto en la raíz del repo: docker build -f services/AccessControl/Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY services/Directory.Build.props services/Directory.Packages.props ./
COPY services/Core/VenuePulse.Core.csproj Core/
COPY services/Contracts/VenuePulse.Contracts.csproj Contracts/
COPY services/AccessControl/Domain/VenuePulse.AccessControl.Domain.csproj AccessControl/Domain/
COPY services/AccessControl/Application/VenuePulse.AccessControl.Application.csproj AccessControl/Application/
COPY services/AccessControl/Infrastructure/VenuePulse.AccessControl.Infrastructure.csproj AccessControl/Infrastructure/
COPY services/AccessControl/Api/VenuePulse.AccessControl.Api.csproj AccessControl/Api/
RUN dotnet restore AccessControl/Api/VenuePulse.AccessControl.Api.csproj

COPY services/Core/ Core/
COPY services/Contracts/ Contracts/
COPY services/AccessControl/ AccessControl/
RUN dotnet publish AccessControl/Api/VenuePulse.AccessControl.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl para el healthcheck de docker-compose (la imagen aspnet no lo trae: Ubuntu 24.04)
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "VenuePulse.AccessControl.Api.dll"]
