namespace HiveMind.Core.Common;

public enum ResultStatus { Ok, Invalid, NotFound, Conflict, PayloadTooLarge, Error }
public sealed record ValidationError(string Field, string Message);

public class AppResult
{
    public ResultStatus Status { get; }
    public bool IsSuccess => Status == ResultStatus.Ok;
    public IReadOnlyList<string> Messages { get; }
    public IReadOnlyList<ValidationError> ValidationErrors { get; }
    protected AppResult(ResultStatus status, IEnumerable<string>? messages = null, IEnumerable<ValidationError>? errors = null)
    { Status = status; Messages = messages?.ToArray() ?? []; ValidationErrors = errors?.ToArray() ?? []; }
    public static AppResult Ok() => new(ResultStatus.Ok);
    public static AppResult Invalid(IEnumerable<ValidationError> errors) => new(ResultStatus.Invalid, errors: errors);
    public static AppResult NotFound(string message) => new(ResultStatus.NotFound, [message]);
    public static AppResult Conflict(string message) => new(ResultStatus.Conflict, [message]);
    public static AppResult PayloadTooLarge(string message) => new(ResultStatus.PayloadTooLarge, [message]);
    public static AppResult Error(string message) => new(ResultStatus.Error, [message]);
}

public sealed class AppResult<T> : AppResult
{
    public T? Value { get; }
    private AppResult(ResultStatus status, T? value = default, IEnumerable<string>? messages = null, IEnumerable<ValidationError>? errors = null) : base(status, messages, errors) => Value = value;
    public static AppResult<T> Ok(T value) => new(ResultStatus.Ok, value);
    public static new AppResult<T> Invalid(IEnumerable<ValidationError> errors) => new(ResultStatus.Invalid, errors: errors);
    public static new AppResult<T> NotFound(string message) => new(ResultStatus.NotFound, messages: [message]);
    public static new AppResult<T> Conflict(string message) => new(ResultStatus.Conflict, messages: [message]);
    public static new AppResult<T> PayloadTooLarge(string message) => new(ResultStatus.PayloadTooLarge, messages: [message]);
    public static new AppResult<T> Error(string message) => new(ResultStatus.Error, messages: [message]);
}
