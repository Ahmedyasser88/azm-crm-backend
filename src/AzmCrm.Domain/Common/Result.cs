namespace AzmCrm.Domain.Common;

public sealed record Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public static Result Success() => new(true, []);
    public static Result Failure(IReadOnlyList<string> errors) => new(false, errors);
    public static Result Failure(string error) => new(false, [error]);
}

public sealed record Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Data { get; }
    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, T? data, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Data = data;
        Errors = errors;
    }

    public static Result<T> Success(T data) => new(true, data, []);
    public static Result<T> Failure(IReadOnlyList<string> errors) => new(false, default(T?), errors);
    public static Result<T> Failure(string error) => new(false, default(T?), [error]);
}
