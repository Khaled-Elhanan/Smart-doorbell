using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Doorbell.Api.ExceptionHandling;
using Doorbell.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Doorbell.Tests.ExceptionHandling;

public class GlobalExceptionHandlerTests
{
    private readonly Mock<ILogger<GlobalExceptionHandler>> _loggerMock;
    private readonly Mock<IHostEnvironment> _envMock;
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _loggerMock = new Mock<ILogger<GlobalExceptionHandler>>();
        _envMock = new Mock<IHostEnvironment>();
        _handler = new GlobalExceptionHandler(_loggerMock.Object, _envMock.Object);
    }

    [Theory]
    [InlineData(typeof(NotFoundException), StatusCodes.Status404NotFound)]
    [InlineData(typeof(ForbiddenException), StatusCodes.Status403Forbidden)]
    [InlineData(typeof(UnauthorizedException), StatusCodes.Status401Unauthorized)]
    [InlineData(typeof(ValidationException), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(DomainException), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(Exception), StatusCodes.Status500InternalServerError)]
    public async Task TryHandleAsync_ReturnsExpectedStatusCode(Type exceptionType, int expectedStatusCode)
    {
        // Arrange
        var exception = (Exception)Activator.CreateInstance(exceptionType, "Test error message")!;
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        _envMock.Setup(e => e.EnvironmentName).Returns("Production");

        // Act
        var result = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_Production_DoesNotIncludeStackTrace()
    {
        // Arrange
        var exception = new Exception("Unexpected");
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        _envMock.Setup(e => e.EnvironmentName).Returns("Production");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(problemDetails);
        Assert.DoesNotContain("stackTrace", problemDetails.Extensions.Keys);
    }

    [Fact]
    public async Task TryHandleAsync_Development_IncludesStackTrace()
    {
        // Arrange
        var exception = new Exception("Unexpected");
        // Force stack trace generation
        try
        {
            throw exception;
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        _envMock.Setup(e => e.EnvironmentName).Returns("Development");

        // Act
        await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        Assert.NotNull(problemDetails);
        Assert.Contains("stackTrace", problemDetails.Extensions.Keys);
    }
}
