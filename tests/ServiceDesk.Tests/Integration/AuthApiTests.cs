using System.Net;
using System.Net.Http.Json;
using ServiceDesk.Api.Contracts;

namespace ServiceDesk.Tests.Integration;

public sealed class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_creates_a_customer_and_returns_a_token()
    {
        var client = factory.CreateClient();
        var email = $"new-{Guid.NewGuid():N}@test.dev";

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { fullName = "Meera Shah", email, password = "Password@123" }, TestApi.Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var auth = await response.ReadAsync<AuthResponse>();
        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.Equal("Customer", auth.User.Role);
        Assert.Equal(email, auth.User.Email);
    }

    [Fact]
    public async Task Register_with_an_existing_email_returns_409()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { fullName = "Someone", email = TestApi.AgentEmail, password = "Password@123" },
            TestApi.Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_returns_401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new { email = TestApi.AgentEmail, password = "wrong-password" }, TestApi.Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_a_token_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_logged_in_user()
    {
        var client = await factory.LoginAsAgentAsync();

        var me = await (await client.GetAsync("/api/users/me")).ReadAsync<UserDto>();

        Assert.Equal(TestApi.AgentEmail, me.Email);
        Assert.Equal("Agent", me.Role);
    }
}
