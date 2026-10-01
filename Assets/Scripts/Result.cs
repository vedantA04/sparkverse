public record Result<T>(T Value, bool IsSuccess, string Error)
{
    public T Value { get; } = Value;
    public bool IsSuccess { get; } = IsSuccess;
    public bool IsFailed => !IsSuccess;
    public string Error { get; } = Error;
}

public static class Result
{
    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(value, true, null);
    }

    public static Result<T> Fail<T>(string error)
    {
        return new Result<T>(default, false, error);
    }
}
