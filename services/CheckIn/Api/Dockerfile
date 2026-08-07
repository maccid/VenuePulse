# syntax=docker/dockerfile:1
# Build ejecutado con el contexto en la raíz del repo: docker build -f services/CheckIn/Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY services/Directory.Build.props services/Directory.Packages.props ./
COPY services/Core/VenuePulse.Core.csproj Core/
COPY services/CheckIn/Domain/VenuePulse.CheckIn.Domain.csproj CheckIn/Domain/
COPY services/CheckIn/Application/VenuePulse.CheckIn.Application.csproj CheckIn/Application/
COPY services/CheckIn/Infrastructure/VenuePulse.CheckIn.Infrastructure.csproj CheckIn/Infrastructure/
COPY services/CheckIn/Api/VenuePulse.CheckIn.Api.csproj CheckIn/Api/
RUN dotnet restore CheckIn/Api/VenuePulse.CheckIn.Api.csproj

COPY services/Core/ Core/
COPY services/CheckIn/ CheckIn/
RUN dotnet publish CheckIn/Api/VenuePulse.CheckIn.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "VenuePulse.CheckIn.Api.dll"]
