# syntax=docker/dockerfile:1
# Contexto de build: la raíz del repo (ver infra/docker-compose.yml) — igual que los Dockerfiles
# del backend, para poder copiar tanto el código de la app como infra/nginx/spa.conf.

FROM node:22-alpine AS build
WORKDIR /app
COPY ui/.npmrc ./
COPY ui/shell/package.json ui/shell/package-lock.json* ./
RUN npm ci
COPY ui/shell/ ./
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY infra/nginx/spa.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
