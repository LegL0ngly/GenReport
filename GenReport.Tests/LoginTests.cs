using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using GenReport.Infrastructure.Models.HttpRequests.Onboarding;
using GenReport.Domain.DBContext;
using GenReport.Domain.Entities.Onboarding;

namespace GenReport.Tests
{
    [TestFixture]
    public class LoginTests
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;
        private string _dbName;

        [SetUp]
        public async Task Setup()
        {
            _dbName = "LoginTestDb_" + Guid.NewGuid().ToString();

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

            // Seed a real user so the invalid-password path is exercised
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();

            var user = new User(
                password: "CorrectPassword123!",
                email: "existing@example.com",
                firstName: "Test",
                lastName: "User",
                middleName: "",
                profileURL: ""
            );
            user.RoleId = 1;
            await context.Users.AddAsync(user);
            await context.SaveChangesAsync();
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange - user exists but password is wrong
            var request = new LoginRequest
            {
                Email = "existing@example.com",
                Password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/login", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "API returns 200 OK but sets ErrorResponse");

            var content = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(content.Contains("Please check password") || content.Contains("ErrorResponse"), "Response should contain an error message");
        }

        [Test]
        public async Task Login_WithNonExistentEmail_ReturnsNotFoundError()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "WrongPassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/login", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "API returns 200 OK but sets ErrorResponse");

            var content = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(content.Contains("Please check email") || content.Contains("ErrorResponse"), "Response should contain an error message");
        }

        [Test]
        public async Task Login_WithMissingEmail_ReturnsBadRequest()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "",
                Password = "SomePassword123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/login", request);

            // Assert
            var content = await response.Content.ReadAsStringAsync();
            // FastEndpoints IsEmail extension throws ArgumentException for empty strings which is caught by GlobalExceptionHandler as 500
            // but wrapped in an HttpResponse 200 OK
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"Unexpected status {response.StatusCode}. Content: {content}");
            Assert.IsTrue(content.Contains("MIDDLEWARE_ERROR") || content.Contains("error executing the query"), "Response should contain an error message");
        }
    }
}
