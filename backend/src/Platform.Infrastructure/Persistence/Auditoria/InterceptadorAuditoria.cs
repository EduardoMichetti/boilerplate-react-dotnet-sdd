namespace Platform.Infrastructure.Persistence.Auditoria;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Platform.Application.Auditoria;
using Platform.Domain.Auditoria;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Preenche centralmente os metadados de auditoria a partir de <see cref="IContextoOperacao"/>,
/// na mesma transação dos dados (data-model §4): criação em <c>Added</c>; última alteração e
/// incremento de <see cref="IEntidadeVersionada.Versao"/> somente em alteração efetiva; rejeita
/// mudança de campos de criação, alteração de concessão e alteração/remoção do histórico.
/// </summary>
/// <param name="contexto">Contexto confiável da operação corrente.</param>
public sealed class InterceptadorAuditoria(IContextoOperacao contexto) : SaveChangesInterceptor
{
    private static readonly HashSet<string> CamposCriacao =
    [
        nameof(IEntidadeAuditavel.CriadoEm),
        nameof(IEntidadeAuditavel.TipoAutorCriacao),
        nameof(IEntidadeAuditavel.AutorCriacao),
        nameof(IEntidadeAuditavel.IdOperacaoCriacao),
    ];

    /// <summary>
    /// Campos da sessão fixados na emissão (D31): teto e janela nunca mudam depois da criação.
    /// </summary>
    private static readonly HashSet<string> CamposImutaveisSessao =
    [
        nameof(SessaoAutenticacao.TetoAbsolutoEm),
        nameof(SessaoAutenticacao.JanelaInatividadeMinutos),
    ];

    private static readonly HashSet<string> CamposAlteracao =
    [
        nameof(IEntidadeAuditavel.AlteradoEm),
        nameof(IEntidadeAuditavel.TipoAutorAlteracao),
        nameof(IEntidadeAuditavel.AutorAlteracao),
        nameof(IEntidadeAuditavel.IdOperacaoAlteracao),
        nameof(IEntidadeVersionada.Versao),
    ];

    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Aplicar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Aplicar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Aplicar(DbContext? contextoDb)
    {
        if (contextoDb is null)
        {
            return;
        }

        contextoDb.ChangeTracker.DetectChanges();

        foreach (EntityEntry entrada in contextoDb.ChangeTracker.Entries().ToList())
        {
            if (entrada.Entity is RegistroOperacaoAcesso && entrada.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("RegistroOperacaoAcesso é append-only: alteração e remoção são proibidas.");
            }

            if (entrada.Entity is PermissaoUsuario && entrada.State == EntityState.Modified)
            {
                throw new InvalidOperationException("PermissaoUsuario é imutável: use INSERT ou DELETE.");
            }

            if (entrada.Entity is not IEntidadeAuditavel auditavel)
            {
                continue;
            }

            switch (entrada.State)
            {
                case EntityState.Added:
                    PreencherCriacao(auditavel);
                    break;
                case EntityState.Modified:
                    AplicarAlteracao(entrada, auditavel);
                    break;
                default:
                    break;
            }
        }
    }

    private void PreencherCriacao(IEntidadeAuditavel auditavel)
    {
        string? autor = ObterAutorCoerente();
        auditavel.CriadoEm = contexto.InstanteOperacao;
        auditavel.TipoAutorCriacao = contexto.TipoAutor;
        auditavel.AutorCriacao = autor;
        auditavel.IdOperacaoCriacao = contexto.IdOperacao;
        auditavel.AlteradoEm = null;
        auditavel.TipoAutorAlteracao = null;
        auditavel.AutorAlteracao = null;
        auditavel.IdOperacaoAlteracao = null;

        if (auditavel is IEntidadeVersionada versionada && versionada.Versao <= 0)
        {
            versionada.Versao = 1;
        }
    }

    private void AplicarAlteracao(EntityEntry entrada, IEntidadeAuditavel auditavel)
    {
        bool alteracaoEfetiva = false;

        foreach (PropertyEntry propriedade in entrada.Properties)
        {
            string nome = propriedade.Metadata.Name;
            bool mudou = !propriedade.Metadata.GetValueComparer().Equals(propriedade.OriginalValue, propriedade.CurrentValue);

            if (CamposCriacao.Contains(nome) || (entrada.Entity is SessaoAutenticacao && CamposImutaveisSessao.Contains(nome)))
            {
                if (mudou)
                {
                    throw new InvalidOperationException($"Campo {nome} é imutável após a criação.");
                }

                continue;
            }

            if (CamposAlteracao.Contains(nome) || EhCarimboConcorrenciaIdentity(entrada, nome))
            {
                continue;
            }

            if (mudou)
            {
                alteracaoEfetiva = true;
            }
        }

        if (!alteracaoEfetiva)
        {
            // No-op: não altera metadados nem versão. O carimbo de concorrência do Identity
            // continua sendo gravado/conferido normalmente pelo próprio store.
            return;
        }

        string? autor = ObterAutorCoerente();
        auditavel.AlteradoEm = contexto.InstanteOperacao;
        auditavel.TipoAutorAlteracao = contexto.TipoAutor;
        auditavel.AutorAlteracao = autor;
        auditavel.IdOperacaoAlteracao = contexto.IdOperacao;

        if (auditavel is IEntidadeVersionada versionada)
        {
            versionada.Versao++;
        }
    }

    /// <summary>
    /// Garante a mesma regra das constraints <c>ck_*_auditoria_*</c> antes de chegar ao banco:
    /// autor nulo se e somente se o tipo for <see cref="TipoAutor.Anonimo"/>.
    /// </summary>
    private string? ObterAutorCoerente()
    {
        string? autor = contexto.Autor;
        bool anonimo = contexto.TipoAutor == TipoAutor.Anonimo;
        if (anonimo != (autor is null))
        {
            throw new InvalidOperationException(
                $"Contexto de auditoria incoerente: tipo de autor {contexto.TipoAutor} " +
                (anonimo ? "não pode ter autor." : "exige autor verificado antes da gravação."));
        }

        return autor;
    }

    private static bool EhCarimboConcorrenciaIdentity(EntityEntry entrada, string nomePropriedade) =>
        entrada.Entity is UsuarioAplicacao && nomePropriedade == nameof(UsuarioAplicacao.ConcurrencyStamp);
}
