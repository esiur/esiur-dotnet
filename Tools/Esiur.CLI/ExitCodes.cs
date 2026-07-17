namespace Esiur.CLI;

public static class ExitCodes
{
    public const int Success = 0;
    public const int GeneralFailure = 1;
    public const int InvalidArguments = 2;
    public const int AuthenticationFailed = 3;
    public const int ConnectionFailed = 4;
    public const int ResourceNotFound = 5;
    public const int MemberNotFound = 6;
    public const int AccessDenied = 7;
    public const int InvalidValue = 8;
    public const int InvocationFailed = 9;
    public const int Timeout = 10;
    public const int Cancelled = 11;
}
