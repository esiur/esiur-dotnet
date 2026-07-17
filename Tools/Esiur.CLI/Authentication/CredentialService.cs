using System.Text;
using Esiur.CLI.Configuration;

namespace Esiur.CLI.Authentication;

public interface ICredentialService
{
    ValueTask<byte[]?> GetPasswordAsync(
        ConnectionProfile profile,
        bool readStandardInput,
        TextReader input,
        TextWriter error,
        CancellationToken cancellationToken);

    ValueTask RemoveAsync(string profileName, CancellationToken cancellationToken);
}

/// <summary>
/// Password credentials are deliberately ephemeral. A platform secret-store implementation
/// can replace this service later without changing profile or connection code.
/// </summary>
public sealed class PromptCredentialService : ICredentialService
{
    public async ValueTask<byte[]?> GetPasswordAsync(
        ConnectionProfile profile,
        bool readStandardInput,
        TextReader input,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (!IsPasswordProvider(profile.Provider)) return null;

        string password;
        if (readStandardInput || Console.IsInputRedirected)
        {
            password = (await input.ReadToEndAsync(cancellationToken)).TrimEnd('\r', '\n');
        }
        else
        {
            await error.WriteAsync("Password: ");
            var value = new StringBuilder();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) break;
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (value.Length > 0) value.Length--;
                    continue;
                }
                if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
            }
            await error.WriteLineAsync();
            password = value.ToString();
            value.Clear();
        }

        if (password.Length == 0)
            throw new CliException("A password is required.", ExitCodes.AuthenticationFailed);
        return Encoding.UTF8.GetBytes(password);
    }

    public ValueTask RemoveAsync(string profileName, CancellationToken cancellationToken) => ValueTask.CompletedTask;

    public static bool IsPasswordProvider(string? provider) =>
        string.Equals(provider, "password", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "password-sha3-v1", StringComparison.OrdinalIgnoreCase);
}
