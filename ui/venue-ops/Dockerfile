# syntax=docker/dockerfile:1
# Contexto de build: la raíz del repo (ver infra/docker-compose.yml).
FROM node:22-alpine AS build
WORKDIR /app
COPY ui/.npmrc ./
COPY ui/venue-ops/package.json ui/venue-ops/package-lock.json* ./
RUN npm ci
COPY ui/venue-ops/ ./
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY infra/nginx/spa.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
