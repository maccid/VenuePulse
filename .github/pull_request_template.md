## Qué cambia y por qué

<!-- Una o dos frases. El "por qué" importa más que el "qué". -->

## Cómo se ha probado

- [ ] `dotnet test services/VenuePulse.slnx` en verde (unit + arquitectura + integración)
- [ ] `docker-compose up` levanta todo y el flujo de compra funciona de extremo a extremo
- [ ] Frontend afectado: `npm run build` sin errores de tipos

## Checklist

- [ ] No hay `COPY . .` en ningún Dockerfile tocado
- [ ] Si se añade un evento/comando nuevo, está en `VenuePulse.Core.Messaging.Contracts`
- [ ] Si se toca la Saga, el camino de compensación tiene test de integración
