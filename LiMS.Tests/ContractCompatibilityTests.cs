using System.Text.Json;
using LiMS.Api.Contracts;
using WebUser = LiMS.Web.Models.ApiUser;
using AdminUser = LiMS.Admin.Models.LoginResponse;

namespace LiMS.Tests;

public sealed class ContractCompatibilityTests
{
    [Fact]
    public void Login_response_deserializes_for_web_and_admin_clients()
    {
        var apiResponse = new LoginResponse(
            17,
            "CARD-17",
            "Test User",
            "user@example.test",
            "Admin",
            "test-token",
            DateTimeOffset.UtcNow.AddMinutes(30));
        var json = JsonSerializer.Serialize(apiResponse, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var webResponse = JsonSerializer.Deserialize<WebUser>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var adminResponse = JsonSerializer.Deserialize<AdminUser>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(webResponse);
        Assert.NotNull(adminResponse);
        Assert.Equal(apiResponse.Id, webResponse.Id);
        Assert.Equal(apiResponse.AccessToken, webResponse.AccessToken);
        Assert.Equal(apiResponse.Role, adminResponse.Role);
        Assert.Equal(apiResponse.AccessToken, adminResponse.AccessToken);
    }
}
