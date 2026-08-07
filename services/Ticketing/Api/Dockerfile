# syntax=docker/dockerfile:1
# Build ejecutado con el contexto en la raíz del repo: docker build -f services/Ticketing/Api/Dockerfile .
# Nunca se usa "COPY . ." — solo las rutas explícitas que este servicio realmente necesita.

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
