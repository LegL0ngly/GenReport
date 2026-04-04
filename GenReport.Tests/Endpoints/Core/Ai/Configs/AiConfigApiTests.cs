using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Ai.Configs;
using GenReport.Infrastructure.Models.HttpResponse.Core.Ai.Configs;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Domain.Entities.Onboarding;
using GenReport.DB.Domain.Entities.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GenReport.Infrastructure.Security.Encryption;
using GenReport.Infrastructure.Models.HttpRequests.Onboarding;

namespace GenReport.Tests.Endpoints.Core.Ai.Configs
{
    [TestFixture]
    public class AiConfigApiTests
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;
        private long _testUserId;
        private string _dbName;
        private long _aiConnectionId;

        [SetUp]
        public async Task Setup()
        {
            _dbName = "AiConfigTestDb_" + Guid.NewGuid().ToString();

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
             await context.Database.EnsureDeletedAsync(); // Ensure clean start
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
            
            var aiConnection = new AiConnection
            {
                Provider = "openai",
                ApiKey = "encrypted-key",
                DefaultModel = "gpt-4",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await context.AiConnections.AddAsync(aiConnection);

            await context.SaveChangesAsync();
            _testUserId = user.Id;
            _aiConnectionId = aiConnection.Id;

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
            
            var data = success.TryGetProperty("data", out var d) ? d :
                       success.TryGetProperty("Data", out d) ? d :
                       default;
            
            var tokenEl = data.TryGetProperty("token", out var t) ? t :
                          data.TryGetProperty("Token", out t) ? t :
                          default;
            var token = tokenEl.GetString();
            
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task AddAiConfig_ReturnsSuccess()
        {
            var request = new AddAiConfigRequest
            {
                Type = AiConfigType.ChatSystemPrompt,
                Value = "Initial Prompt",
                ModelId = "gpt-4"
            };

            var response = await _client.PostAsJsonAsync($"/ai/connections/{_aiConnectionId}/configs", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("successResponse").GetProperty("data");
            
            Assert.That(data.GetProperty("value").GetString(), Is.EqualTo("Initial Prompt"));
            Assert.That(data.GetProperty("version").GetInt32(), Is.EqualTo(1));
        }

        [Test]
        public async Task AddAiConfig_BumpsVersionAndDeactivatesOld()
        {
            // First add
            var req1 = new AddAiConfigRequest { Type = AiConfigType.ChatSystemPrompt, Value = "V1", ModelId = null };
            await _client.PostAsJsonAsync($"/ai/connections/{_aiConnectionId}/configs", req1);

            // Second add
            var req2 = new AddAiConfigRequest { Type = AiConfigType.ChatSystemPrompt, Value = "V2", ModelId = null };
            var response2 = await _client.PostAsJsonAsync($"/ai/connections/{_aiConnectionId}/configs", req2);

            Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var json2 = await response2.Content.ReadAsStringAsync();
            using var doc2 = JsonDocument.Parse(json2);
            var data2 = doc2.RootElement.GetProperty("successResponse").GetProperty("data");
            
            Assert.That(data2.GetProperty("version").GetInt32(), Is.EqualTo(2));
            Assert.That(data2.GetProperty("value").GetString(), Is.EqualTo("V2"));

            // Get configs to see if only V2 is returned as active
            var getResponse = await _client.GetAsync($"/ai/connections/{_aiConnectionId}/configs");
            var getJson = await getResponse.Content.ReadAsStringAsync();
            using var getDoc = JsonDocument.Parse(getJson);
            var getData = getDoc.RootElement.GetProperty("successResponse").GetProperty("data");
            
            Assert.That(getData.GetArrayLength(), Is.EqualTo(1)); // Should only return IsActive=true
            Assert.That(getData[0].GetProperty("value").GetString(), Is.EqualTo("V2"));
        }

        [Test]
        public async Task EditAiConfig_UpdatesValueInPlace()
        {
            // First add
            var addReq = new AddAiConfigRequest { Type = AiConfigType.IntentClassifier, Value = "Original", ModelId = "m1" };
            var addResp = await _client.PostAsJsonAsync($"/ai/connections/{_aiConnectionId}/configs", addReq);
            var addJson = await addResp.Content.ReadAsStringAsync();
            using var addDoc = JsonDocument.Parse(addJson);
            var configId = addDoc.RootElement.GetProperty("successResponse").GetProperty("data").GetProperty("id").GetInt64();

            // Edit
            var editReq = new EditAiConfigRequest { Value = "Updated" };
            var editResp = await _client.PutAsJsonAsync($"/ai/connections/{_aiConnectionId}/configs/{configId}", editReq);

            Assert.That(editResp.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            // Verify
            var getResponse = await _client.GetAsync($"/ai/connections/{_aiConnectionId}/configs");
            var getJson = await getResponse.Content.ReadAsStringAsync();
            using var getDoc = JsonDocument.Parse(getJson);
            var getData = getDoc.RootElement.GetProperty("successResponse").GetProperty("data");
            
            // Convert array to enumerable to find the updated element
            var elements = getData.EnumerateArray().ToList();
            var returnedConfig = elements.FirstOrDefault(e => e.GetProperty("id").GetInt64() == configId);
            
            Assert.That(returnedConfig.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
            Assert.That(returnedConfig.GetProperty("value").GetString(), Is.EqualTo("Updated"));
            Assert.That(returnedConfig.GetProperty("version").GetInt32(), Is.EqualTo(1)); // Version should not bump on Edit
        }
    }
}
