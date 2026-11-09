namespace App.Api.Access;

using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Platform.Application.Access.Portas;

/// <summary>
/// Claims do ticket persistido: só referências e o carimbo de segurança (sem concessões).
/// <c>sub</c> é a sessão, para que o token antiforgery fique vinculado à sessão corrente.
/// </summary>
public static class ClaimsSessao
{
    /// <summary>Sessão (vincula o CSRF à sessão corrente).</summary>
    public const string Sessao = "sub";

    /// <summary>Conta dona da sessão.</summary>
    public const string Usuario = ClaimTypes.NameIdentifier;

    /// <summary>Tenant da instalação.</summary>
    public const string Tenant = "bps:tenant";

    /// <summary>Época do marcador na emissão.</summary>
    public const string Epoca = "bps:epoca";

    /// <summary>Carimbo de segurança da conta na emissão.</summary>
    public const string Carimbo = "bps:carimbo";
}

/// <summary>
/// Serializa e protege por Data Protection o ticket nativo persistido em <c>TicketProtegido</c>.
/// </summary>
/// <param name="provedor">Data Protection da instalação (key ring e nome lógico próprios).</param>
public sealed class ProtetorTicketSessao(IDataProtectionProvider provedor) : IProtetorTicketSessao
{
    /// <summary>
    /// Propósito do protetor do ticket persistido.
    /// </summary>
    public const string Proposito = "App.Api.Access.TicketSessaoPersistido.v1";

    private readonly IDataProtector _protetor = provedor.CreateProtector(Proposito);

    /// <inheritdoc />
    public byte[] Proteger(DadosTicketSessao dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ClaimsIdentity identidade = new(
            [
                new Claim(ClaimsSessao.Sessao, dados.IdSessao.ToString()),
                new Claim(ClaimsSessao.Usuario, dados.IdUsuario.ToString()),
                new Claim(ClaimsSessao.Tenant, dados.IdTenant.ToString()),
                new Claim(ClaimsSessao.Epoca, dados.EpocaAcesso.ToString()),
                new Claim(ClaimsSessao.Carimbo, dados.CarimboSeguranca),
            ],
            ConfiguracaoAutenticacaoSessao.Esquema,
            ClaimTypes.NameIdentifier,
            ClaimTypes.Role);

        AuthenticationProperties propriedades = new()
        {
            IssuedUtc = dados.EmitidaEm,
            ExpiresUtc = dados.ExpiraEm,
            IsPersistent = true,
            AllowRefresh = false,
        };
        propriedades.Items["bps:emitida"] = dados.EmitidaEm.ToString("O", CultureInfo.InvariantCulture);

        return ProtegerTicket(new AuthenticationTicket(new ClaimsPrincipal(identidade), propriedades, ConfiguracaoAutenticacaoSessao.Esquema));
    }

    /// <summary>
    /// Protege um ticket já montado (usado pelo <c>RenewAsync</c>).
    /// </summary>
    /// <param name="ticket">Ticket.</param>
    /// <returns>Bytes protegidos.</returns>
    public byte[] ProtegerTicket(AuthenticationTicket ticket) =>
        _protetor.Protect(TicketSerializer.Default.Serialize(ticket));

    /// <summary>
    /// Desprotege o ticket persistido.
    /// </summary>
    /// <param name="protegido">Bytes gravados.</param>
    /// <returns>Ticket ou nulo quando o key ring não consegue desprotegê-lo.</returns>
    public AuthenticationTicket? Desproteger(byte[] protegido)
    {
        ArgumentNullException.ThrowIfNull(protegido);
        try
        {
            return TicketSerializer.Default.Deserialize(_protetor.Unprotect(protegido));
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
