using StarterKit.Api.Endpoints;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace StarterKit.Tests
{
    public class AuthAndProductsTests(ApiFactory factory) : IClassFixture<ApiFactory>
    {
        private readonly HttpClient _client = factory.CreateClient();
        private const string Password = "Test1234!";

        private async Task<string> RegisterAndLoginAsync()
        {
            var email = $"{Guid.NewGuid():N}@test.com";

            var register = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
            Assert.Equal(HttpStatusCode.NoContent, register.StatusCode);

            var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
            login.EnsureSuccessStatusCode();

            var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
            return auth!.Token;
        }

        [Fact]
        public async Task Products_without_token_returns_401()
        {
            var response = await _client.GetAsync("/api/products");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Login_with_wrong_password_returns_401()
        {
            var email = $"{Guid.NewGuid():N}@test.com";
            await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });

            var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "Other1234!" });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Register_with_weak_password_returns_400()
        {
            var response = await _client.PostAsJsonAsync("/api/auth/register",
                new { email = $"{Guid.NewGuid():N}@test.com", password = "abc" });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Full_flow_register_login_create_and_list_product()
        {
            var token = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var create = await _client.PostAsJsonAsync("/api/products",
                new ProductRequest("Test product", "Description", 9.99m));
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);

            var list = await _client.GetFromJsonAsync<PagedResponse<ProductResponse>>("/api/products");
            Assert.Contains(list!.Items, p => p.Name == "Test product");
        }

        [Fact]
        public async Task Create_product_with_invalid_body_returns_400()
        {
            var token = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.PostAsJsonAsync("/api/products", new ProductRequest("", null, -5));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Get_unknown_product_returns_404()
        {
            var token = await RegisterAndLoginAsync();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/api/products/999999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Owner_can_update_and_delete_their_product()
        {
            var owner = CreateClientFor(await RegisterAndLoginAsync());
            var id = await CreateProductAsync(owner);

            var update = await owner.PutAsJsonAsync($"/api/products/{id}", new ProductRequest("Updated", null, 1m));
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);

            var delete = await owner.DeleteAsync($"/api/products/{id}");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

            var get = await owner.GetAsync($"/api/products/{id}");
            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        }

        [Fact]
        public async Task Another_user_cannot_update_a_product_they_do_not_own()
        {
            var owner = CreateClientFor(await RegisterAndLoginAsync());
            var intruder = CreateClientFor(await RegisterAndLoginAsync());
            var id = await CreateProductAsync(owner);

            var response = await intruder.PutAsJsonAsync($"/api/products/{id}", new ProductRequest("Hacked", null, 1m));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            var current = await owner.GetFromJsonAsync<ProductResponse>($"/api/products/{id}");
            Assert.Equal("Original", current!.Name);
        }

        [Fact]
        public async Task Another_user_cannot_delete_a_product_they_do_not_own()
        {
            var owner = CreateClientFor(await RegisterAndLoginAsync());
            var intruder = CreateClientFor(await RegisterAndLoginAsync());
            var id = await CreateProductAsync(owner);

            var response = await intruder.DeleteAsync($"/api/products/{id}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

            var stillThere = await owner.GetAsync($"/api/products/{id}");
            Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
        }

        [Fact]
        public async Task Me_returns_the_authenticated_user()
        {
            var email = $"{Guid.NewGuid():N}@test.com";
            await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
            var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
            var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
            var client = CreateClientFor(auth!.Token);

            var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");

            Assert.Equal(email, me!.Email);
            Assert.False(string.IsNullOrEmpty(me.Id));
        }

        [Fact]
        public async Task Me_without_token_returns_401()
        {
            var response = await _client.GetAsync("/api/auth/me");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        private HttpClient CreateClientFor(string token)
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        private static async Task<int> CreateProductAsync(HttpClient client)
        {
            var response = await client.PostAsJsonAsync("/api/products", new ProductRequest("Original", null, 5m));
            response.EnsureSuccessStatusCode();
            var product = await response.Content.ReadFromJsonAsync<ProductResponse>();
            return product!.Id;
        }
    }
}
