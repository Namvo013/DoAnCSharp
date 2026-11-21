using System.ComponentModel.DataAnnotations;
using LiMS.Api.Contracts;
using LiMS.Api.Controllers;
using LiMS.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LiMS.Tests;

public sealed class ValidationAndControllerTests
{
    [Fact]
    public void LoginRequest_requires_both_credentials()
    {
        var request = new LoginRequest { Username = "", Password = "" };
        var errors = Validate(request);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(LoginRequest.Username)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(LoginRequest.Password)));
    }

    [Theory]
    [InlineData("", "valid-password", false)]
    [InlineData("valid-user", "", false)]
    [InlineData("valid-user", "valid-password", true)]
    public void LoginRequest_validates_required_fields(string username, string password, bool expectedValid)
    {
        var errors = Validate(new LoginRequest { Username = username, Password = password });
        Assert.Equal(expectedValid, errors.Count == 0);
    }

    [Fact]
    public async Task AuthController_returns_unauthorized_when_service_rejects_login()
    {
        var service = new Mock<IAuthService>();
        service.Setup(item => item.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginResponse?)null);
        var controller = new AuthController(service.Object);

        var result = await controller.Login(
            new LoginRequest { Username = "unknown", Password = "bad-password" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task AuthController_returns_login_response_when_service_accepts_credentials()
    {
        var expected = new LoginResponse(7, "CARD-7", "Test Reader", "reader@example.test", "Borrower", "token", DateTimeOffset.UtcNow.AddHours(1));
        var service = new Mock<IAuthService>();
        service.Setup(item => item.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = new AuthController(service.Object);

        var result = await controller.Login(
            new LoginRequest { Username = "reader", Password = "valid-password" },
            CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, response.Value);
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
