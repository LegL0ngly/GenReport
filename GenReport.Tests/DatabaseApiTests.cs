using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Databases;
using GenReport.Infrastructure.Models.HttpResponse.Core.Databases;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using Moq;
using GenReport.Domain.Entities.Onboarding;
using GenReport.DB.Domain.Entities.Core;
using GenReport.DB.Domain.Enums;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GenReport.Tests
{
    [TestFixture]
    public class DatabaseApiTests
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;
        private Mock<ICurrentUserService> _userServiceMock;
        private long _testUserId;
        private string _dbName;

        [SetUp]
        public async Task Setup()
        {
            _userServiceMock = new Mock<ICurrentUserService>();
            _userServiceMock.Setup(s => s.IsAuthenticated()).Returns(true);
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

                        services.Replace(ServiceDescriptor.Singleton<ICurrentUserService>(_ => _userServiceMock.Object));
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
            await context.SaveChangesAsync();
            _testUserId = user.Id;

            _userServiceMock.Setup(s => s.LoggedInUserId()).Returns(_testUserId);
            
            Assert.That(_testUserId, Is.GreaterThan(0));
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
            _factory.Dispose();
        }

        [Test]
        public async Task AddDatabase_ReturnsSuccess()
        {
            // Arrange
            var request = new AddDatabaseRequest
            {
                Name = "Test DB",
                Type = "PostgreSQL",
                Provider = DbProvider.NpgSql,
                ConnectionString = "Host=localhost;Database=test",
                Description = "Integration Test DB",
                Password = "pwd",
                Port = 5432,
                ServerAddress = "127.0.0.1",
                Username = "user"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/databases", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<HttpResponse<string>>();
            Assert.IsNotNull(result?.SuccessResponse);
            Assert.That(result.SuccessResponse.Message, Does.Contain("successfully added"));
        }

        [Test]
        public async Task ListDatabases_ReturnsDatabases()
        {
            // Arrange - add a database first
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await context.Databases.AddAsync(new Database
                {
                    Name = "Sample DB",
                    Type = "PostgreSQL",
                    Provider = DbProvider.NpgSql,
                    ConnectionString = "conn",
                    Description = "desc",
                    Password = "pwd",
                    Port = 5432,
                    ServerAddress = "127.0.0.1",
                    Username = "user",
                    Status = "Active",
                    SizeInBytes = 100,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            // Act
            var response = await _client.GetAsync("/databases");

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<HttpResponse<List<DatabaseResponse>>>();
            Assert.IsNotNull(result?.SuccessResponse);
            Assert.That(result.SuccessResponse.Data.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(result.SuccessResponse.Data[0].Name, Is.EqualTo("Sample DB"));
        }

        [Test]
        public async Task EditDatabase_UpdatesDetails()
        {
            // Arrange - add a database
            long dbId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var db = new Database
                {
                    Name = "Old Name",
                    Type = "PostgreSQL",
                    Provider = DbProvider.NpgSql,
                    ConnectionString = "old_conn",
                    Description = "old_desc",
                    Password = "pwd",
                    Port = 5432,
                    ServerAddress = "127.0.0.1",
                    Username = "user",
                    Status = "Active",
                    SizeInBytes = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await context.Databases.AddAsync(db);
                await context.SaveChangesAsync();
                dbId = db.Id;
            }

            var request = new EditDatabaseRequest
            {
                Id = dbId,
                Name = "New Name",
                Description = "New Description",
                Provider = DbProvider.SqlClient
            };

            // Act
            var response = await _client.PutAsJsonAsync("/databases", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            
            // Verify in DB
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var updatedDb = await context.Databases.FindAsync(dbId);
                Assert.That(updatedDb.Name, Is.EqualTo("New Name"));
                Assert.That(updatedDb.Description, Is.EqualTo("New Description"));
                Assert.That(updatedDb.Provider, Is.EqualTo(DbProvider.SqlClient));
            }
        }
    }
}
