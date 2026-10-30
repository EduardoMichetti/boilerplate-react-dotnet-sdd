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

public sealed class TestesPersistenciaUsuarioInquilino(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>, IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        CancellationToken tokenCancelamento = TestContext.Current.CancellationToken;

        // Garante que ambas as bases estejam migradas antes de rodar os testes de persistência
        foreach (InstalacaoTestePostgres inquilino in new[] { fixture.InquilinoA, fixture.InquilinoB })
        {
            DbContextOptionsBuilder<ContextoBancoAcessoInquilino> construtor = new();
            construtor.UseNpgsql(inquilino.StringConexaoProprietario, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
            });
            construtor.UseSeeding((contexto, _) =>
            {
                if (contexto is ContextoBancoAcessoInquilino contextoInquilino)
                {
                    PreValidacaoMigracaoInquilino.PopularIdentidadeBanco(contextoInquilino, inquilino.IdInquilino, inquilino.IdAmbiente);
                }
            });
            construtor.UseAsyncSeeding(async (contexto, _, ct) =>
            {
                if (contexto is ContextoBancoAcessoInquilino contextoInquilino)
                {
                    await PreValidacaoMigracaoInquilino.PopularIdentidadeBancoAsync(contextoInquilino, inquilino.IdInquilino, inquilino.IdAmbiente, ct);
                }
            });

            await using (ContextoBancoAcessoInquilino contexto = new(construtor.Options))
            {
                await contexto.Database.MigrateAsync(tokenCancelamento);
            }

            await inquilino.ConcederPermissoesExecucaoAsync(tokenCancelamento);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task MesmoEmailEId_CriamContasIndependentesComSenhasDistintas()
    {
        Guid idUsuarioCompartilhado = Guid.NewGuid();
        const string emailCompartilhado = "comum@inquilino.local";
        const string senhaA = "SenhaInquilinoA123!";
        const string senhaB = "SenhaInquilinoB456!";

        var (escopoA, gerenciadorUsuariosA, _) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
        await using (escopoA)
        {
            UsuarioAplicacao usuarioA = new()
            {
                Id = idUsuarioCompartilhado,
                IdInquilino = fixture.InquilinoA.IdInquilino,
                UserName = emailCompartilhado,
                Email = emailCompartilhado,
                NomeExibicao = "Usuário no Inquilino A",
                EstaAtivo = true
            };

            IdentityResult resultadoA = await gerenciadorUsuariosA.CreateAsync(usuarioA, senhaA);
            Assert.True(resultadoA.Succeeded, string.Join("; ", resultadoA.Errors.Select(e => e.Description)));
        }

        var (escopoB, gerenciadorUsuariosB, _) = CriarEscopoCliente(fixture.InquilinoB.StringConexaoExecucao);
        await using (escopoB)
        {
            UsuarioAplicacao usuarioB = new()
            {
                Id = idUsuarioCompartilhado,
                IdInquilino = fixture.InquilinoB.IdInquilino,
                UserName = emailCompartilhado,
                Email = emailCompartilhado,
                NomeExibicao = "Usuário no Inquilino B",
                EstaAtivo = true
            };

            IdentityResult resultadoB = await gerenciadorUsuariosB.CreateAsync(usuarioB, senhaB);
            Assert.True(resultadoB.Succeeded, string.Join("; ", resultadoB.Errors.Select(e => e.Description)));
        }

        // Verifica que as senhas e hashes são independentes e isolados
        var (escopoVerificacaoA, gerenciadorVerificacaoA, _) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
        await using (escopoVerificacaoA)
        {
            UsuarioAplicacao? carregadoA = await gerenciadorVerificacaoA.FindByIdAsync(idUsuarioCompartilhado.ToString());
            Assert.NotNull(carregadoA);
            Assert.Equal("Usuário no Inquilino A", carregadoA.NomeExibicao);
            Assert.True(await gerenciadorVerificacaoA.CheckPasswordAsync(carregadoA, senhaA));
            Assert.False(await gerenciadorVerificacaoA.CheckPasswordAsync(carregadoA, senhaB));
        }

        var (escopoVerificacaoB, gerenciadorVerificacaoB, _) = CriarEscopoCliente(fixture.InquilinoB.StringConexaoExecucao);
        await using (escopoVerificacaoB)
        {
            UsuarioAplicacao? carregadoB = await gerenciadorVerificacaoB.FindByIdAsync(idUsuarioCompartilhado.ToString());
            Assert.NotNull(carregadoB);
            Assert.Equal("Usuário no Inquilino B", carregadoB.NomeExibicao);
            Assert.True(await gerenciadorVerificacaoB.CheckPasswordAsync(carregadoB, senhaB));
            Assert.False(await gerenciadorVerificacaoB.CheckPasswordAsync(carregadoB, senhaA));
        }
    }

    [Fact]
    public async Task PermissoesLocais_ConcedemERevogamIndependentemente()
    {
        Guid idUsuario = Guid.NewGuid();
        const string email = "teste_perm@inquilino.local";

        var (escopoA, gerenciadorUsuariosA, contextoBancoA) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
        await using (escopoA)
        {
            await gerenciadorUsuariosA.CreateAsync(new UsuarioAplicacao
            {
                Id = idUsuario,
                IdInquilino = fixture.InquilinoA.IdInquilino,
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

        var (escopoB, gerenciadorUsuariosB, contextoBancoB) = CriarEscopoCliente(fixture.InquilinoB.StringConexaoExecucao);
        await using (escopoB)
        {
            await gerenciadorUsuariosB.CreateAsync(new UsuarioAplicacao
            {
                Id = idUsuario,
                IdInquilino = fixture.InquilinoB.IdInquilino,
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
        var (escopoChecagemA, _, dbChecagemA) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
        await using (escopoChecagemA)
        {
            var permissoesA = await dbChecagemA.Permissoes.Where(p => p.IdUsuario == idUsuario).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(permissoesA);
            Assert.Equal("fundacao.contexto.ler", permissoesA[0].CodigoPermissao);

            // Revoga permissão no Inquilino A
            dbChecagemA.Permissoes.Remove(permissoesA[0]);
            await dbChecagemA.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (escopoChecagemB, _, dbChecagemB) = CriarEscopoCliente(fixture.InquilinoB.StringConexaoExecucao);
        await using (escopoChecagemB)
        {
            var permissoesB = await dbChecagemB.Permissoes.Where(p => p.IdUsuario == idUsuario).ToListAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Single(permissoesB);
            Assert.Equal("modulo.personalizado.acesso", permissoesB[0].CodigoPermissao);
        }

        // Confirma que Inquilino A tem 0 permissões e Inquilino B continua intacto
        var (escopoFinalA, _, dbFinalA) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
        await using (escopoFinalA)
        {
            Assert.Empty(await dbFinalA.Permissoes.Where(p => p.IdUsuario == idUsuario).ToListAsync(cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task UnicidadeEmail_AplicadaLocalmentePorBanco()
    {
        const string emailDuplicado = "duplicado@inquilino.local";
        var (escopoA, gerenciadorUsuariosA, _) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
        await using (escopoA)
        {
            var usuario1 = new UsuarioAplicacao
            {
                Id = Guid.NewGuid(),
                IdInquilino = fixture.InquilinoA.IdInquilino,
                UserName = emailDuplicado,
                Email = emailDuplicado,
                NomeExibicao = "Usuário 1"
            };
            var resultado1 = await gerenciadorUsuariosA.CreateAsync(usuario1, "Senha123!");
            Assert.True(resultado1.Succeeded);

            var usuario2 = new UsuarioAplicacao
            {
                Id = Guid.NewGuid(),
                IdInquilino = fixture.InquilinoA.IdInquilino,
                UserName = emailDuplicado,
                Email = emailDuplicado,
                NomeExibicao = "Usuário 2"
            };
            var resultado2 = await gerenciadorUsuariosA.CreateAsync(usuario2, "Senha123!");
            Assert.False(resultado2.Succeeded);
        }
    }

    [Fact]
    public async Task RestricaoChaveEstrangeira_ImpedeContaComIdInquilinoDivergente()
    {
        var (escopoA, gerenciadorUsuariosA, _) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
        await using (escopoA)
        {
            UsuarioAplicacao usuarioInquilinoInvalido = new()
            {
                Id = Guid.NewGuid(),
                IdInquilino = fixture.InquilinoB.IdInquilino, // ID de inquilino divergente!
                UserName = "divergente@inquilino.local",
                Email = "divergente@inquilino.local",
                NomeExibicao = "Usuário Inquilino Inválido"
            };

            await Assert.ThrowsAnyAsync<DbUpdateException>(
                () => gerenciadorUsuariosA.CreateAsync(usuarioInquilinoInvalido, "Senha123!"));
        }
    }

    [Fact]
    public async Task Transacoes_SuportamCommitERollback()
    {
        Guid idUsuarioRollback = Guid.NewGuid();
        Guid idUsuarioCommit = Guid.NewGuid();

        var (escopo, _, contextoBanco) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
        await using (escopo)
        {
            // 1. Rollback
            await using (var transacao = await contextoBanco.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
            {
                contextoBanco.Users.Add(new UsuarioAplicacao
                {
                    Id = idUsuarioRollback,
                    IdInquilino = fixture.InquilinoA.IdInquilino,
                    UserName = "rollback@inquilino.local",
                    Email = "rollback@inquilino.local",
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
                    IdInquilino = fixture.InquilinoA.IdInquilino,
                    UserName = "commit@inquilino.local",
                    Email = "commit@inquilino.local",
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
            var (escopoA, _, dbA) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
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
                    IdInquilino = fixture.InquilinoA.IdInquilino,
                    UserName = "concorrente_a@inquilino.local",
                    Email = "concorrente_a@inquilino.local",
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
            var (escopoB, _, dbB) = CriarEscopoCliente(fixture.InquilinoB.StringConexaoExecucao);
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
                    IdInquilino = fixture.InquilinoB.IdInquilino,
                    UserName = "concorrente_b@inquilino.local",
                    Email = "concorrente_b@inquilino.local",
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

        // Verifica que o Inquilino A realizou rollback e não possui idUsuarioA
        var (escopoChecagemA, _, dbChecagemA) = CriarEscopoCliente(fixture.InquilinoA.StringConexaoExecucao);
        await using (escopoChecagemA)
        {
            Assert.Null(await dbChecagemA.Users.FindAsync([idUsuarioA], TestContext.Current.CancellationToken));
        }

        // Verifica que o Inquilino B realizou commit e possui idUsuarioB
        var (escopoChecagemB, _, dbChecagemB) = CriarEscopoCliente(fixture.InquilinoB.StringConexaoExecucao);
        await using (escopoChecagemB)
        {
            Assert.NotNull(await dbChecagemB.Users.FindAsync([idUsuarioB], TestContext.Current.CancellationToken));
        }
    }

    private static (ServiceProvider Provedor, UserManager<UsuarioAplicacao> GerenciadorUsuarios, ContextoBancoAcessoInquilino ContextoBanco) CriarEscopoCliente(
        string stringConexao)
    {
        ServiceCollection servicos = new();
        servicos.AddLogging();
        servicos.AddDbContext<ContextoBancoAcessoInquilino>(opcoes =>
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
        .AddEntityFrameworkStores<ContextoBancoAcessoInquilino>();

        ServiceProvider provedor = servicos.BuildServiceProvider();
        return (provedor, provedor.GetRequiredService<UserManager<UsuarioAplicacao>>(), provedor.GetRequiredService<ContextoBancoAcessoInquilino>());
    }
}
