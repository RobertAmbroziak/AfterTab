namespace AfterTab.Common.Errors;

public enum ErrorKind { Validation, NotFound, Forbidden, Conflict }
public sealed class AppException(ErrorKind kind, string message) : Exception(message)
{
    public ErrorKind Kind { get; } = kind;
}
