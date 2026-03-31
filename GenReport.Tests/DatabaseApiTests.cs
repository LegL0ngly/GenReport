using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Databases;
using GenReport.Infrastructure.Models.HttpResponse.Core.Databases;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Domain.Entities.Onboarding;
using GenReport.DB.Domain.Entities.Core;
using GenReport.DB.Domain.Enums;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GenReport.Infrastructure.Security.Encryption;
using GenReport.Infrastructure.Models.HttpRequests.Onboarding;

namespace GenReport.Tests
{
    [TestFixture]
    public class DatabaseApiTests
    {
        private WebApplicationFactory<Program> _factory;
        private HttpClient _client;
        private long _testUserId;
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
            
            Assert.That(_testUserId, Is.GreaterThan(0));

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
        public async Task AddDatabase_ReturnsSuccess()
        {
            // Arrange
            var request = new AddDatabaseRequest
            {
                Name = "Test DB",
                DatabaseType = "PostgreSQL",
                Provider = DbProvider.NpgSql,
                ConnectionString = "Host=localhost;Database=test",
                Description = "Integration Test DB",
                Password = "pwd",
                Port = 5432,
                HostName = "127.0.0.1",
                UserName = "user",
                DatabaseName = "test"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/connections", request);

            // Assert
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var result = await response.Content.ReadFromJsonAsync<HttpResponse<string>>();
            Assert.IsNotNull(result?.SuccessResponse);
            Assert.That(result.SuccessResponse.Message, Does.Contain("successfully added"));
        }

        [Test]
        public async Task AddDatabase_EncryptsConnectionStringAndPassword()
        {
            var request = new AddDatabaseRequest
            {
                Name = "Test DB",
                DatabaseType = "PostgreSQL",
                Provider = DbProvider.NpgSql,
                ConnectionString = "Host=localhost;Database=test",
                Description = "Integration Test DB",
                Password = "pwd",
                Port = 5432,
                HostName = "127.0.0.1",
                UserName = "user",
                DatabaseName = "test"
            };

            var response = await _client.PostAsJsonAsync("/connections", request);

            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var encryptorFactory = scope.ServiceProvider.GetRequiredService<ICredentialEncryptorFactory>();

            var db = await context.Databases.SingleAsync();

            Assert.That(db.ConnectionString, Is.Not.EqualTo(request.ConnectionString));
            Assert.That(db.Password, Is.Not.EqualTo(request.Password));

            var decryptedConn = encryptorFactory.GetEncryptor(CredentialType.ConnectionString).Decrypt(db.ConnectionString);
            var decryptedPwd = encryptorFactory.GetEncryptor(CredentialType.Password).Decrypt(db.Password);

            Assert.That(decryptedConn, Is.EqualTo(request.ConnectionString));
            Assert.That(decryptedPwd, Is.EqualTo(request.Password));
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
                    DatabaseAlias = "sample-db-alias",
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
            var response = await _client.GetAsync("/connections");

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
                    DatabaseAlias = "old-db-alias",
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
            var response = await _client.PutAsJsonAsync("/connections", request);

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
