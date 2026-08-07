#!/bin/bash
# Una única instancia de Postgres, una base de datos por servicio (Ticketing/AccessControl/CheckIn) —
# ver ADR sobre persistencia poliglota. Se ejecuta automáticamente en el primer arranque del contenedor.
set -e
set -u

function create_database() {
	local database=$1
	echo "Creando base de datos '$database'"
	psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-EOSQL
	    CREATE DATABASE $database;
EOSQL
}

if [ -n "${POSTGRES_MULTIPLE_DATABASES:-}" ]; then
	echo "Bases de datos solicitadas: $POSTGRES_MULTIPLE_DATABASES"
	for db in $(echo "$POSTGRES_MULTIPLE_DATABASES" | tr ',' ' '); do
		create_database "$db"
	done
fi
