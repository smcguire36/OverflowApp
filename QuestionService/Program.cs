using Common;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.Services;
using Wolverine.RabbitMQ;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.AddServiceDefaults();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<TagService>();
builder.Services.AddKeyCloakAuthentication();       // Custom Extension Method in Common project

builder.AddNpgsqlDbContext<QuestionDbContext>("questionDb");

/*
builder.Services.AddOpenTelemetry().WithTracing(traceProviderBuilder =>
{
    traceProviderBuilder.SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService(builder.Environment.ApplicationName))
        .AddSource("Wolverine");
});
*/

await builder.UseWolverineWithRabbitMqAsync(builder.Configuration, opts => 
{
    opts.PublishAllMessages().ToRabbitExchange("questions");
    opts.ApplicationAssembly = typeof(Program).Assembly;
});

/*
builder.Host.UseWolverine(opts =>
{
    opts.UseRabbitMqUsingNamedConnection("messaging").AutoProvision();
    opts.PublishAllMessages().ToRabbitExchange("questions");
});
*/

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    var dbContext = services.GetRequiredService<QuestionDbContext>();
    await dbContext.Database.MigrateAsync();
}
catch (Exception e)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(e, "An error occurred while migrating or seeding the database.");
}

app.Run();
