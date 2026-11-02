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
                IdUnico = table.Column<int>(type: "integer", nullable: false, comment: "Identificador singleton fixado em 1 para garantir linha única."),
                IdTenant = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador imutável do tenant proprietário deste banco de dados."),
                IdAmbiente = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador único do ambiente de execução correspondente a esta instalação."),
                EpocaAcesso = table.Column<Guid>(type: "uuid", nullable: false, comment: "Época de acesso atual utilizada para revogação e invalidação global de sessões."),
                InicializadoEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora UTC em que o banco de dados da instalação foi inicializado tecnicamente.")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_identidade_banco", x => x.IdUnico);
                table.UniqueConstraint("uq_identidade_banco_id_tenant", x => x.IdTenant);
            },
            comment: "Armazena a identidade singleton do banco de dados para comprovação e isolamento por tenant.");

        migrationBuilder.CreateTable(
            name: "usuarios",
            schema: "access",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador único da conta do usuário (UUID v4)."),
                IdTenant = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador imutável do tenant proprietário desta conta."),
                NomeExibicao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, comment: "Nome amigável de exibição do usuário na aplicação."),
                EstaAtivo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true, comment: "Indica se o usuário está ativo no sistema e autorizado a acessar."),
                UserName = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true, comment: "Nome de usuário no sistema para propósitos de autenticação."),
                NormalizedUserName = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true, comment: "Nome de usuário normalizado para comparações e indexação."),
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true, comment: "Endereço de e-mail do usuário utilizado para login e notificações."),
                NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true, comment: "Endereço de e-mail normalizado em letras maiúsculas para consultas eficientes."),
                EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: true),
                SecurityStamp = table.Column<string>(type: "text", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                PhoneNumber = table.Column<string>(type: "text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_usuarios", x => x.Id);
                table.ForeignKey(
                    name: "FK_usuarios_identidade_banco_IdTenant",
                    column: x => x.IdTenant,
                    principalSchema: "foundation",
                    principalTable: "identidade_banco",
                    principalColumn: "IdTenant",
                    onDelete: ReferentialAction.Restrict);
            },
            comment: "Armazena os usuários da aplicação vinculados exclusivamente ao tenant desta instalação.");

        migrationBuilder.CreateTable(
            name: "logins_usuario",
            schema: "access",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                ProviderKey = table.Column<string>(type: "text", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_logins_usuario", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_logins_usuario_usuarios_UserId",
                    column: x => x.UserId,
                    principalSchema: "access",
                    principalTable: "usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "permissoes_usuario",
            schema: "access",
            columns: table => new
            {
                IdUsuario = table.Column<Guid>(type: "uuid", nullable: false, comment: "Identificador do usuário que recebeu a permissão."),
                CodigoPermissao = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false, comment: "Código textual identificador da permissão concedida no sistema."),
                ConcedidaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Data e hora UTC em que a permissão foi concedida."),
                Versao = table.Column<long>(type: "bigint", nullable: false, comment: "Token de versão para controle de concorrência otimista.")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_permissoes_usuario", x => new { x.IdUsuario, x.CodigoPermissao });
                table.ForeignKey(
                    name: "FK_permissoes_usuario_usuarios_IdUsuario",
                    column: x => x.IdUsuario,
                    principalSchema: "access",
                    principalTable: "usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            },
            comment: "Armazena as permissões granulares concedidas a cada usuário na instalação.");

        migrationBuilder.CreateTable(
            name: "reivindicacoes_usuario",
            schema: "access",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reivindicacoes_usuario", x => x.Id);
                table.ForeignKey(
                    name: "FK_reivindicacoes_usuario_usuarios_UserId",
                    column: x => x.UserId,
                    principalSchema: "access",
                    principalTable: "usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "tokens_usuario",
            schema: "access",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tokens_usuario", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_tokens_usuario_usuarios_UserId",
                    column: x => x.UserId,
                    principalSchema: "access",
                    principalTable: "usuarios",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_logins_usuario_UserId",
            schema: "access",
            table: "logins_usuario",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_reivindicacoes_usuario_UserId",
            schema: "access",
            table: "reivindicacoes_usuario",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "ix_usuarios_email_normalizado",
            schema: "access",
            table: "usuarios",
            column: "NormalizedEmail",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_usuarios_id_tenant",
            schema: "access",
            table: "usuarios",
            column: "IdTenant");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            schema: "access",
            table: "usuarios",
            column: "NormalizedUserName",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "logins_usuario",
            schema: "access");

        migrationBuilder.DropTable(
            name: "permissoes_usuario",
            schema: "access");

        migrationBuilder.DropTable(
            name: "reivindicacoes_usuario",
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
