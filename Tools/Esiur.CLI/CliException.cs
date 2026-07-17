namespace Esiur.CLI;

public sealed class CliException : Exception
{
    public CliException(string message, int exitCode = ExitCodes.GeneralFailure, Exception? inner = null)
        : base(message, inner) => ExitCode = exitCode;

    public int ExitCode { get; }
}
