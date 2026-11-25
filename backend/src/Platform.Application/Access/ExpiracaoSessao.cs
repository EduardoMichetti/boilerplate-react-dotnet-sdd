namespace Platform.Application.Access;

using Platform.Domain.Tempo;

/// <summary>
/// Regra pura de expiração da sessão (D31) e limitação do registro de uso (D33). Não lê relógio:
/// todos os instantes vêm do chamador (<c>InstanteOperacao</c>) e são validados por <see cref="InstanteUtc"/>.
/// </summary>
public static class ExpiracaoSessao
{
    /// <summary>
    /// Intervalo mínimo entre duas gravações de <c>UltimoUsoEm</c> (1 escrita/minuto; não configurável).
    /// A sessão pode expirar até 60 s antes do que a janela nominal indicaria (tolerância a favor da segurança).
    /// </summary>
    public static readonly TimeSpan IntervaloMinimoRegistroUso = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Calcula a expiração efetiva: <c>min(UltimoUsoEm + janela, TetoAbsolutoEm)</c>.
    /// </summary>
    /// <param name="ultimoUsoEm">Último uso registrado (UTC).</param>
    /// <param name="janelaMinutos">Janela de inatividade gravada na emissão.</param>
    /// <param name="tetoAbsolutoEm">Teto absoluto gravado na emissão (UTC).</param>
    /// <returns>Instante a partir do qual a sessão não autoriza mais uso.</returns>
    /// <exception cref="ArgumentException">Instante fora de UTC ou não definido.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Janela não positiva.</exception>
    public static DateTimeOffset CalcularExpiracaoEfetiva(DateTimeOffset ultimoUsoEm, int janelaMinutos, DateTimeOffset tetoAbsolutoEm)
    {
        InstanteUtc.Validar(ultimoUsoEm, nameof(ultimoUsoEm));
        InstanteUtc.Validar(tetoAbsolutoEm, nameof(tetoAbsolutoEm));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(janelaMinutos);

        DateTimeOffset fimInatividade = ultimoUsoEm.AddMinutes(janelaMinutos);
        return fimInatividade < tetoAbsolutoEm ? fimInatividade : tetoAbsolutoEm;
    }

    /// <summary>
    /// Avalia se a sessão está expirada no instante informado.
    /// </summary>
    /// <param name="agora">Instante da operação (UTC).</param>
    /// <param name="ultimoUsoEm">Último uso registrado (UTC).</param>
    /// <param name="janelaMinutos">Janela de inatividade gravada na emissão.</param>
    /// <param name="tetoAbsolutoEm">Teto absoluto gravado na emissão (UTC).</param>
    /// <returns>
    /// <c>null</c> quando válida; <see cref="MotivoNegacaoSessao.TetoAbsoluto"/> quando o teto venceu (prevalece);
    /// senão <see cref="MotivoNegacaoSessao.Inatividade"/>.
    /// </returns>
    /// <exception cref="ArgumentException">Instante fora de UTC ou não definido.</exception>
    public static MotivoNegacaoSessao? Avaliar(DateTimeOffset agora, DateTimeOffset ultimoUsoEm, int janelaMinutos, DateTimeOffset tetoAbsolutoEm)
    {
        InstanteUtc.Validar(agora, nameof(agora));
        DateTimeOffset expiracao = CalcularExpiracaoEfetiva(ultimoUsoEm, janelaMinutos, tetoAbsolutoEm);

        if (agora >= tetoAbsolutoEm)
        {
            return MotivoNegacaoSessao.TetoAbsoluto;
        }

        return agora >= expiracao ? MotivoNegacaoSessao.Inatividade : null;
    }

    /// <summary>
    /// Indica se uma requisição aceita no instante informado deve gravar <c>UltimoUsoEm</c>
    /// (<c>instante − ultimoUsoEm ≥ 60 s</c>).
    /// </summary>
    /// <param name="instante">Instante da operação (UTC).</param>
    /// <param name="ultimoUsoEm">Último uso registrado (UTC).</param>
    /// <returns><c>true</c> quando a gravação é devida.</returns>
    /// <exception cref="ArgumentException">Instante fora de UTC ou não definido.</exception>
    public static bool DeveRegistrarUso(DateTimeOffset instante, DateTimeOffset ultimoUsoEm)
    {
        InstanteUtc.Validar(instante, nameof(instante));
        InstanteUtc.Validar(ultimoUsoEm, nameof(ultimoUsoEm));
        return instante - ultimoUsoEm >= IntervaloMinimoRegistroUso;
    }
}
