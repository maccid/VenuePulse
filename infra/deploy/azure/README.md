# Topología en Azure (documentada, no aprovisionada)

Este documento describe cómo se desplegaría VenuePulse en Azure en un escenario real. **No se ha
aprovisionado nada de esto** — es la traducción consciente de las decisiones de arquitectura a
servicios gestionados de Azure, para que quede claro qué se sabe hacer sin necesidad de gastar en
una cuenta de Azure para una demo.

## Topología propuesta

| Pieza local (docker-compose) | Servicio Azure equivalente | Por qué |
|---|---|---|
| 3 contenedores .NET (Ticketing/AccessControl/CheckIn) | **AKS** (Azure Kubernetes Service), manifiestos en `infra/deploy/k8s/` | Ya está modelado como microservicios independientes; AKS es el destino natural |
| Imágenes Docker | **ACR** (Azure Container Registry) | Privado, integrado con AKS vía identidad gestionada (sin secretos de pull) |
| RabbitMQ | **Azure Service Bus** | MassTransit abstrae el transporte — cambiar `UsingRabbitMq` por `UsingAzureServiceBus` es configuración, no código (ver ADR 0001) |
| Postgres (3 bases) | **Azure Database for PostgreSQL Flexible Server** (una instancia, 3 bases, igual que en local) | Gestionado, sin operar parches/backups a mano |
| MongoDB (estado de Saga) | **Azure Cosmos DB for MongoDB (vCore)** | Compatible con el driver de MongoDB tal cual, sin cambiar código de `MongoDbSagaRepository` |
| Jaeger (trazas) | **Azure Monitor / Application Insights** (vía el mismo exporter OTLP) | En producción no se opera un Jaeger propio; se cambia el endpoint OTLP, el código de instrumentación no cambia |
| Seq (logs) | **Azure Monitor / Log Analytics** (Serilog dirigido también ahí) | En producción no se opera un Seq propio; se cambia el `SeqServerUrl` |
| Nginx gateway | **AKS Ingress Controller** (`ingress-nginx` o Application Gateway Ingress Controller) | Mismo esquema de rutas que `infra/nginx/nginx.conf`, ver `infra/deploy/k8s/base/ingress.yaml` |
| Secretos (connection strings) | **Azure Key Vault** + CSI Secret Store driver en AKS | Nunca en ConfigMaps ni variables de entorno en claro |

## Qué cambia en el código (y qué no)

- **No cambia**: el dominio, los agregados, la Saga, los tests. Es la ventaja de haber usado
  MassTransit y el driver oficial de MongoDB en vez de código propietario contra RabbitMQ/Mongo
  directamente.
- **Cambia solo configuración**: `RabbitMq:ConnectionString` → cadena de Service Bus,
  `Mongo:ConnectionString` → cadena de Cosmos DB, `Observability:OtlpEndpoint` → el collector de
  Azure Monitor.
- **Se añadiría**: Managed Identity para que los pods de AKS accedan a ACR/Key Vault sin
  credenciales estáticas (no implementado aquí, pero es el patrón estándar).

## CI/CD

`docker-build.yml` publicaría en ACR en vez de GHCR (cambio de `docker/login-action` a
`azure/docker-login@v2`) y un paso adicional de `kubectl apply -k infra/deploy/k8s/overlays/prod`
contra el AKS real, con las credenciales inyectadas vía OIDC federation de GitHub Actions
(sin secretos de larga duración).
