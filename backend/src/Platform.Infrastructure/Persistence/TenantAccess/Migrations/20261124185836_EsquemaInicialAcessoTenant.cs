using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Platform.Infrastructure.Persistence.TenantAccess.Migrations;
/// <inheritdoc />
public partial class EsquemaInicialAcessoTenant : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "foundation");

        migrationBuilder.EnsureSchema(
            name: "access");

        migrationBuilder.CreateTable(
            name: "identidade_banco",
            schema: "foundation",
            columns: table => new
            {
                IdUnico = table.Column<int>(type: "integer", nullable: false, comment: "Chave singleton fixa (IdUnicoPadrao = 1)."),
                IdTenant = table.Column<Guid>(type: "uuid", nullable: false, comment: "UUID imutável do tenant dono deste banco; chave alternativa referenciada pelas contas."),
                IdAmbiente = table.Column<Guid>(type: "uuid", nullable: false, comment: "UUID imutável do ambiente da instalação."),
                EpocaAcesso = table.Column<Guid>(type: "uuid", nullable: false, comment: "Geração local de sessões; sessões de outra época são rejeitadas."),
                InicializadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante UTC da inicialização técnica, do TimeProvider do tooling.")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_identidade_banco", x => x.IdUnico);
                table.UniqueConstraint("uq_identidade_banco_id_tenant", x => x.IdTenant);
                table.CheckConstraint("ck_identidade_banco_id_unico", "\"IdUnico\" = 1");
            },
            comment: "Identidade singleton do banco da instalação, conferida antes de qualquer acesso a contas e sessões.");

        migrationBuilder.CreateTable(
            name: "operacoes_acesso",
            schema: "access",
            columns: table => new
            {
                IdOperacao = table.Column<Guid>(type: "uuid", nullable: false, comment: "UUID v7 do evento e correlação do comando."),
                IdTenant = table.Column<Guid>(type: "uuid", nullable: false, comment: "Tenant do alvo validado (referência sem constraint)."),
                IdAmbiente = table.Column<Guid>(type: "uuid", nullable: false, comment: "Ambiente do alvo validado (referência sem constraint)."),
                IdUsuarioAlvo = table.Column<Guid>(type: "uuid", nullable: true, comment: "Conta alvo (referência sem FK, preservada após evolução da conta)."),
                LoginOperador = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "Login PostgreSQL individual autenticado do operador."),
                TipoOperacao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Tipo da operação restrita."),
                CodigoPermissao = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true, comment: "Código de permissão, quando relevante."),
                RegistradaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante UTC do registro, igual ao InstanteOperacao do comando."),
                Resultado = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Resultado da operação (Sucesso, Negado, Falha)."),
                CodigoResultado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Código estável e seguro do resultado, sem mensagem SQL."),
                RetencaoDias = table.Column<int>(type: "integer", nullable: true, comment: "Somente LimparSessoes: retenção aplicada, em dias (1–365)."),
                InstanteCorte = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Somente LimparSessoes: RegistradaEm - RetencaoDias."),
                QuantidadeRemovida = table.Column<int>(type: "integer", nullable: true, comment: "Somente LimparSessoes: sessões removidas (0 quando nada elegível ou falha).")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_operacoes_acesso", x => x.IdOperacao);
                table.CheckConstraint("ck_operacoes_acesso_limpeza", "(\"TipoOperacao\" = 'LimparSessoes' AND \"RetencaoDias\" IS NOT NULL AND \"InstanteCorte\" IS NOT NULL AND \"QuantidadeRemovida\" IS NOT NULL AND \"IdUsuarioAlvo\" IS NULL AND \"CodigoPermissao\" IS NULL) OR (\"TipoOperacao\" <> 'LimparSessoes' AND \"RetencaoDias\" IS NULL AND \"InstanteCorte\" IS NULL AND \"QuantidadeRemovida\" IS NULL)");
                table.CheckConstraint("ck_operacoes_acesso_login_operador", "char_length(\"LoginOperador\") BETWEEN 1 AND 128");
                table.CheckConstraint("ck_operacoes_acesso_resultado", "\"Resultado\" IN ('Sucesso', 'Negado', 'Falha')");
                table.CheckConstraint("ck_operacoes_acesso_tipo_operacao", "\"TipoOperacao\" IN ('CriarConta', 'DesativarConta', 'ReativarConta', 'RedefinirSenha', 'RevogarSessoes', 'ConcederPermissao', 'RevogarPermissao', 'LimparSessoes')");
                table.CheckConstraint("ck_operacoes_acesso_valores_limpeza", "(\"RetencaoDias\" IS NULL OR \"RetencaoDias\" BETWEEN 1 AND 365) AND (\"QuantidadeRemovida\" IS NULL OR \"QuantidadeRemovida\" >= 0)");
            },
            comment: "Histórico append-only das operações restritas de contas, permissões e sessões (FR-033).");

        migrationBuilder.CreateTable(
            name: "usuarios",
            schema: "access",
            columns: table => new
            {
                IdUsuario = table.Column<Guid>(type: "uuid", nullable: false, comment: "UUID v7 da conta, gerado pelo servidor."),
                FkIdTenant = table.Column<Guid>(type: "uuid", nullable: false, comment: "FK para identidade_banco.IdTenant: tenant dono da conta."),
                NomeExibicao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, comment: "Nome de exibição (1 a 120 caracteres); dado pessoal."),
                EstaAtivo = table.Column<bool>(type: "boolean", nullable: false, comment: "Conta habilitada; false nega login e operações protegidas."),
                CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante UTC da criação (microssegundos), do TimeProvider da operação."),
                TipoAutorCriacao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Tipo do autor da criação (Operador, Usuario, Sistema, Anonimo)."),
                AutorCriacao = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "Referência textual do autor da criação; nula somente para Anonimo."),
                IdOperacaoCriacao = table.Column<Guid>(type: "uuid", nullable: false, comment: "UUID de correlação da operação que criou o registro."),
                AlteradoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante UTC da última alteração efetiva; nulo se nunca alterado."),
                TipoAutorAlteracao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, comment: "Tipo do autor da última alteração; nulo se nunca alterado."),
                AutorAlteracao = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "Referência textual do autor da última alteração; nula para Anonimo ou sem alteração."),
                IdOperacaoAlteracao = table.Column<Guid>(type: "uuid", nullable: true, comment: "UUID de correlação da última alteração; obrigatório quando AlteradoEm está preenchido."),
                UserName = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true, comment: "Nome de usuário Identity, derivado do email."),
                NormalizedUserName = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true, comment: "Nome de usuário normalizado pelo Identity."),
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true, comment: "Email de login (até 254); dado pessoal."),
                NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true, comment: "Email normalizado; único no banco da instalação."),
                EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false, comment: "Confirmação de email do Identity; não exigida na L2."),
                PasswordHash = table.Column<string>(type: "text", nullable: true, comment: "Hash nativo do Identity; nunca senha pura."),
                SecurityStamp = table.Column<string>(type: "text", nullable: true, comment: "Carimbo de segurança vigente; mudança invalida sessões."),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true, comment: "Token de concorrência da conta, renovado pelo Identity a cada atualização."),
                PhoneNumber = table.Column<string>(type: "text", nullable: true, comment: "Telefone do Identity; sem uso na L2."),
                PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false, comment: "Confirmação de telefone do Identity; sem uso na L2."),
                TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false, comment: "2FA do Identity; sem uso na L2."),
                LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Fim UTC do bloqueio por tentativas, gravado a partir do TimeProvider."),
                LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false, comment: "Lockout habilitado em toda conta desde a criação."),
                AccessFailedCount = table.Column<int>(type: "integer", nullable: false, comment: "Falhas de senha consecutivas desde o último sucesso ou bloqueio.")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_usuarios", x => x.IdUsuario);
                table.CheckConstraint("ck_usuarios_auditoria_alteracao", "(\"AlteradoEm\" IS NULL AND \"TipoAutorAlteracao\" IS NULL AND \"AutorAlteracao\" IS NULL AND \"IdOperacaoAlteracao\" IS NULL) OR (\"AlteradoEm\" IS NOT NULL AND \"TipoAutorAlteracao\" IS NOT NULL AND \"IdOperacaoAlteracao\" IS NOT NULL AND (\"TipoAutorAlteracao\" = 'Anonimo') = (\"AutorAlteracao\" IS NULL))");
                table.CheckConstraint("ck_usuarios_auditoria_criacao", "(\"TipoAutorCriacao\" = 'Anonimo') = (\"AutorCriacao\" IS NULL)");
                table.CheckConstraint("ck_usuarios_nome_exibicao", "char_length(\"NomeExibicao\") BETWEEN 1 AND 120");
                table.CheckConstraint("ck_usuarios_tipo_autor_alteracao", "\"TipoAutorAlteracao\" IS NULL OR \"TipoAutorAlteracao\" IN ('Operador', 'Usuario', 'Sistema', 'Anonimo')");
                table.CheckConstraint("ck_usuarios_tipo_autor_criacao", "\"TipoAutorCriacao\" IN ('Operador', 'Usuario', 'Sistema', 'Anonimo')");
                table.ForeignKey(
                    name: "fk_usuarios_identidade_banco",
                    column: x => x.FkIdTenant,
                    principalSchema: "foundation",
                    principalTable: "identidade_banco",
                    principalColumn: "IdTenant",
                    onDelete: ReferentialAction.Restrict);
            },
            comment: "Contas locais da aplicação, exclusivas do tenant desta instalação.");

        migrationBuilder.CreateTable(
            name: "logins_usuario",
            schema: "access",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "text", nullable: false, comment: "Provedor externo (parte da PK)."),
                ProviderKey = table.Column<string>(type: "text", nullable: false, comment: "Chave no provedor externo (parte da PK)."),
                ProviderDisplayName = table.Column<string>(type: "text", nullable: true, comment: "Nome de exibição do provedor."),
                FkIdUsuario = table.Column<Guid>(type: "uuid", nullable: false, comment: "FK para a conta.")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_logins_usuario", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "fk_logins_usuario_usuarios",
                    column: x => x.FkIdUsuario,
                    principalSchema: "access",
                    principalTable: "usuarios",
                    principalColumn: "IdUsuario",
                    onDelete: ReferentialAction.Cascade);
            },
            comment: "Tabela auxiliar do Identity (logins externos); vazia e sem uso na L2.");

        migrationBuilder.CreateTable(
            name: "permissoes_usuario",
            schema: "access",
            columns: table => new
            {
                FkIdUsuario = table.Column<Guid>(type: "uuid", nullable: false, comment: "FK para a conta que recebeu a concessão."),
                CodigoPermissao = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "Código do catálogo CodigosPermissao (exceção registrada: compõe a PK)."),
                CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante UTC da criação (microssegundos), do TimeProvider da operação."),
                TipoAutorCriacao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Tipo do autor da criação (Operador, Usuario, Sistema, Anonimo)."),
                AutorCriacao = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "Referência textual do autor da criação; nula somente para Anonimo."),
                IdOperacaoCriacao = table.Column<Guid>(type: "uuid", nullable: false, comment: "UUID de correlação da operação que criou o registro."),
                AlteradoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante UTC da última alteração efetiva; nulo se nunca alterado."),
                TipoAutorAlteracao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, comment: "Tipo do autor da última alteração; nulo se nunca alterado."),
                AutorAlteracao = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "Referência textual do autor da última alteração; nula para Anonimo ou sem alteração."),
                IdOperacaoAlteracao = table.Column<Guid>(type: "uuid", nullable: true, comment: "UUID de correlação da última alteração; obrigatório quando AlteradoEm está preenchido.")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_permissoes_usuario", x => new { x.FkIdUsuario, x.CodigoPermissao });
                table.CheckConstraint("ck_permissoes_usuario_auditoria_alteracao", "(\"AlteradoEm\" IS NULL AND \"TipoAutorAlteracao\" IS NULL AND \"AutorAlteracao\" IS NULL AND \"IdOperacaoAlteracao\" IS NULL) OR (\"AlteradoEm\" IS NOT NULL AND \"TipoAutorAlteracao\" IS NOT NULL AND \"IdOperacaoAlteracao\" IS NOT NULL AND (\"TipoAutorAlteracao\" = 'Anonimo') = (\"AutorAlteracao\" IS NULL))");
                table.CheckConstraint("ck_permissoes_usuario_auditoria_criacao", "(\"TipoAutorCriacao\" = 'Anonimo') = (\"AutorCriacao\" IS NULL)");
                table.CheckConstraint("ck_permissoes_usuario_codigo_permissao", "\"CodigoPermissao\" ~ '^[a-z][a-z0-9]*(\\.[a-z][a-z0-9]*)+$' AND char_length(\"CodigoPermissao\") <= 128");
                table.CheckConstraint("ck_permissoes_usuario_imutavel", "\"AlteradoEm\" IS NULL AND \"TipoAutorAlteracao\" IS NULL AND \"AutorAlteracao\" IS NULL AND \"IdOperacaoAlteracao\" IS NULL");
                table.CheckConstraint("ck_permissoes_usuario_tipo_autor_alteracao", "\"TipoAutorAlteracao\" IS NULL OR \"TipoAutorAlteracao\" IN ('Operador', 'Usuario', 'Sistema', 'Anonimo')");
                table.CheckConstraint("ck_permissoes_usuario_tipo_autor_criacao", "\"TipoAutorCriacao\" IN ('Operador', 'Usuario', 'Sistema', 'Anonimo')");
                table.ForeignKey(
                    name: "fk_permissoes_usuario_usuarios",
                    column: x => x.FkIdUsuario,
                    principalSchema: "access",
                    principalTable: "usuarios",
                    principalColumn: "IdUsuario",
                    onDelete: ReferentialAction.Restrict);
            },
            comment: "Concessões imutáveis de permissões do catálogo às contas locais.");

        migrationBuilder.CreateTable(
            name: "reivindicacoes_usuario",
            schema: "access",
            columns: table => new
            {
                IdReivindicacaoUsuario = table.Column<int>(type: "integer", nullable: false, comment: "PK inteira imposta pelo Identity.")
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                FkIdUsuario = table.Column<Guid>(type: "uuid", nullable: false, comment: "FK para a conta."),
                ClaimType = table.Column<string>(type: "text", nullable: true, comment: "Tipo da claim."),
                ClaimValue = table.Column<string>(type: "text", nullable: true, comment: "Valor da claim.")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_reivindicacoes_usuario", x => x.IdReivindicacaoUsuario);
                table.ForeignKey(
                    name: "fk_reivindicacoes_usuario_usuarios",
                    column: x => x.FkIdUsuario,
                    principalSchema: "access",
                    principalTable: "usuarios",
                    principalColumn: "IdUsuario",
                    onDelete: ReferentialAction.Cascade);
            },
            comment: "Tabela auxiliar do Identity (claims); vazia e sem uso na L2.");

        migrationBuilder.CreateTable(
            name: "sessoes_autenticacao",
            schema: "access",
            columns: table => new
            {
                IdSessao = table.Column<Guid>(type: "uuid", nullable: false, comment: "UUID v7 opaco da sessão, gerado pelo servidor."),
                FkIdUsuario = table.Column<Guid>(type: "uuid", nullable: false, comment: "FK para a conta dona da sessão (exclusão restrita)."),
                EpocaAcesso = table.Column<Guid>(type: "uuid", nullable: false, comment: "Cópia da EpocaAcesso vigente na emissão."),
                TicketProtegido = table.Column<byte[]>(type: "bytea", nullable: true, comment: "Ticket nativo protegido por Data Protection; removido na revogação."),
                TetoAbsolutoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "CriadoEm + Sessao:TetoAbsolutoMinutos vigente na emissão; imutável e nunca estendido pelo uso."),
                JanelaInatividadeMinutos = table.Column<int>(type: "integer", nullable: false, comment: "Sessao:InatividadeMinutos vigente na emissão (5–240, não maior que o teto); imutável."),
                UltimoUsoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante UTC do último uso registrado (igual a CriadoEm na emissão; no máximo 1 escrita/minuto); só cresce."),
                RevogadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante UTC da revogação (igual a AlteradoEm); nulo enquanto não revogada."),
                Versao = table.Column<long>(type: "bigint", nullable: false, comment: "Token de concorrência positivo, incrementado a cada alteração efetiva."),
                CriadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Instante UTC da criação (microssegundos), do TimeProvider da operação."),
                TipoAutorCriacao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, comment: "Tipo do autor da criação (Operador, Usuario, Sistema, Anonimo)."),
                AutorCriacao = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "Referência textual do autor da criação; nula somente para Anonimo."),
                IdOperacaoCriacao = table.Column<Guid>(type: "uuid", nullable: false, comment: "UUID de correlação da operação que criou o registro."),
                AlteradoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Instante UTC da última alteração efetiva; nulo se nunca alterado."),
                TipoAutorAlteracao = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, comment: "Tipo do autor da última alteração; nulo se nunca alterado."),
                AutorAlteracao = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true, comment: "Referência textual do autor da última alteração; nula para Anonimo ou sem alteração."),
                IdOperacaoAlteracao = table.Column<Guid>(type: "uuid", nullable: true, comment: "UUID de correlação da última alteração; obrigatório quando AlteradoEm está preenchido.")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_sessoes_autenticacao", x => x.IdSessao);
                table.CheckConstraint("ck_sessoes_autenticacao_auditoria_alteracao", "(\"AlteradoEm\" IS NULL AND \"TipoAutorAlteracao\" IS NULL AND \"AutorAlteracao\" IS NULL AND \"IdOperacaoAlteracao\" IS NULL) OR (\"AlteradoEm\" IS NOT NULL AND \"TipoAutorAlteracao\" IS NOT NULL AND \"IdOperacaoAlteracao\" IS NOT NULL AND (\"TipoAutorAlteracao\" = 'Anonimo') = (\"AutorAlteracao\" IS NULL))");
                table.CheckConstraint("ck_sessoes_autenticacao_auditoria_criacao", "(\"TipoAutorCriacao\" = 'Anonimo') = (\"AutorCriacao\" IS NULL)");
                table.CheckConstraint("ck_sessoes_autenticacao_janela_inatividade", "\"JanelaInatividadeMinutos\" BETWEEN 5 AND 240 AND \"JanelaInatividadeMinutos\" * interval '1 minute' <= \"TetoAbsolutoEm\" - \"CriadoEm\"");
                table.CheckConstraint("ck_sessoes_autenticacao_revogada_sem_ticket", "\"RevogadaEm\" IS NULL OR \"TicketProtegido\" IS NULL");
                table.CheckConstraint("ck_sessoes_autenticacao_teto", "\"TetoAbsolutoEm\" - \"CriadoEm\" BETWEEN interval '30 minutes' AND interval '720 minutes'");
                table.CheckConstraint("ck_sessoes_autenticacao_tipo_autor_alteracao", "\"TipoAutorAlteracao\" IS NULL OR \"TipoAutorAlteracao\" IN ('Operador', 'Usuario', 'Sistema', 'Anonimo')");
                table.CheckConstraint("ck_sessoes_autenticacao_tipo_autor_criacao", "\"TipoAutorCriacao\" IN ('Operador', 'Usuario', 'Sistema', 'Anonimo')");
                table.CheckConstraint("ck_sessoes_autenticacao_ultimo_uso", "\"CriadoEm\" <= \"UltimoUsoEm\" AND \"UltimoUsoEm\" < \"TetoAbsolutoEm\"");
                table.CheckConstraint("ck_sessoes_autenticacao_uuids", "\"IdSessao\" <> '00000000-0000-0000-0000-000000000000' AND \"EpocaAcesso\" <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("ck_sessoes_autenticacao_versao", "\"Versao\" > 0");
                table.ForeignKey(
                    name: "fk_sessoes_autenticacao_usuarios",
                    column: x => x.FkIdUsuario,
                    principalSchema: "access",
                    principalTable: "usuarios",
                    principalColumn: "IdUsuario",
                    onDelete: ReferentialAction.Restrict);
            },
            comment: "Sessões autenticadas persistidas e revogáveis; expiram por inatividade ou teto absoluto, o que ocorrer primeiro.");

        migrationBuilder.CreateTable(
            name: "tokens_usuario",
            schema: "access",
            columns: table => new
            {
                FkIdUsuario = table.Column<Guid>(type: "uuid", nullable: false, comment: "FK para a conta (parte da PK)."),
                LoginProvider = table.Column<string>(type: "text", nullable: false, comment: "Provedor do token (parte da PK)."),
                Name = table.Column<string>(type: "text", nullable: false, comment: "Nome do token (parte da PK)."),
                Value = table.Column<string>(type: "text", nullable: true, comment: "Valor do token.")
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_tokens_usuario", x => new { x.FkIdUsuario, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "fk_tokens_usuario_usuarios",
                    column: x => x.FkIdUsuario,
                    principalSchema: "access",
                    principalTable: "usuarios",
                    principalColumn: "IdUsuario",
                    onDelete: ReferentialAction.Cascade);
            },
            comment: "Tabela auxiliar do Identity (tokens); vazia e sem uso na L2.");

        migrationBuilder.CreateIndex(
            name: "ix_logins_usuario_fk_id_usuario",
            schema: "access",
            table: "logins_usuario",
            column: "FkIdUsuario");

        migrationBuilder.CreateIndex(
            name: "ix_operacoes_acesso_id_usuario_alvo_registrada_em",
            schema: "access",
            table: "operacoes_acesso",
            columns: new[] { "IdUsuarioAlvo", "RegistradaEm" });

        migrationBuilder.CreateIndex(
            name: "ix_operacoes_acesso_registrada_em",
            schema: "access",
            table: "operacoes_acesso",
            column: "RegistradaEm");

        migrationBuilder.CreateIndex(
            name: "ix_reivindicacoes_usuario_fk_id_usuario",
            schema: "access",
            table: "reivindicacoes_usuario",
            column: "FkIdUsuario");

        migrationBuilder.CreateIndex(
            name: "ix_sessoes_autenticacao_fk_id_usuario",
            schema: "access",
            table: "sessoes_autenticacao",
            column: "FkIdUsuario");

        migrationBuilder.CreateIndex(
            name: "ix_sessoes_autenticacao_revogada_em",
            schema: "access",
            table: "sessoes_autenticacao",
            column: "RevogadaEm");

        migrationBuilder.CreateIndex(
            name: "ix_sessoes_autenticacao_teto_absoluto_em",
            schema: "access",
            table: "sessoes_autenticacao",
            column: "TetoAbsolutoEm");

        migrationBuilder.CreateIndex(
            name: "ix_sessoes_autenticacao_ultimo_uso_em",
            schema: "access",
            table: "sessoes_autenticacao",
            column: "UltimoUsoEm");

        migrationBuilder.CreateIndex(
            name: "ix_usuarios_fk_id_tenant",
            schema: "access",
            table: "usuarios",
            column: "FkIdTenant");

        migrationBuilder.CreateIndex(
            name: "uq_usuarios_normalized_email",
            schema: "access",
            table: "usuarios",
            column: "NormalizedEmail",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "uq_usuarios_normalized_user_name",
            schema: "access",
            table: "usuarios",
            column: "NormalizedUserName",
            unique: true);

        // GRANTs mínimos por tabela (decisão G6): roles derivadas do banco atual, criadas
        // antes da migration (init-db.sh no Compose ou fixture nos testes). Tabelas auxiliares
        // do Identity, suas sequências e escrita no marcador não recebem privilégio.
        migrationBuilder.Sql("""
            DO $$
            DECLARE
                v_runtime text := current_database() || '_runtime';
                v_operador text := current_database() || '_operador';
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_runtime) THEN
                    RAISE EXCEPTION 'Role % ausente: crie as roles da instalação (init-db.sh/fixture) antes da migration.', v_runtime;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_operador) THEN
                    RAISE EXCEPTION 'Role % ausente: crie as roles da instalação (init-db.sh/fixture) antes da migration.', v_operador;
                END IF;

                EXECUTE format('GRANT USAGE ON SCHEMA foundation, access TO %I, %I', v_runtime, v_operador);

                EXECUTE format('GRANT SELECT ON foundation.identidade_banco, foundation."__EFMigrationsHistory" TO %I', v_runtime);
                EXECUTE format('GRANT SELECT, UPDATE ON access.usuarios TO %I', v_runtime);
                EXECUTE format('GRANT SELECT ON access.permissoes_usuario TO %I', v_runtime);
                EXECUTE format('GRANT SELECT, INSERT, UPDATE ON access.sessoes_autenticacao TO %I', v_runtime);

                EXECUTE format('GRANT SELECT ON foundation.identidade_banco, foundation."__EFMigrationsHistory" TO %I', v_operador);
                EXECUTE format('GRANT SELECT, INSERT, UPDATE ON access.usuarios TO %I', v_operador);
                EXECUTE format('GRANT SELECT, INSERT, DELETE ON access.permissoes_usuario TO %I', v_operador);
                EXECUTE format('GRANT SELECT, UPDATE, DELETE ON access.sessoes_autenticacao TO %I', v_operador);
                EXECUTE format('GRANT INSERT ON access.operacoes_acesso TO %I', v_operador);
            END
            $$;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

        migrationBuilder.Sql("""
            DO $$
            DECLARE
                v_role text;
            BEGIN
                FOREACH v_role IN ARRAY ARRAY[current_database() || '_runtime', current_database() || '_operador'] LOOP
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_role) THEN
                        EXECUTE format('REVOKE ALL ON foundation.identidade_banco, foundation."__EFMigrationsHistory" FROM %I', v_role);
                        EXECUTE format('REVOKE ALL ON access.usuarios, access.permissoes_usuario, access.sessoes_autenticacao, access.operacoes_acesso FROM %I', v_role);
                    END IF;
                END LOOP;
            END
            $$;
            """);

        migrationBuilder.DropTable(
            name: "logins_usuario",
            schema: "access");

        migrationBuilder.DropTable(
            name: "operacoes_acesso",
            schema: "access");

        migrationBuilder.DropTable(
            name: "permissoes_usuario",
            schema: "access");

        migrationBuilder.DropTable(
            name: "reivindicacoes_usuario",
            schema: "access");

        migrationBuilder.DropTable(
            name: "sessoes_autenticacao",
            schema: "access");

        migrationBuilder.DropTable(
            name: "tokens_usuario",
            schema: "access");

        migrationBuilder.DropTable(
            name: "usuarios",
            schema: "access");

        migrationBuilder.DropTable(
            name: "identidade_banco",
            schema: "foundation");
    }
}
