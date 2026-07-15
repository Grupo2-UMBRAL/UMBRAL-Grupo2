FROM node:22-alpine AS realm
WORKDIR /build
COPY infra/keycloak/import/umbral-realm.json ./
COPY deployment/keycloak/prepare-realm.mjs ./
ARG DEMO_PUBLIC_BASE_URL
ENV DEMO_PUBLIC_BASE_URL=$DEMO_PUBLIC_BASE_URL
RUN node prepare-realm.mjs

FROM quay.io/keycloak/keycloak:25.0
COPY --from=realm /build/umbral-realm.demo.json /opt/keycloak/data/import/umbral-realm.json
USER 1000
