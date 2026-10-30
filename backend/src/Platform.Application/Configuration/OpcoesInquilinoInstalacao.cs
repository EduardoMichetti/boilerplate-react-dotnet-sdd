using System.Data.Common;
using Platform.Domain.Tenancy;

namespace Platform.Application.Configuration;

/// <summary>
/// Opções de configuração essenciais para a inicialização e isolamento da instalação do inquilino.
/// </summary>
public sealed class OpcoesInquilinoInstalacao
{
    /// <summary>
    /// Identificador do produto ou aplicação derivado (de 1 a 100 caracteres).
    /// </summary>
    public string? IdAplicacao { get; init; }

    /// <summary>
    /// Identificador único e imutável do ambiente da instalação (UUID não vazio).
    /// </summary>
    public Guid IdAmbiente { get; init; }

    /// <summary>
    /// Identificador único e imutável do inquilino (UUID não vazio).
    /// </summary>
    public Guid IdInquilino { get; init; }

    /// <summary>
    /// Nome amigável de exibição do inquilino/empresa (de 1 a 120 caracteres).
    /// </summary>
    public string? NomeExibicaoInquilino { get; init; }

    /// <summary>
    /// Estado operacional configurado para a instalação (Inativo ou Ativo).
    /// </summary>
    public EstadoInquilino? EstadoInquilino { get; init; }

    /// <summary>
    /// Strings de conexão da instalação com o banco de dados dedicado.
    /// </summary>
    public StringsConexaoInstalacao? StringsConexao { get; init; }

    /// <summary>
    /// Valida as invariantes obrigatórias dos parâmetros de configuração da instalação.
    /// </summary>
    public void Validar()
    {
        List<string> erros = [];

        if (string.IsNullOrWhiteSpace(IdAplicacao) || IdAplicacao.Length > 100)
        {
            erros.Add("IdAplicacao deve conter entre 1 e 100 caracteres.");
        }

        if (IdAmbiente == Guid.Empty)
        {
            erros.Add("IdAmbiente deve ser um UUID não vazio.");
        }

        if (IdInquilino == Guid.Empty)
        {
            erros.Add("IdInquilino deve ser um UUID não vazio.");
        }

        if (string.IsNullOrWhiteSpace(NomeExibicaoInquilino) || NomeExibicaoInquilino.Length > 120)
        {
            erros.Add("NomeExibicaoInquilino deve conter entre 1 e 120 caracteres.");
        }

        if (EstadoInquilino is not { } estado || !Enum.IsDefined(estado))
        {
            erros.Add("EstadoInquilino deve ser explicitamente Inativo ou Ativo.");
        }

        if (string.IsNullOrWhiteSpace(StringsConexao?.BancoApp))
        {
            erros.Add("StringsConexao:BancoApp é obrigatória.");
        }
        else
        {
            try
            {
                _ = new DbConnectionStringBuilder { ConnectionString = StringsConexao.BancoApp };
            }
            catch (ArgumentException)
            {
                erros.Add("StringsConexao:BancoApp possui sintaxe inválida.");
            }
        }

        if (erros.Count != 0)
        {
            throw new ExcecaoConfiguracaoInstalacao(erros);
        }
    }
}

/// <summary>
/// Contém as strings de conexão necessárias para acesso ao banco de dados da instalação.
/// </summary>
public sealed class StringsConexaoInstalacao
{
    /// <summary>
    /// String de conexão do banco de dados principal da aplicação para esta instalação.
    /// </summary>
    public string? BancoApp { get; init; }
}
