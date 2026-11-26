using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Authentication;
using LogMyDay.Api.Controllers;
using LogMyDay.Domain.Entities;
using LogMyDay.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;

namespace LogMyDay.Api.Tests;

/// <summary>Changing your own password from the web app.</summary>
public class AccountControllerTests
{
    [Fact]
    public async Task AChangedPassword_IsReportedAsChanged_WhenTheBrowserCantBeSignedInAgain()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@test.com", PasswordHash = "hash" };
        var users = new Mock<IUserService>();
        users.Setup(s => s.Get(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var auth = new Mock<IAuthService>();
        auth.Setup(a => a.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(user.Id);
        auth.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), user, It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("The data protection keys can't be read."));
        var controller = new AccountController(users.Object, auth.Object, Mock.Of<ILogger<AccountController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CookieSession.Create(user) }
            }
        };

        var result = await controller.ChangePassword(new ChangePasswordDto("Old-Pa55word!", "New-Pa55word!"), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        users.Verify(s => s.ChangePassword(user.Id, "Old-Pa55word!", "New-Pa55word!", user.Id, It.IsAny<CancellationToken>()));
        auth.Verify(a => a.SignInAsync(It.IsAny<HttpContext>(), user, It.IsAny<bool>()));
    }
}
