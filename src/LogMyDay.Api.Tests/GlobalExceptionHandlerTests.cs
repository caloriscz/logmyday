using System.Security.Claims;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Infrastructure;
using LogMyDay.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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

    // --- The error is also written to the signed-in user's event log ---

    private static DefaultHttpContext ContextWithUser(Guid userId, Mock<IEventLogService> events)
    {
        var services = new ServiceCollection();
        services.AddSingleton(events.Object);
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Method = "POST";
        context.Request.Path = "/api/account/api-keys";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"));

        return context;
    }

    [Fact]
    public async Task ApiError_IsRecordedInTheEventLog_ForTheSignedInUser()
    {
        var (handler, _) = Create();
        var userId = Guid.NewGuid();
        var events = new Mock<IEventLogService>();
        var context = ContextWithUser(userId, events);

        await handler.TryHandleAsync(context, new InvalidOperationException("no such table: LogMyDay_ApiKeys"), CancellationToken.None);

        events.Verify(e => e.Log(
            userId,
            EventLogLevel.Error,
            It.Is<string>(m => m.StartsWith("API error: POST /api/account/api-keys") && m.Contains("InvalidOperationException: no such table")),
            It.Is<string?>(d => d != null && d.Contains("InvalidOperationException"))), Times.Once);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApiError_WithoutASignedInUser_IsNotRecorded_ButStillHandled()
    {
        var (handler, _) = Create();
        var events = new Mock<IEventLogService>();
        var services = new ServiceCollection();
        services.AddSingleton(events.Object);
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Path = "/api/auth/login";

        var handled = await handler.TryHandleAsync(context, new Exception("boom"), CancellationToken.None);

        Assert.True(handled);
        events.Verify(e => e.Log(It.IsAny<Guid>(), It.IsAny<EventLogLevel>(), It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task AFailingEventLog_NeverMasksTheOriginalError()
    {
        var (handler, problemDetails) = Create();
        var events = new Mock<IEventLogService>();
        events.Setup(e => e.Log(It.IsAny<Guid>(), It.IsAny<EventLogLevel>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("event log is down too"));
        var context = ContextWithUser(Guid.NewGuid(), events);

        var handled = await handler.TryHandleAsync(context, new Exception("boom"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        problemDetails.Verify(p => p.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Once);
    }
}
