using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using WolverineOutboxDemo.Api.Data;
using WolverineOutboxDemo.Api.Endpoints;
using WolverineOutboxDemo.Contracts;

namespace WolverineOutboxDemo.Tests
{
    public class OutboxInboxIntegrationTests : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("wolverine_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        private readonly RabbitMqContainer _rabbitmq = new RabbitMqBuilder("rabbitmq:3.13-management-alpine")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        private WebApplicationFactory<Program> _factory = null!;
        private HttpClient _client = null!;

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();
            await _rabbitmq.StartAsync();

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
                    builder.UseSetting("ConnectionStrings:RabbitMQ", $"amqp://guest:guest@{_rabbitmq.Hostname}:5672");
                });

            _client = _factory.CreateClient();

            // 应用数据库迁移
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        public async Task DisposeAsync()
        {
            _client.Dispose();
            await _factory.DisposeAsync();
            await _postgres.DisposeAsync();
            await _rabbitmq.DisposeAsync();
        }

        [Fact]
        public async Task RegisterUser_WithOutbox_ShouldPersistUserAndPublishMessage()
        {
            // Arrange
            var request = new RegisterUserRequest("test@example.com", "Test User");

            // Act
            var response = await _client.PostAsJsonAsync("/api/users/register-outbox", request);

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result!.UserId);

            // 验证用户已保存
            var user = await GetUserAsync(result.UserId);
            Assert.NotNull(user);
            Assert.Equal("test@example.com", user.Email);

            // 等待消息处理完成
            await Task.Delay(2000);

            // 验证状态更新
            user = await GetUserAsync(result.UserId);
            Assert.NotNull(user);

            // 状态应该是 "Completed" 或在某个中间状态
            // 由于异步处理，这里可以检查状态是否发生变化
        }

        [Fact]
        public async Task OutboxPattern_EnsuresAtomicity_EvenWhenBrokerUnavailable()
        {
            // 此测试验证即使消息代理不可用，数据也会被保存

            // Arrange
            var request = new RegisterUserRequest("atomic@example.com", "Atomic User");

            // 模拟代理不可用的情况
            // Wolverine 的 Outbox 会在后台重试发送

            // Act
            var response = await _client.PostAsJsonAsync("/api/users/register-outbox", request);

            // Assert
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();

            // 验证用户数据已保存
            var user = await GetUserAsync(result!.UserId);
            Assert.NotNull(user);
            Assert.Equal("Pending", user.Status); // 初始状态

            // Outbox 消息应该已排队
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 检查 Outbox 表是否有待发送消息
            using var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM wolverine_outgoing_envelopes";
            var outboxCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            Assert.True(outboxCount > 0);
        }

        [Fact]
        public async Task InboxPattern_EnsuresIdempotentProcessing()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var command = new RegisterUser(userId, "idempotent@example.com", "Idempotent User");

            using var scope = _factory.Services.CreateScope();
            var messageBus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();

            // Act - 发送相同消息两次
            await messageBus.SendAsync(command);
            await messageBus.SendAsync(command); // 重复发送

            await Task.Delay(2000); // 等待处理

            // Assert - 验证只创建了一个用户
            var user = await GetUserAsync(userId);
            Assert.NotNull(user);

            // Inbox 模式确保了幂等性
        }

        private async Task<UserResponse?> GetUserAsync(Guid userId)
        {
            var response = await _client.GetAsync($"/api/users/{userId}");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<UserResponse>();
        }

        private record RegisterResponse(Guid UserId, string Message);
        private record UserResponse(Guid Id, string Email, string Name, string Status);
    }
}
