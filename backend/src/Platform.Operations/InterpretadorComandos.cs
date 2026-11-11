namespace Platform.Operations;

using Platform.Domain.Access;

/// <summary>
/// Tipos de comandos aceitos pelo host operacional (FR-033).
/// </summary>
public enum TipoComandoOperacao
{
    ContaCriar,
    ContaDesativar,
    ContaReativar,
    ContaRedefinirSenha,
    ContaRevogarSessoes,
    PermissaoConceder,
    PermissaoRevogar,
    SessaoLimpar
}

/// <summary>
/// Requisição parseada de um comando operacional com argumentos validados.
/// </summary>
public sealed record RequisicaoComando(
    TipoComandoOperacao Tipo,
    Guid IdTenant,
    Guid IdAmbiente,
    Guid? IdUsuario = null,
    string? Email = null,
    string? NomeExibicao = null,
    string? CodigoPermissao = null);

/// <summary>
/// Interpretador manual de linha de comando para o host operacional (sem biblioteca externa de CLI).
/// Valida verbos, parâmetros conhecidos, ausência de duplicatas, UUIDs não vazios e opções proibidas.
/// </summary>
public static class InterpretadorComandos
{
    private static readonly HashSet<string> ParametrosProibidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "--actor",
        "--senha",
        "--password",
        "--retencao-dias"
    };

    /// <summary>
    /// Interpreta os argumentos de linha de comando e retorna a requisição validada.
    /// </summary>
    /// <param name="args">Argumentos passados ao processo.</param>
    /// <returns>A requisição do comando.</returns>
    /// <exception cref="ArgumentException">Se os argumentos forem inválidos, ausentes, duplicados ou proibidos.</exception>
    public static RequisicaoComando Interpretar(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length < 2)
        {
            throw new ArgumentException("Comando incompleto. É necessário informar verbo e ação (ex.: conta criar, sessao limpar).");
        }

        string verbo = args[0];
        string acao = args[1];

        TipoComandoOperacao tipo = (verbo, acao) switch
        {
            ("conta", "criar") => TipoComandoOperacao.ContaCriar,
            ("conta", "desativar") => TipoComandoOperacao.ContaDesativar,
            ("conta", "reativar") => TipoComandoOperacao.ContaReativar,
            ("conta", "redefinir-senha") => TipoComandoOperacao.ContaRedefinirSenha,
            ("conta", "revogar-sessoes") => TipoComandoOperacao.ContaRevogarSessoes,
            ("permissao", "conceder") => TipoComandoOperacao.PermissaoConceder,
            ("permissao", "revogar") => TipoComandoOperacao.PermissaoRevogar,
            ("sessao", "limpar") => TipoComandoOperacao.SessaoLimpar,
            _ => throw new ArgumentException($"Comando '{verbo} {acao}' desconhecido.")
        };

        Dictionary<string, string> parametros = [];
        for (int i = 2; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Argumento inesperado '{arg}'. Parâmetros devem iniciar com '--'.");
            }

            if (ParametrosProibidos.Contains(arg))
            {
                throw new ArgumentException($"O parâmetro '{arg}' é expressamente proibido.");
            }

            if (parametros.ContainsKey(arg))
            {
                throw new ArgumentException($"O parâmetro '{arg}' foi informado mais de uma vez.");
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"O parâmetro '{arg}' exige um valor.");
            }

            parametros[arg] = args[++i];
        }

        Guid idTenant = ObterGuidObrigatorio(parametros, "--id-tenant");
        Guid idAmbiente = ObterGuidObrigatorio(parametros, "--id-ambiente");

        switch (tipo)
        {
            case TipoComandoOperacao.ContaCriar:
                {
                    ValidarApenasParametrosPermitidos(parametros, ["--id-tenant", "--id-ambiente", "--email", "--nome-exibicao"]);
                    string email = ObterStringObrigatoria(parametros, "--email");
                    string nomeExibicao = ObterStringObrigatoria(parametros, "--nome-exibicao");
                    return new RequisicaoComando(tipo, idTenant, idAmbiente, Email: email, NomeExibicao: nomeExibicao);
                }

            case TipoComandoOperacao.ContaDesativar:
            case TipoComandoOperacao.ContaReativar:
            case TipoComandoOperacao.ContaRedefinirSenha:
            case TipoComandoOperacao.ContaRevogarSessoes:
                {
                    ValidarApenasParametrosPermitidos(parametros, ["--id-tenant", "--id-ambiente", "--id-usuario"]);
                    Guid idUsuario = ObterGuidObrigatorio(parametros, "--id-usuario");
                    return new RequisicaoComando(tipo, idTenant, idAmbiente, IdUsuario: idUsuario);
                }

            case TipoComandoOperacao.PermissaoConceder:
            case TipoComandoOperacao.PermissaoRevogar:
                {
                    ValidarApenasParametrosPermitidos(parametros, ["--id-tenant", "--id-ambiente", "--id-usuario", "--codigo"]);
                    Guid idUsuario = ObterGuidObrigatorio(parametros, "--id-usuario");
                    string codigo = ObterStringObrigatoria(parametros, "--codigo");
                    if (!CodigosPermissao.EhConhecido(codigo))
                    {
                        throw new ArgumentException($"O código de permissão '{codigo}' não pertence ao catálogo conhecido.");
                    }

                    return new RequisicaoComando(tipo, idTenant, idAmbiente, IdUsuario: idUsuario, CodigoPermissao: codigo);
                }

            case TipoComandoOperacao.SessaoLimpar:
                {
                    ValidarApenasParametrosPermitidos(parametros, ["--id-tenant", "--id-ambiente"]);
                    return new RequisicaoComando(tipo, idTenant, idAmbiente);
                }

            default:
                throw new ArgumentException($"Tipo de comando '{tipo}' não suportado.");
        }
    }

    private static Guid ObterGuidObrigatorio(Dictionary<string, string> parametros, string nomeParametro)
    {
        if (!parametros.TryGetValue(nomeParametro, out string? valor))
        {
            throw new ArgumentException($"O parâmetro obrigatório '{nomeParametro}' não foi informado.");
        }

        if (!Guid.TryParse(valor, out Guid guid) || guid == Guid.Empty)
        {
            throw new ArgumentException($"O parâmetro '{nomeParametro}' deve ser um UUID válido e não vazio.");
        }

        return guid;
    }

    private static string ObterStringObrigatoria(Dictionary<string, string> parametros, string nomeParametro)
    {
        if (!parametros.TryGetValue(nomeParametro, out string? valor) || string.IsNullOrWhiteSpace(valor))
        {
            throw new ArgumentException($"O parâmetro obrigatório '{nomeParametro}' não foi informado ou está vazio.");
        }

        return valor;
    }

    private static void ValidarApenasParametrosPermitidos(Dictionary<string, string> parametros, string[] permitidos)
    {
        HashSet<string> conjuntoPermitidos = new(permitidos, StringComparer.OrdinalIgnoreCase);
        foreach (string param in parametros.Keys)
        {
            if (!conjuntoPermitidos.Contains(param))
            {
                throw new ArgumentException($"Parâmetro não reconhecido para este comando: '{param}'.");
            }
        }
    }
}
