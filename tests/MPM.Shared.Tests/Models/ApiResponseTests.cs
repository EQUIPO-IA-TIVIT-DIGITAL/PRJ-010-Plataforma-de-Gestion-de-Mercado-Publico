using MPM.Shared.Models;
using FluentAssertions;
using Xunit;

namespace MPM.Shared.Tests.Models;

public class ApiResponseTests
{
    [Fact]
    public void Ok_SetsSuccessTrue_AndData()
    {
        var response = ApiResponse<string>.Ok("hello");
        response.Success.Should().BeTrue();
        response.Data.Should().Be("hello");
        response.Message.Should().BeNull();
        response.Errors.Should().BeNull();
    }

    [Fact]
    public void Fail_SetsSuccessFalse_AndMessage()
    {
        var response = ApiResponse<string>.Fail("Error occurred");
        response.Success.Should().BeFalse();
        response.Message.Should().Be("Error occurred");
        response.Data.Should().Be(default);
    }

    [Fact]
    public void Fail_WithErrors_SetsErrorsList()
    {
        var errors = new List<ErrorDetail>
        {
            new() { Code = "VAL_001", Field = "email", Message = "Required" }
        };
        var response = ApiResponse<string>.Fail("Validation failed", errors);
        response.Success.Should().BeFalse();
        response.Errors.Should().HaveCount(1);
        response.Errors![0].Code.Should().Be("VAL_001");
        response.Errors[0].Field.Should().Be("email");
    }

    [Fact]
    public void PaginationInfo_HasNext_True_WhenMorePages()
    {
        var p = new PaginationInfo { Page = 1, PageSize = 10, TotalRecords = 25, TotalPages = 3 };
        p.HasNext.Should().BeTrue();
        p.HasPrevious.Should().BeFalse();
    }

    [Fact]
    public void PaginationInfo_HasPrevious_True_WhenNotFirstPage()
    {
        var p = new PaginationInfo { Page = 2, PageSize = 10, TotalRecords = 25, TotalPages = 3 };
        p.HasNext.Should().BeTrue();
        p.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public void PaginationInfo_NoNext_WhenLastPage()
    {
        var p = new PaginationInfo { Page = 3, PageSize = 10, TotalRecords = 25, TotalPages = 3 };
        p.HasNext.Should().BeFalse();
        p.HasPrevious.Should().BeTrue();
    }

    [Fact]
    public void PaginationInfo_SinglePage()
    {
        var p = new PaginationInfo { Page = 1, PageSize = 50, TotalRecords = 5, TotalPages = 1 };
        p.HasNext.Should().BeFalse();
        p.HasPrevious.Should().BeFalse();
    }

    [Fact]
    public void ErrorDetail_PropertiesSet()
    {
        var e = new ErrorDetail { Code = "SYS_001", Field = "field1", Message = "Something went wrong" };
        e.Code.Should().Be("SYS_001");
        e.Field.Should().Be("field1");
        e.Message.Should().Be("Something went wrong");
    }

    [Fact]
    public void ApiResponse_WithComplexType()
    {
        var data = new List<int> { 1, 2, 3 };
        var response = ApiResponse<List<int>>.Ok(data);
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(3);
    }

    [Fact]
    public void ApiResponse_Fail_WithNullErrors()
    {
        var response = ApiResponse<object>.Fail("Error");
        response.Errors.Should().BeNull();
    }
}