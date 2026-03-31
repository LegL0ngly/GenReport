using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using GenReport.Domain.DBContext;
using GenReport.Domain.Entities.Onboarding;
using GenReport.Infrastructure.Models.HttpRequests.Core.Ai;
using GenReport.Infrastructure.Models.HttpRequests.Onboarding;
using GenReport.Infrastructure.Security.Encryption;

namespace GenReport.Tests
{
    [TestFixture]
    public class AiConnectionApiTests
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;
        private string _dbName;

        [SetUp]
        public async Task Setup()
        {
            _dbName = "GenReportTestDb_" + Guid.NewGuid().ToString();

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.Replace(ServiceDescriptor.Scoped<DbContextOptions<ApplicationDbContext>>(_ =>
                            new DbContextOptionsBuilder<ApplicationDbContext>()
                                .UseInMemoryDatabase(_dbName)
                                .Options));
                    });
                });

            _client = _factory.CreateClient();

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            var user = new User(
                password: "TestPassword123",
                email: "test@example.com",
                firstName: "Test",
                lastName: "User",
                middleName: "M",
                profileURL: "http://example.com/profile.png"
            );
            user.RoleId = 1;

            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();

            var loginResponse = await _client.PostAsJsonAsync("/login", new LoginRequest
            {
                Email = "test@example.com",
                Password = "TestPassword123"
            });

            var loginJson = await loginResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(loginJson);

            var root = doc.RootElement;
            var success = root.TryGetProperty("successResponse", out var sr) ? sr :
                          root.TryGetProperty("SuccessResponse", out sr) ? sr :
                          default;
            Assert.That(success.ValueKind, Is.EqualTo(JsonValueKind.Object));

            var data = success.TryGetProperty("data", out var d) ? d :
                       success.TryGetProperty("Data", out d) ? d :
                       default;
            Assert.That(data.ValueKind, Is.EqualTo(JsonValueKind.Object));

            var tokenEl = data.TryGetProperty("token", out var t) ? t :
                          data.TryGetProperty("Token", out t) ? t :
                          default;
            var token = tokenEl.GetString();
            Assert.That(token, Is.Not.Null.And.Not.Empty);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task AddAiConnection_EncryptsApiKey()
        {
            var request = new AddAiConnectionRequest
            {
                Provider = "OpenAI",
                ApiKey = "sk-test",
                DefaultModel = "gpt-4o",
                IsActive = true,
                IsDefault = false
            };

            var response = await _client.PostAsJsonAsync("/ai/connections", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var encryptorFactory = scope.ServiceProvider.GetRequiredService<ICredentialEncryptorFactory>();

            var connection = await context.AiConnections.SingleAsync();

            Assert.That(connection.ApiKey, Is.Not.EqualTo(request.ApiKey));

            var decryptedKey = encryptorFactory.GetEncryptor(CredentialType.ApiKey).Decrypt(connection.ApiKey);
            Assert.That(decryptedKey, Is.EqualTo(request.ApiKey));
        }
    }
}
