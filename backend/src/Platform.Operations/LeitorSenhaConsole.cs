namespace Platform.Operations;

using System.Text;
using Platform.Application.Access.Operacoes.Portas;

/// <summary>
/// Provedor de leitura de senha via console interativo sem eco de caracteres (FR-033).
/// Exige digitação e confirmação idênticas; nunca aceita senha por argumento, variável de ambiente ou arquivo.
/// </summary>
public sealed class LeitorSenhaConsole : IProvedorSenha
{
    /// <inheritdoc />
    public Task<string> ObterSenhaComConfirmacaoAsync(CancellationToken tokenCancelamento = default)
    {
        Console.Error.Write("Senha: ");
        string senha = LerSenhaSemEco();
        Console.Error.WriteLine();

        Console.Error.Write("Confirmação da senha: ");
        string confirmacao = LerSenhaSemEco();
        Console.Error.WriteLine();

        if (!string.Equals(senha, confirmacao, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A senha e a confirmação informadas não conferem.");
        }

        return Task.FromResult(senha);
    }

    private static string LerSenhaSemEco()
    {
        StringBuilder construtor = new();
        while (true)
        {
            ConsoleKeyInfo tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (construtor.Length > 0)
                {
                    construtor.Length--;
                }
            }
            else if (!char.IsControl(tecla.KeyChar))
            {
                construtor.Append(tecla.KeyChar);
            }
        }

        return construtor.ToString();
    }
}
