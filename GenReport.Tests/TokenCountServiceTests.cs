using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.SharedServices.Core.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GenReport.Tests
{
    [TestFixture]
    public class TokenCountServiceTests
    {
        private ApplicationDbContext _dbContext;
        private TokenCountService _tokenCountService;
        private Mock<ILogger<TokenCountService>> _loggerMock;

        [SetUp]
        public void Setup()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "GenReportTestDb_" + System.Guid.NewGuid().ToString())
                .Options;

            _dbContext = new ApplicationDbContext(options);
            _loggerMock = new Mock<ILogger<TokenCountService>>();

            // The DB context is injected to the service
            _tokenCountService = new TokenCountService(_dbContext, _loggerMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _dbContext.Dispose();
        }

        [Test]
        public async Task GetSessionTokenCountAsync_ReturnsError_WhenSessionNotFound()
        {
            var response = await _tokenCountService.GetSessionTokenCountAsync(999, CancellationToken.None);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual("Session not found.", response.ErrorMessage);
        }

        [Test]
        public async Task GetSessionTokenCountAsync_ReturnsError_WhenAiConnectionMissing()
        {
            var session = new ChatSession
            {
                UserId = 1,
                AiConnection = null // No AI connection
            };
            _dbContext.ChatSessions.Add(session);
            await _dbContext.SaveChangesAsync();

            var response = await _tokenCountService.GetSessionTokenCountAsync(session.Id, CancellationToken.None);

            Assert.IsFalse(response.IsSuccess);
            Assert.AreEqual("No AI connection associated with this session.", response.ErrorMessage);
        }

        [Test]
        public async Task GetSessionTokenCountAsync_UsesTiktokenForOpenAI()
        {
            var aiConnection = new AiConnection
            {
                Provider = "openAI", // Mixed case to test normalisation
                ApiKey = "key",
                DefaultModel = "gpt-4",
                MaxTokens = 100,
                SystemPrompt = "You are a helpful assistant."
            };

            var session = new ChatSession
            {
                UserId = 1,
                AiConnection = aiConnection,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = "Hello!" }
                }
            };
            _dbContext.ChatSessions.Add(session);
            await _dbContext.SaveChangesAsync();

            var response = await _tokenCountService.GetSessionTokenCountAsync(session.Id, CancellationToken.None);

            Assert.IsTrue(response.IsSuccess);
            Assert.IsTrue(response.TotalTokens > 0, "Tokens should be calculated.");
            Assert.AreEqual(100, response.MaxTokens);
            Assert.IsFalse(response.IsExceeded, "Tokens should be under limit.");
            Assert.AreEqual("Local Tiktoken (Primary)", response.CalculationMethod);
        }

        [Test]
        public async Task GetSessionTokenCountAsync_FallsBackForAnthropic()
        {
            var aiConnection = new AiConnection
            {
                Provider = "anthropic",
                ApiKey = "key",
                DefaultModel = "claude-3-opus",
                MaxTokens = 10 // Setting low limit
            };

            var session = new ChatSession
            {
                UserId = 1,
                AiConnection = aiConnection,
                Messages = new List<ChatMessage>
                {
                    // Adding relatively lengthy text
                    new ChatMessage { Role = "user", Content = "Tell me a long story about a very adventurous dog that travels space. 12345 67890." }
                }
            };
            _dbContext.ChatSessions.Add(session);
            await _dbContext.SaveChangesAsync();

            var response = await _tokenCountService.GetSessionTokenCountAsync(session.Id, CancellationToken.None);

            Assert.IsTrue(response.IsSuccess);
            Assert.IsTrue(response.TotalTokens > 0);
            Assert.IsTrue(response.IsExceeded, "Tokens should exceed the artifically low limit.");
            Assert.AreEqual(10, response.MaxTokens);
            Assert.AreEqual("Local Tiktoken (Fallback)", response.CalculationMethod);
        }
    }
}
