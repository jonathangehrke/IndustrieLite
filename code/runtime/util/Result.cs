// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Non-generic Result for operations without a return value.
/// Deutsche Fehlermeldungen; ErrorInfo traegt strukturierte Details.
/// </summary>
public class Result
{
    public bool Ok { get; }
    public string Error { get; }
    public ErrorInfo? ErrorInfo { get; }

    private Result(bool ok, string error, ErrorInfo? info)
    {
        Ok = ok;
        Error = error;
        ErrorInfo = info;
    }

    public static Result Success() => new Result(true, string.Empty, null);
    public static Result Fail(string error) => new Result(false, error ?? "Error", null);
    public static Result Fail(ErrorInfo info) => new Result(false, info?.Message ?? "Error", info);

    public static Result FromException(Exception ex, StringName? code = null, string? message = null, Dictionary<string, object?>? details = null)
    {
        var info = new ErrorInfo(code ?? ErrorIds.SystemUnexpectedExceptionName, message ?? ex.Message, details, ex);
        return Fail(info);
    }
}

/// <summary>
/// Generic Result with value and structured error information.
/// </summary>
public class Result<T>
{
    public bool Ok { get; }
    public T Value { get; }
    public string Error { get; }
    public ErrorInfo? ErrorInfo { get; }

    private Result(bool ok, T value, string error, ErrorInfo? info)
    {
        Ok = ok;
        Value = value;
        Error = error;
        ErrorInfo = info;
    }

    public static Result<T> Success(T value) => new Result<T>(true, value, string.Empty, null);
    public static Result<T> Fail(string error) => new Result<T>(false, default(T)!, error ?? "Error", null);
    public static Result<T> Fail(ErrorInfo info) => new Result<T>(false, default(T)!, info?.Message ?? "Error", info);

    public static Result<T> FromException(Exception ex, StringName? code = null, string? message = null, Dictionary<string, object?>? details = null)
    {
        var info = new ErrorInfo(code ?? ErrorIds.SystemUnexpectedExceptionName, message ?? ex.Message, details, ex);
        return Fail(info);
    }
}

/// <summary>
/// Helpful extensions to work with Result / Result&lt;T&gt; in a functional style.
/// </summary>
public static class ResultExtensions
{
    public static Result<U> Map<T, U>(this Result<T> res, Func<T, U> mapper)
    {
        if (res == null) return Result<U>.Fail("Null Result");
        if (!res.Ok) return Result<U>.Fail(res.ErrorInfo ?? new ErrorInfo(ErrorIds.SystemUnexpectedExceptionName, res.Error));
        return Result<U>.Success(mapper(res.Value));
    }

    public static Result<U> Bind<T, U>(this Result<T> res, Func<T, Result<U>> binder)
    {
        if (res == null) return Result<U>.Fail("Null Result");
        if (!res.Ok) return Result<U>.Fail(res.ErrorInfo ?? new ErrorInfo(ErrorIds.SystemUnexpectedExceptionName, res.Error));
        return binder(res.Value);
    }

    public static Result<T> Tap<T>(this Result<T> res, Action<T> action)
    {
        if (res != null && res.Ok) action(res.Value);
        return res!;
    }

    public static Result<T> OnError<T>(this Result<T> res, Action<ErrorInfo> handler)
    {
        if (res != null && !res.Ok)
        {
            handler(res.ErrorInfo ?? new ErrorInfo(ErrorIds.SystemUnexpectedExceptionName, res.Error));
        }
        return res!;
    }

    public static Result OnError(this Result res, Action<ErrorInfo> handler)
    {
        if (res != null && !res.Ok)
        {
            handler(res.ErrorInfo ?? new ErrorInfo(ErrorIds.SystemUnexpectedExceptionName, res.Error));
        }
        return res!;
    }
}
