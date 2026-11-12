namespace Platform.IntegrationTests.Carga;

using System.Data.Common;
using System.Net;
using System.Text.Json;
using App.Api.Access;
using App.Api.Diagnostics;
using App.Api.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Application.Access;
using Platform.Application.Access.CasosUso;
using Platform.Application.Access.Portas;
using Platform.Domain.Resultados;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.IntegrationTests.Fixtures;

/// <summary>
/// DTO do corpo para gravação de registro sintético de teste de carga (T117/SC-003).
/// Não usa o prefixo Requisicao para não exigir validador de produção.
/// </summary>
/// <param name="Valor">Valor textual do registro.</param>
public sealed record DadosRegistroSintetico(string Valor);

/// <summary>
/// DTO de resposta do registro sintético.
/// </summary>
/// <param name="Id">Id do registro.</param>
/// <param name="Valor">Valor textual.</param>
public sealed record RespostaRegistroSintetico(Guid Id, string Valor);

/// <summary>
/// Suporte exclusivo para o teste de carga concorrente isolada SC-003 (T117).
/// </summary>
public static class SuporteCargaSintetica
{
    /// <summary>
    /// Prepara o schema e a tabela sintética com o owner da instalação e concede privilégios ao runtime.
    /// </summary>
    /// <param name="instalacao">Instalação alvo.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    public static async Task PrepararTabelaSinteticaAsync(InstalacaoTestePostgres instalacao, CancellationToken tokenCancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(instalacao);

        await using NpgsqlConnection conexaoOwner = new(instalacao.StringConexaoProprietario);
        await conexaoOwner.OpenAsync(tokenCancelamento);

        string sql = $"""
            CREATE SCHEMA IF NOT EXISTS teste_sc003;
            CREATE TABLE IF NOT EXISTS teste_sc003.registros_sinteticos (
                "IdRegistro" uuid PRIMARY KEY,
                "Valor" text NOT NULL,
                "GravadoEm" timestamp with time zone NOT NULL
            );
            GRANT USAGE ON SCHEMA teste_sc003 TO {instalacao.PapelExecucao};
            GRANT SELECT, INSERT, UPDATE ON teste_sc003.registros_sinteticos TO {instalacao.PapelExecucao};
            """;

        await using NpgsqlCommand comando = new(sql, conexaoOwner);
        await comando.ExecuteNonQueryAsync(tokenCancelamento);
    }
}

/// <summary>
/// Configurador das rotas sintéticas GET e POST sob /api/v1/teste-sc003/registros/{id}.
/// Exige a mesma validação de tenant, sessão e permissão foundation.context.read das rotas reais.
/// </summary>
public sealed class ConfiguradorRotasCargaSintetica : IConfiguradorRotasTeste
{
    /// <inheritdoc />
    public void Configurar(RouteGroupBuilder grupo)
    {
        ArgumentNullException.ThrowIfNull(grupo);

        RouteGroupBuilder grupoSintetico = grupo.MapGroup("/teste-sc003/registros")
            .WithMetadata(new RotaAcessoL2());

        grupoSintetico.MapPost("/{id:guid}", async (
            Guid id,
            [FromBody] DadosRegistroSintetico corpo,
            HttpContext contexto,
            EstadoRequisicaoAcesso estado,
            CasoUsoConsultarContexto casoUsoContexto,
            AcessoTenantDbContext db,
            TimeProvider relogio,
            CancellationToken ct) =>
        {
            if (estado.Sessao is not { } sessao)
            {
                return ConfiguracaoProblemDetails.Resultado(ErrosAcesso.AutenticacaoNecessaria);
            }

            Resultado<ContextoAcesso> autorizacao = await casoUsoContexto.ExecutarAsync(sessao, ct);
            if (!autorizacao.EhSucesso)
            {
                return ConfiguracaoProblemDetails.Resultado(autorizacao.Erro!);
            }

            DateTimeOffset agora = relogio.GetUtcNow();

            DbConnection conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await db.Database.OpenConnectionAsync(ct);
            }

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO teste_sc003.registros_sinteticos ("IdRegistro", "Valor", "GravadoEm")
                VALUES (@id, @valor, @agora)
                ON CONFLICT ("IdRegistro") DO UPDATE
                SET "Valor" = EXCLUDED."Valor", "GravadoEm" = EXCLUDED."GravadoEm";
                """;

            DbParameter pId = cmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = id;
            cmd.Parameters.Add(pId);

            DbParameter pValor = cmd.CreateParameter();
            pValor.ParameterName = "@valor";
            pValor.Value = corpo.Valor;
            cmd.Parameters.Add(pValor);

            DbParameter pAgora = cmd.CreateParameter();
            pAgora.ParameterName = "@agora";
            pAgora.Value = agora;
            cmd.Parameters.Add(pAgora);

            await cmd.ExecuteNonQueryAsync(ct);

            contexto.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new RespostaRegistroSintetico(id, corpo.Valor));
        });

        grupoSintetico.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext contexto,
            EstadoRequisicaoAcesso estado,
            CasoUsoConsultarContexto casoUsoContexto,
            AcessoTenantDbContext db,
            CancellationToken ct) =>
        {
            if (estado.Sessao is not { } sessao)
            {
                return ConfiguracaoProblemDetails.Resultado(ErrosAcesso.AutenticacaoNecessaria);
            }

            Resultado<ContextoAcesso> autorizacao = await casoUsoContexto.ExecutarAsync(sessao, ct);
            if (!autorizacao.EhSucesso)
            {
                return ConfiguracaoProblemDetails.Resultado(autorizacao.Erro!);
            }

            DbConnection conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await db.Database.OpenConnectionAsync(ct);
            }

            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT \"Valor\" FROM teste_sc003.registros_sinteticos WHERE \"IdRegistro\" = @id;";

            DbParameter pId = cmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = id;
            cmd.Parameters.Add(pId);

            await using DbDataReader reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                return Results.NotFound();
            }

            string valor = reader.GetString(0);
            contexto.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new RespostaRegistroSintetico(id, valor));
        });
    }
}
