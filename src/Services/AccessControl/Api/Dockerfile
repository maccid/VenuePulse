# syntax=docker/dockerfile:1
# Build ejecutado con el contexto en la raíz del repo: docker build -f src/Services/AccessControl/Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Directory.Build.props src/Directory.Packages.props ./
COPY src/Core/VenuePulse.Core/VenuePulse.Core.csproj Core/VenuePulse.Core/
COPY src/Services/AccessControl/Domain/VenuePulse.AccessControl.Domain.csproj Services/AccessControl/Domain/
COPY src/Services/AccessControl/Application/VenuePulse.AccessControl.Application.csproj Services/AccessControl/Application/
COPY src/Services/AccessControl/Infrastructure/VenuePulse.AccessControl.Infrastructure.csproj Services/AccessControl/Infrastructure/
COPY src/Services/AccessControl/Api/VenuePulse.AccessControl.Api.csproj Services/AccessControl/Api/
RUN dotnet restore Services/AccessControl/Api/VenuePulse.AccessControl.Api.csproj

COPY src/Core/VenuePulse.Core/ Core/VenuePulse.Core/
COPY src/Services/AccessControl/ Services/AccessControl/
RUN dotnet publish Services/AccessControl/Api/VenuePulse.AccessControl.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "VenuePulse.AccessControl.Api.dll"]
