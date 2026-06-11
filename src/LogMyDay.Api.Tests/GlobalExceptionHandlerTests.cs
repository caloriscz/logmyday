using LogMyDay.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace LogMyDay.Api.Tests;

public class GlobalExceptionHandlerTests
{
    private static (GlobalExceptionHandler handler, Mock<IProblemDetailsService> problemDetails) Create()
    {
        var problemDetails = new Mock<IProblemDetailsService>();
        problemDetails
            .Setup(p => p.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Returns(new ValueTask<bool>(true));

        var handler = new GlobalExceptionHandler(problemDetails.Object, NullLogger<GlobalExceptionHandler>.Instance);

        return (handler, problemDetails);
    }

    [Fact]
    public async Task ApiRequest_IsHandled_WithProblemDetailsAnd500()
    {
        var (handler, problemDetails) = Create();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/backup/info";

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        problemDetails.Verify(p => p.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Once);
    }

    [Fact]
    public async Task NonApiRequest_IsNotHandled_AndLeftToErrorPage()
    {
        var (handler, problemDetails) = Create();
        var context = new DefaultHttpContext();
        context.Request.Path = "/dashboard";

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("boom"), CancellationToken.None);

        Assert.False(handled);
        problemDetails.Verify(p => p.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Never);
    }
}
