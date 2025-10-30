// SPDX-License-Identifier: MIT
namespace IndustrieLite.Core.Util;

public sealed class CoreError
{
    public string Code { get; }
    public string Message { get; }

    public CoreError(string code, string message)
    {
        Code = code;
        Message = message;
    }
}

public sealed class CoreResult
{
    public bool Ok { get; }
    public CoreError? Error { get; }

    private CoreResult(bool ok, CoreError? error)
    {
        Ok = ok;
        Error = error;
    }

    public static CoreResult Success() => new(true, null);
    public static CoreResult Fail(string code, string message) => new(false, new CoreError(code, message));
    public static CoreResult Fail(CoreError err) => new(false, err);
}

public sealed class CoreResult<T>
{
    public bool Ok { get; }
    public T? Value { get; }
    public CoreError? Error { get; }

    private CoreResult(bool ok, T? value, CoreError? error)
    {
        Ok = ok;
        Value = value;
        Error = error;
    }

    public static CoreResult<T> Success(T value) => new(true, value, null);
    public static CoreResult<T> Fail(string code, string message) => new(false, default, new CoreError(code, message));
    public static CoreResult<T> Fail(CoreError err) => new(false, default, err);
}

