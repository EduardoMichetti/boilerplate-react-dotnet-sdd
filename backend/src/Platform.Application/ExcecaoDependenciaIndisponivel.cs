namespace Platform.Application;

/// <summary>
/// Falha de dependência obrigatória (banco, key ring, marcador/versão divergente) tratada de forma
/// centralizada como 503, nunca convertida em "ausente", 401 silencioso ou sucesso aparente.
/// </summary>
public sealed class ExcecaoDependenciaIndisponivel : Exception
{
    /// <summary>
    /// Cria a exceção com mensagem técnica segura (sem conexão, SQL ou valores sensíveis).
    /// </summary>
    /// <param name="mensagem">Descrição técnica segura.</param>
    public ExcecaoDependenciaIndisponivel(string mensagem)
        : base(mensagem)
    {
    }

    /// <summary>
    /// Cria a exceção encapsulando a causa original (não exposta ao consumidor).
    /// </summary>
    /// <param name="mensagem">Descrição técnica segura.</param>
    /// <param name="causa">Exceção original.</param>
    public ExcecaoDependenciaIndisponivel(string mensagem, Exception causa)
        : base(mensagem, causa)
    {
    }
}
