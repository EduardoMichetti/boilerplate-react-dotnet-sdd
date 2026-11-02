namespace Platform.IntegrationTests.Access;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Persistence.DesignTime;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Xunit;

public sealed class TestesPersistenciaUsuarioTenant(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>, IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        CancellationToken tokenCancelamento = TestContext.Current.CancellationToken;

        // Garante que ambas as bases estejam migradas antes de rodar os testes de persistência
        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            DbContextOptionsBuilder<AcessoTenantDbContext> construtor = new();
            construtor.UseNpgsql(tenant.StringConexaoProprietario, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
            });
            construtor.UseSeeding((contexto, _) =>
            {
                if (contexto is AcessoTenantDbContext contextoTenant)
                {
                    PreflightMigracaoTenant.PopularIdentidadeBanco(contextoTenant, tenant.IdTenant, tenant.IdAmbiente);
                }
            });
            construtor.UseAsyncSeeding(async (contexto, _, ct) =>
            {
                if (contexto is AcessoTenantDbContext contextoTenant)
                {
                    await PreflightMigracaoTenant.PopularIdentidadeBancoAsync(contextoTenant, tenant.IdTenant, tenant.IdAmbiente, ct);
                }
            });

            await using (AcessoTenantDbContext contexto = new(construtor.Options))
            {
                await contexto.Database.MigrateAsync(tokenCancelamento);
            }

            await tenant.ConcederPermissoesExecucaoAsync(tokenCancelamento);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task MesmoEmailEId_CriamContasIndependentesComSenhasDistintas()
    {
        Guid idUsuarioCompartilhado = Guid.NewGuid();
        const string emailCompartilhado = "comum@tenant.local";
        const string senhaA = "SenhaTenantA123!";
        const string senhaB = "SenhaTenantB456!";

        var (escopoA, gerenciadorUsuariosA, _) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
        await using (escopoA)
        {
            UsuarioAplicacao usuarioA = new()
            {
                Id = idUsuarioCompartilhado,
                IdTenant = fixture.TenantA.IdTenant,
                UserName = emailCompartilhado,
                Email = emailCompartilhado,
                NomeExibicao = "Usuário no Tenant A",
                EstaAtivo = true
            };

            IdentityResult resultadoA = await gerenciadorUsuariosA.CreateAsync(usuarioA, senhaA);
            Assert.True(resultadoA.Succeeded, string.Join("; ", resultadoA.Errors.Select(e => e.Description)));
        }

        var (escopoB, gerenciadorUsuariosB, _) = CriarEscopoCliente(fixture.TenantB.StringConexaoExecucao);
        await using (escopoB)
        {
            UsuarioAplicacao usuarioB = new()
            {
                Id = idUsuarioCompartilhado,
                IdTenant = fixture.TenantB.IdTenant,
                UserName = emailCompartilhado,
                Email = emailCompartilhado,
                NomeExibicao = "Usuário no Tenant B",
                EstaAtivo = true
            };

            IdentityResult resultadoB = await gerenciadorUsuariosB.CreateAsync(usuarioB, senhaB);
            Assert.True(resultadoB.Succeeded, string.Join("; ", resultadoB.Errors.Select(e => e.Description)));
        }

        // Verifica que as senhas e hashes são independentes e isolados
        var (escopoVerificacaoA, gerenciadorVerificacaoA, _) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
        await using (escopoVerificacaoA)
        {
            UsuarioAplicacao? carregadoA = await gerenciadorVerificacaoA.FindByIdAsync(idUsuarioCompartilhado.ToString());
            Assert.NotNull(carregadoA);
            Assert.Equal("Usuário no Tenant A", carregadoA.NomeExibicao);
            Assert.True(await gerenciadorVerificacaoA.CheckPasswordAsync(carregadoA, senhaA));
            Assert.False(await gerenciadorVerificacaoA.CheckPasswordAsync(carregadoA, senhaB));
        }

        var (escopoVerificacaoB, gerenciadorVerificacaoB, _) = CriarEscopoCliente(fixture.TenantB.StringConexaoExecucao);
        await using (escopoVerificacaoB)
        {
            UsuarioAplicacao? carregadoB = await gerenciadorVerificacaoB.FindByIdAsync(idUsuarioCompartilhado.ToString());
            Assert.NotNull(carregadoB);
            Assert.Equal("Usuário no Tenant B", carregadoB.NomeExibicao);
            Assert.True(await gerenciadorVerificacaoB.CheckPasswordAsync(carregadoB, senhaB));
            Assert.False(await gerenciadorVerificacaoB.CheckPasswordAsync(carregadoB, senhaA));
        }
    }

    [Fact]
    public async Task PermissoesLocais_ConcedemERevogamIndependentemente()
    {
        Guid idUsuario = Guid.NewGuid();
        const string email = "teste_perm@tenant.local";

        var (escopoA, gerenciadorUsuariosA, contextoBancoA) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
        await using (escopoA)
        {
            await gerenciadorUsuariosA.CreateAsync(new UsuarioAplicacao
            {
                Id = idUsuario,
                IdTenant = fixture.TenantA.IdTenant,
                UserName = email,
                Email = email,
                NomeExibicao = "Usuário A"
            }, "TesteSenha123!");

            contextoBancoA.Permissoes.Add(new PermissaoUsuario
            {
                IdUsuario = idUsuario,
                CodigoPermissao = "fundacao.contexto.ler",
                ConcedidaEm = DateTimeOffset.UtcNow,
                Versao = 1
            });
            await contextoBancoA.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (escopoB, gerenciadorUsuariosB, contextoBancoB) = CriarEscopoCliente(fixture.TenantB.StringConexaoExecucao);
        await using (escopoB)
        {
            await gerenciadorUsuariosB.CreateAsync(new UsuarioAplicacao
            {
                Id = idUsuario,
                IdTenant = fixture.TenantB.IdTenant,
                UserName = email,
                Email = email,
                NomeExibicao = "Usuário B"
            }, "TesteSenha123!");

            contextoBancoB.Permissoes.Add(new PermissaoUsuario
            {
                IdUsuario = idUsuario,
                CodigoPermissao = "modulo.personalizado.acesso",
                ConcedidaEm = DateTimeOffset.UtcNow,
                Versao = 1
            });
            await contextoBancoB.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Verifica permissões distintas
        var (escopoChecagemA, _, dbChecagemA) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
        await using (escopoChecagemA)
        {
            var permissoesA = await dbChecagemA.Permissoes.Where(p => p.IdUsuario == idUsuario).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(permissoesA);
            Assert.Equal("fundacao.contexto.ler", permissoesA[0].CodigoPermissao);

            // Revoga permissão no Tenant A
            dbChecagemA.Permissoes.Remove(permissoesA[0]);
            await dbChecagemA.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (escopoChecagemB, _, dbChecagemB) = CriarEscopoCliente(fixture.TenantB.StringConexaoExecucao);
        await using (escopoChecagemB)
        {
            var permissoesB = await dbChecagemB.Permissoes.Where(p => p.IdUsuario == idUsuario).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(permissoesB);
            Assert.Equal("modulo.personalizado.acesso", permissoesB[0].CodigoPermissao);
        }

        // Confirma que Tenant A tem 0 permissões e Tenant B continua intacto
        var (escopoFinalA, _, dbFinalA) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
        await using (escopoFinalA)
        {
            Assert.Empty(await dbFinalA.Permissoes.Where(p => p.IdUsuario == idUsuario).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task UnicidadeEmail_AplicadaLocalmentePorBanco()
    {
        const string emailDuplicado = "duplicado@tenant.local";
        var (escopoA, gerenciadorUsuariosA, _) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
        await using (escopoA)
        {
            var usuario1 = new UsuarioAplicacao
            {
                Id = Guid.NewGuid(),
                IdTenant = fixture.TenantA.IdTenant,
                UserName = emailDuplicado,
                Email = emailDuplicado,
                NomeExibicao = "Usuário 1"
            };
            var resultado1 = await gerenciadorUsuariosA.CreateAsync(usuario1, "Senha123!");
            Assert.True(resultado1.Succeeded);

            var usuario2 = new UsuarioAplicacao
            {
                Id = Guid.NewGuid(),
                IdTenant = fixture.TenantA.IdTenant,
                UserName = emailDuplicado,
                Email = emailDuplicado,
                NomeExibicao = "Usuário 2"
            };
            var resultado2 = await gerenciadorUsuariosA.CreateAsync(usuario2, "Senha123!");
            Assert.False(resultado2.Succeeded);
        }
    }

    [Fact]
    public async Task RestricaoChaveEstrangeira_ImpedeContaComIdTenantDivergente()
    {
        var (escopoA, gerenciadorUsuariosA, _) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
        await using (escopoA)
        {
            UsuarioAplicacao usuarioTenantInvalido = new()
            {
                Id = Guid.NewGuid(),
                IdTenant = fixture.TenantB.IdTenant, // ID de tenant divergente!
                UserName = "divergente@tenant.local",
                Email = "divergente@tenant.local",
                NomeExibicao = "Usuário Tenant Inválido"
            };

            await Assert.ThrowsAnyAsync<DbUpdateException>(
                () => gerenciadorUsuariosA.CreateAsync(usuarioTenantInvalido, "Senha123!"));
        }
    }

    [Fact]
    public async Task Transacoes_SuportamCommitERollback()
    {
        Guid idUsuarioRollback = Guid.NewGuid();
        Guid idUsuarioCommit = Guid.NewGuid();

        var (escopo, _, contextoBanco) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
        await using (escopo)
        {
            // 1. Rollback
            await using (var transacao = await contextoBanco.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                contextoBanco.Users.Add(new UsuarioAplicacao
                {
                    Id = idUsuarioRollback,
                    IdTenant = fixture.TenantA.IdTenant,
                    UserName = "rollback@tenant.local",
                    Email = "rollback@tenant.local",
                    NomeExibicao = "Usuário Rollback"
                });
                await contextoBanco.SaveChangesAsync(TestContext.Current.CancellationToken);
                await transacao.RollbackAsync(TestContext.Current.CancellationToken);
            }

            contextoBanco.ChangeTracker.Clear();
            Assert.Null(await contextoBanco.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == idUsuarioRollback, TestContext.Current.CancellationToken));

            // 2. Commit
            await using (var transacao = await contextoBanco.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                contextoBanco.Users.Add(new UsuarioAplicacao
                {
                    Id = idUsuarioCommit,
                    IdTenant = fixture.TenantA.IdTenant,
                    UserName = "commit@tenant.local",
                    Email = "commit@tenant.local",
                    NomeExibicao = "Usuário Commit"
                });
                await contextoBanco.SaveChangesAsync(TestContext.Current.CancellationToken);
                await transacao.CommitAsync(TestContext.Current.CancellationToken);
            }

            contextoBanco.ChangeTracker.Clear();
            Assert.NotNull(await contextoBanco.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == idUsuarioCommit, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task BarreiraConcorrencia_OperacoesSobrepostasComFalhaEmADeixamBIntacto()
    {
        Guid idUsuarioA = Guid.NewGuid();
        Guid idUsuarioB = Guid.NewGuid();
        using Barrier barreira = new(2);
        int picoConcorrenciaObservado = 0;
        int ativosEmTransacao = 0;

        Task tarefaA = Task.Run(async () =>
        {
            var (escopoA, _, dbA) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
            await using (escopoA)
            await using (var txA = await dbA.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                int atual = Interlocked.Increment(ref ativosEmTransacao);
                lock (barreira)
                {
                    if (atual > picoConcorrenciaObservado)
                    {
                        picoConcorrenciaObservado = atual;
                    }
                }

                dbA.Users.Add(new UsuarioAplicacao
                {
                    Id = idUsuarioA,
                    IdTenant = fixture.TenantA.IdTenant,
                    UserName = "concorrente_a@tenant.local",
                    Email = "concorrente_a@tenant.local",
                    NomeExibicao = "Concorrente A"
                });
                await dbA.SaveChangesAsync(TestContext.Current.CancellationToken);

                // Aguarda na barreira para garantir transações ativas simultaneamente
                barreira.SignalAndWait();

                // Falha deliberada e rollback em A
                await txA.RollbackAsync(TestContext.Current.CancellationToken);
                Interlocked.Decrement(ref ativosEmTransacao);
            }
        }, TestContext.Current.CancellationToken);

        Task tarefaB = Task.Run(async () =>
        {
            var (escopoB, _, dbB) = CriarEscopoCliente(fixture.TenantB.StringConexaoExecucao);
            await using (escopoB)
            await using (var txB = await dbB.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                int atual = Interlocked.Increment(ref ativosEmTransacao);
                lock (barreira)
                {
                    if (atual > picoConcorrenciaObservado)
                    {
                        picoConcorrenciaObservado = atual;
                    }
                }

                dbB.Users.Add(new UsuarioAplicacao
                {
                    Id = idUsuarioB,
                    IdTenant = fixture.TenantB.IdTenant,
                    UserName = "concorrente_b@tenant.local",
                    Email = "concorrente_b@tenant.local",
                    NomeExibicao = "Concorrente B"
                });
                await dbB.SaveChangesAsync(TestContext.Current.CancellationToken);

                // Aguarda na barreira para garantir transações ativas simultaneamente
                barreira.SignalAndWait();

                // Commit com sucesso em B
                await txB.CommitAsync(TestContext.Current.CancellationToken);
                Interlocked.Decrement(ref ativosEmTransacao);
            }
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(tarefaA, tarefaB);

        Assert.True(picoConcorrenciaObservado >= 2, $"Pico de concorrência foi {picoConcorrenciaObservado}, esperado >= 2");

        // Verifica que o Tenant A realizou rollback e não possui idUsuarioA
        var (escopoChecagemA, _, dbChecagemA) = CriarEscopoCliente(fixture.TenantA.StringConexaoExecucao);
        await using (escopoChecagemA)
        {
            Assert.Null(await dbChecagemA.Users.FindAsync([idUsuarioA], TestContext.Current.CancellationToken));
        }

        // Verifica que o Tenant B realizou commit e possui idUsuarioB
        var (escopoChecagemB, _, dbChecagemB) = CriarEscopoCliente(fixture.TenantB.StringConexaoExecucao);
        await using (escopoChecagemB)
        {
            Assert.NotNull(await dbChecagemB.Users.FindAsync([idUsuarioB], TestContext.Current.CancellationToken));
        }
    }

    private static (ServiceProvider Provedor, UserManager<UsuarioAplicacao> GerenciadorUsuarios, AcessoTenantDbContext ContextoBanco) CriarEscopoCliente(
        string stringConexao)
    {
        ServiceCollection servicos = new();
        servicos.AddLogging();
        servicos.AddDbContext<AcessoTenantDbContext>(opcoes =>
        {
            opcoes.UseNpgsql(stringConexao, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
            });
        });
        servicos.AddIdentityCore<UsuarioAplicacao>(opcoes =>
        {
            opcoes.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AcessoTenantDbContext>();

        ServiceProvider provedor = servicos.BuildServiceProvider();
        return (provedor, provedor.GetRequiredService<UserManager<UsuarioAplicacao>>(), provedor.GetRequiredService<AcessoTenantDbContext>());
    }
}
