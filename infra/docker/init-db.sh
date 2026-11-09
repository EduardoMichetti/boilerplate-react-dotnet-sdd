#!/usr/bin/env bash
# Inicialização do banco e das identidades técnicas de UMA instalação.
# Executado pelo entrypoint oficial do PostgreSQL (/docker-entrypoint-initdb.d/) somente
# na primeira criação do volume. GRANTs por tabela NÃO ficam aqui: são emitidos pela
# migration inicial (decisão G6).
set -euo pipefail

faltantes=()
for variavel in BANCO_APP SENHA_OWNER SENHA_RUNTIME LOGIN_OPERADOR_ACESSO SENHA_OPERADOR_ACESSO; do
    if [ -z "${!variavel:-}" ]; then
        faltantes+=("$variavel")
    fi
done
if [ "${#faltantes[@]}" -ne 0 ]; then
    # Somente os nomes das variáveis; nunca valores.
    echo "init-db: variáveis obrigatórias ausentes: ${faltantes[*]}" >&2
    exit 1
fi

if ! [[ "$BANCO_APP" =~ ^[a-z][a-z0-9_]{0,40}$ ]]; then
    echo "init-db: BANCO_APP deve casar com ^[a-z][a-z0-9_]{0,40}$." >&2
    exit 1
fi
if ! [[ "$LOGIN_OPERADOR_ACESSO" =~ ^[a-z][a-z0-9_]{0,62}$ ]]; then
    echo "init-db: LOGIN_OPERADOR_ACESSO deve casar com ^[a-z][a-z0-9_]{0,62}$." >&2
    exit 1
fi

owner="${BANCO_APP}_owner"
runtime="${BANCO_APP}_runtime"
operador="${BANCO_APP}_operador"

if [ "$LOGIN_OPERADOR_ACESSO" = "$owner" ] || [ "$LOGIN_OPERADOR_ACESSO" = "$runtime" ] || [ "$LOGIN_OPERADOR_ACESSO" = "$operador" ]; then
    echo "init-db: LOGIN_OPERADOR_ACESSO deve ser um login individual distinto das roles técnicas." >&2
    exit 1
fi

# Senhas são passadas como variáveis psql (:'var'), citadas pelo próprio psql; nunca ecoadas.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "${POSTGRES_DB:-postgres}" \
    -v owner="$owner" -v runtime="$runtime" -v operador="$operador" \
    -v login_operador="$LOGIN_OPERADOR_ACESSO" -v banco="$BANCO_APP" \
    -v senha_owner="$SENHA_OWNER" -v senha_runtime="$SENHA_RUNTIME" \
    -v senha_operador="$SENHA_OPERADOR_ACESSO" <<'SQL'
\set QUIET on
CREATE ROLE :"owner" WITH LOGIN PASSWORD :'senha_owner'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"runtime" WITH LOGIN PASSWORD :'senha_runtime'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"operador" WITH NOLOGIN
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;
CREATE ROLE :"login_operador" WITH LOGIN PASSWORD :'senha_operador'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS
    IN ROLE :"operador";

CREATE DATABASE :"banco" OWNER :"owner";
REVOKE ALL ON DATABASE :"banco" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"banco" TO :"owner", :"runtime", :"operador";

\connect :"banco"
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
CREATE SCHEMA foundation AUTHORIZATION :"owner";
CREATE SCHEMA access AUTHORIZATION :"owner";
GRANT USAGE ON SCHEMA foundation, access TO :"runtime", :"operador";
SQL

echo "init-db: banco e identidades técnicas da instalação criados."
