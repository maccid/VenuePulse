# syntax=docker/dockerfile:1
# Contexto de build: la raíz del repo (ver infra/docker-compose.yml).
#
# @venue/core es una dependencia `file:../venue-core` (resuelve al PADRE de WORKDIR), por eso se
# copia a /venue-core. Se le hace su propio `npm ci` para que tsc encuentre sus tipos (react,
# @types/react, zustand, @microsoft/signalr) al compilar los .tsx del core con `tsc -b`.

FROM node:22-alpine AS build
WORKDIR /app
COPY ui/.npmrc ./
COPY ui/venue-ops/package.json ui/venue-ops/package-lock.json* ./
COPY ui/venue-core/ /venue-core/
COPY ui/.npmrc /venue-core/.npmrc
WORKDIR /venue-core
RUN npm ci
WORKDIR /app
RUN npm ci
COPY ui/venue-ops/ ./
RUN npm run build

FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY infra/nginx/spa.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
