namespace RepairConnect.Services.Common;

public enum ServiceErrorType
{
    None,
    NotFound,
    BadRequest,
    Conflict,
    Forbidden
}

public class ServiceResult
{
    public bool Succeeded { get; protected set; }
    public string? Error { get; protected set; }
    public ServiceErrorType ErrorType { get; protected set; } = ServiceErrorType.None;

    public static ServiceResult Success() => new() { Succeeded = true };

    public static ServiceResult Failure(string error, ServiceErrorType type = ServiceErrorType.BadRequest) =>
        new() { Succeeded = false, Error = error, ErrorType = type };

    public static ServiceResult NotFound(string error = "Resource not found.") =>
        Failure(error, ServiceErrorType.NotFound);
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private set; }

    public static ServiceResult<T> Success(T data) => new() { Succeeded = true, Data = data };

    public new static ServiceResult<T> Failure(string error, ServiceErrorType type = ServiceErrorType.BadRequest) =>
        new() { Succeeded = false, Error = error, ErrorType = type };

    public new static ServiceResult<T> NotFound(string error = "Resource not found.") =>
        new() { Succeeded = false, Error = error, ErrorType = ServiceErrorType.NotFound };
}
