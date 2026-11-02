using System.Data.Common;
using Platform.Domain.Tenancy;

namespace Platform.Application.Configuration;

/// <summary>
/// Opções de configuração essenciais para a inicialização e isolamento da instalação do tenant.
/// </summary>
public sealed class OpcoesInstalacaoTenant
{
    /// <summary>
    /// Identificador do produto ou aplicação derivado (de 1 a 100 caracteres).
    /// </summary>
    public string? IdAplicacao { get; set; }

    /// <summary>
    /// Identificador único e imutável do ambiente da instalação (UUID não vazio).
    /// </summary>
    public Guid IdAmbiente { get; set; }

    /// <summary>
    /// Identificador único e imutável do tenant (UUID não vazio).
    /// </summary>
    public Guid IdTenant { get; set; }

    /// <summary>
    /// Nome amigável de exibição do tenant/empresa (de 1 a 120 caracteres).
    /// </summary>
    public string? NomeExibicaoTenant { get; set; }

    /// <summary>
    /// Estado operacional configurado para a instalação (Inativo ou Ativo).
    /// </summary>
    public EstadoTenant? EstadoTenant { get; set; }

    /// <summary>
    /// Strings de conexão da instalação com o banco de dados dedicado.
    /// </summary>
    public StringsConexaoInstalacao? StringsConexao { get; set; }

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

        if (IdTenant == Guid.Empty)
        {
            erros.Add("IdTenant deve ser um UUID não vazio.");
        }

        if (string.IsNullOrWhiteSpace(NomeExibicaoTenant) || NomeExibicaoTenant.Length > 120)
        {
            erros.Add("NomeExibicaoTenant deve conter entre 1 e 120 caracteres.");
        }

        if (EstadoTenant is not { } estado || !Enum.IsDefined(estado))
        {
            erros.Add("EstadoTenant deve ser explicitamente Inativo ou Ativo.");
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
    public string? BancoApp { get; set; }
}
