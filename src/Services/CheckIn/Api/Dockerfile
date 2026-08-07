# syntax=docker/dockerfile:1
# Build ejecutado con el contexto en la raíz del repo: docker build -f src/Services/CheckIn/Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Directory.Build.props src/Directory.Packages.props ./
COPY src/Core/VenuePulse.Core/VenuePulse.Core.csproj Core/VenuePulse.Core/
COPY src/Services/CheckIn/Domain/VenuePulse.CheckIn.Domain.csproj Services/CheckIn/Domain/
COPY src/Services/CheckIn/Application/VenuePulse.CheckIn.Application.csproj Services/CheckIn/Application/
COPY src/Services/CheckIn/Infrastructure/VenuePulse.CheckIn.Infrastructure.csproj Services/CheckIn/Infrastructure/
COPY src/Services/CheckIn/Api/VenuePulse.CheckIn.Api.csproj Services/CheckIn/Api/
RUN dotnet restore Services/CheckIn/Api/VenuePulse.CheckIn.Api.csproj

COPY src/Core/VenuePulse.Core/ Core/VenuePulse.Core/
COPY src/Services/CheckIn/ Services/CheckIn/
RUN dotnet publish Services/CheckIn/Api/VenuePulse.CheckIn.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "VenuePulse.CheckIn.Api.dll"]
