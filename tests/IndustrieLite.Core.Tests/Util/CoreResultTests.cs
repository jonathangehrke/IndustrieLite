// SPDX-License-Identifier: MIT
using IndustrieLite.Core.Util;
using Xunit;

public class CoreResultTests
{
    [Fact]
    public void Success_NonGeneric_HasOk_True_And_NoError()
    {
        var res = CoreResult.Success();
        Assert.True(res.Ok);
        Assert.Null(res.Error);
    }

    [Fact]
    public void Fail_NonGeneric_HasOk_False_And_Error()
    {
        var res = CoreResult.Fail("test.error", "Something went wrong");
        Assert.False(res.Ok);
        Assert.NotNull(res.Error);
        Assert.Equal("test.error", res.Error!.Code);
        Assert.Equal("Something went wrong", res.Error!.Message);
    }

    [Fact]
    public void Success_Generic_HasValue_And_NoError()
    {
        var res = CoreResult<string>.Success("ok");
        Assert.True(res.Ok);
        Assert.Equal("ok", res.Value);
        Assert.Null(res.Error);
    }

    [Fact]
    public void Fail_Generic_HasDefaultValue_And_Error()
    {
        var err = new CoreError("x.y", "msg");
        var res = CoreResult<int>.Fail(err);
        Assert.False(res.Ok);
        Assert.Equal(default(int), res.Value);
        Assert.Equal("x.y", res.Error!.Code);
        Assert.Equal("msg", res.Error!.Message);
    }
}
