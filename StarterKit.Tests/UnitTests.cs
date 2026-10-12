using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Moq;
using StarterKit.Api.Auth;
using StarterKit.Api.Endpoints;
using StarterKit.Api.Middleware;
using StarterKit.Api.Validation;

namespace StarterKit.Tests
{
    public class UnitTests
    {
        [Fact]
        public void TokenService_creates_a_valid_token_with_the_user_claims()
        {
            var options = Options.Create(new JwtOptions
            {
                Issuer = "issuer",
                Audience = "audience",
                Key = new string('k', 32),
                ExpiresMinutes = 30
            });
            var service = new TokenService(options);

            var (token, expiresAt) = service.CreateToken(new IdentityUser { Id = "42", Email = "a@b.com" });

            var parsed = new JsonWebTokenHandler().ReadJsonWebToken(token);
            Assert.Equal("42", parsed.Subject);
            Assert.Equal("issuer", parsed.Issuer);
            Assert.True(expiresAt > DateTime.UtcNow);
        }

        [Theory]
        [InlineData("", 10, false)]
        [InlineData("Name", -1, false)]
        [InlineData("Name", 0, true)]
        [InlineData("Name", 10, true)]
        public void ProductRequestValidator_checks_name_and_price(string name, decimal price, bool expectedValid)
        {
            var result = new ProductRequestValidator().Validate(new ProductRequest(name, null, price));
            Assert.Equal(expectedValid, result.IsValid);
        }

        [Fact]
        public async Task GlobalExceptionHandler_returns_500_and_handles_the_exception()
        {
            var handler = new GlobalExceptionHandler(Mock.Of<ILogger<GlobalExceptionHandler>>());
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            var handled = await handler.TryHandleAsync(context, new Exception("boom"), CancellationToken.None);

            Assert.True(handled);
            Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        }
    }
}
