FROM postgres:16-alpine
COPY infra/postgres/init/ /docker-entrypoint-initdb.d/
