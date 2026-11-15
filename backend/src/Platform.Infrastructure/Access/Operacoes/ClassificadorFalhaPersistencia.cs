namespace Platform.Infrastructure.Access.Operacoes;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Application.Access.Operacoes.Portas;

/// <summary>
/// Classifica exceções do EF Core/Npgsql: conflito concorrente real é concorrência otimista
/// (<see cref="DbUpdateConcurrencyException"/>) ou SQLSTATE de unicidade (<c>23505</c>),
/// falha de serialização (<c>40001</c>) ou deadlock (<c>40P01</c>). Qualquer outra exceção
/// (restrição de auditoria, permissão, conexão, bug) não é conflito.
/// </summary>
public sealed class ClassificadorFalhaPersistencia : IClassificadorFalhaPersistencia
{
    private static readonly HashSet<string> EstadosConflito = new(StringComparer.Ordinal)
    {
        PostgresErrorCodes.UniqueViolation,
        PostgresErrorCodes.SerializationFailure,
        PostgresErrorCodes.DeadlockDetected,
    };

    /// <inheritdoc />
    public bool EhConflitoConcorrente(Exception excecao)
    {
        ArgumentNullException.ThrowIfNull(excecao);

        for (Exception? atual = excecao; atual is not null; atual = atual.InnerException)
        {
            if (atual is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (atual is PostgresException postgres && EstadosConflito.Contains(postgres.SqlState))
            {
                return true;
            }
        }

        return false;
    }
}
