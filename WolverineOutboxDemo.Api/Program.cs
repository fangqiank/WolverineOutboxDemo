using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;
using WolverineOutboxDemo.Api.Data;
using WolverineOutboxDemo.Api.Endpoints;
using WolverineOutboxDemo.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=wolverine_demo;Username=postgres;Password=postgres";
    options.UseNpgsql(connectionString);
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
});

builder.Host.UseWolverine(opts =>
{
    var rabbitUri = builder.Configuration.GetConnectionString("RabbitMQ")
        ?? "amqp://guest:guest@localhost:5672";

    opts.UseRabbitMq(new Uri(rabbitUri))
        .AutoProvision()
        .AutoPurgeOnStartup();

    opts.PublishMessage<RegisterUser>()
        .ToRabbitExchange("user-registration", exchange =>
        {
            exchange.IsDurable = true;
            exchange.ExchangeType = ExchangeType.Topic;
        });

    opts.PublishMessage<UserRegistered>()
        .ToRabbitExchange("user-events", exchange =>
        {
            exchange.IsDurable = true;
            exchange.ExchangeType = ExchangeType.Topic;
        });

    opts.PublishMessage<SendWelcomeEmail>()
        .ToRabbitQueue("send-welcome-email");

    opts.PublishMessage<WelcomeEmailSent>()
        .ToRabbitExchange("user-events");

    opts.ListenToRabbitQueue("user-registration-queue")
        .UseDurableInbox();

    opts.ListenToRabbitQueue("user-events-queue")
        .UseDurableInbox();

    opts.ListenToRabbitQueue("send-welcome-email")
        .UseDurableInbox();

    // 配置 EF Core 持久化
    var pgConnectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? "Host=localhost;Port=5432;Database=wolverine_demo;Username=postgres;Password=postgres";
    opts.PersistMessagesWithPostgresql(pgConnectionString);

    opts.UseEntityFrameworkCoreTransactions();

    opts.Services.AddDbContextWithWolverineIntegration<AppDbContext>(
        x => x.UseNpgsql(pgConnectionString)
    );

    // 对所有发送端点启用 Durable Outbox
    // 这意味着所有消息发布都会先写入 Outbox 表
    opts.Policies.UseDurableOutboxOnAllSendingEndpoints();

    // 对所有监听端点启用 Durable Inbox
    // 这意味着所有接收的消息都会先写入 Inbox 表
    opts.Policies.UseDurableInboxOnAllListeners();

    if (builder.Environment.IsDevelopment())
    {
        opts.Durability.Mode = DurabilityMode.Solo;
        opts.DefaultLocalQueue.MaximumParallelMessages(5);
    }

    opts.Policies.OnException<Exception>()
    .RetryWithCooldown(
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2)
    );

});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapUserEndpoints();

// 健康检查端点
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow }));

app.Run();
