-- Script de inicialização dos papéis e banco de dados isolados para a instalação
-- Executado pelo PostgreSQL na inicialização via /docker-entrypoint-initdb.d/

-- 1. Cria a role de owner (sem privilégios de superusuário)
CREATE ROLE owner_cliente_a WITH LOGIN PASSWORD 'ExemploSenhaOwnerFicticia123!'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;

-- 2. Cria o banco de dados da aplicação já com o owner correto
CREATE DATABASE appdb_cliente_a OWNER owner_cliente_a;


