using PaymentsService.Database;
using PaymentsService.UseCases.AccountBalance;
using PaymentsService.UseCases.ConsumePayment;
using PaymentsService.UseCases.CreateAccount;
using PaymentsService.UseCases.OrderStatusUpdate;
using PaymentsService.UseCases.ProcessPayment;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Threading;
using Microsoft.OpenApi.Models;
using Confluent.Kafka;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Payments Service API",
        Version = "v1",
        Description = "API for creating, refilling, monitoring user accounts and processing order payments.",
        Contact = new OpenApiContact
        {
            Name = "Mikhail Gerasimov",
            Email = "disgolem@gmail.com"
        }
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

builder.Services.AddDbContext<PaymentContext>((serviceProvider, options) =>
{
    Microsoft.Extensions.Configuration.IConfiguration? config = null;
    for (var i = 0; i < 5; ++i)
    {
        try
        {
            config = serviceProvider.GetRequiredService<IConfiguration>();
            options.UseNpgsql(config.GetConnectionString("Default"));
            break;
        }

        catch (Exception)
        {
            Thread.Sleep(5000);
        }
    }
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var producerConfig = new ProducerConfig
    {
        BootstrapServers = config.GetSection("Kafka:BootstrapServers").Value
    };

    IProducer<string, string>? producer = null;

    for (var i = 0; i < 5; ++i)
    {
        try
        {
            producer = new ProducerBuilder<string, string>(producerConfig).Build();
            break;
        }
        catch (Exception)
        {
            Thread.Sleep(5000);
        }
    }

    if (producer == null)
    {
        throw new Exception("Kafka Producer connection failed after 5 retries");
    }

    return producer;
});

builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var consumerConfig = new ConsumerConfig
    {
        BootstrapServers = config.GetSection("Kafka:BootstrapServers").Value,
        GroupId = config.GetValue<string>("Kafka:GroupId")
    };

    IConsumer<string, string>? consumer = null;

    for (var i = 0; i < 5; ++i)
    {
        try
        {
            consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            break;
        }
        catch (Exception)
        {
            Thread.Sleep(5000);
        }
    }

    if (consumer == null)
    {
        throw new Exception("Kafka Consumer connection failed after 5 retries");
    }

    return consumer;
});

builder.Services.AddHostedService(sp =>
{
    var producer = sp.GetRequiredService<IProducer<string, string>>();
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new OrderStatusUpdateService(
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<OrderStatusUpdateService>>(),
        producer,
        configuration.GetValue<string>("Kafka:Topics:OrderStatus")
            ?? throw new InvalidOperationException("Kafka topic is not configured")
    );
});

builder.Services.AddHostedService(sp =>
{
    try
    {
        var service = new PaymentProcessService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<PaymentProcessService>>());

        Console.WriteLine("Registered PaymentProcessService");
        return service;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to register PaymentProcessService: {ex}");
        throw;
    }
});

builder.Services.AddHostedService(sp =>
{
    var consumer = sp.GetRequiredService<IConsumer<string, string>>();
    var configuration = sp.GetRequiredService<IConfiguration>();

    ConsumePaymentService? paymentService = null;

    for (var i = 0; i < 10; ++i)
    {
        try
        {
            paymentService = new ConsumePaymentService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<ConsumePaymentService>>(),
                consumer,
                configuration.GetValue<string>("Kafka:Topics:PaymentTask"));

            if (paymentService == null)
            {
                throw new Exception("Error");
            }
            break;
        }
        catch (Exception)
        {
            Thread.Sleep(5000);
        }
    }

    return paymentService;
});

builder.Services.AddScoped<IAccountBalanceService, AccountBalanceService>();
builder.Services.AddScoped<ICreateAccountService, CreateAccountService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PaymentContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    const int maxRetries = 5;
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            logger.LogInformation("Applying migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Migrations completed.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Migration failed. Retrying...");
            await Task.Delay(3000);
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();

app.MapControllers();

app.Run();