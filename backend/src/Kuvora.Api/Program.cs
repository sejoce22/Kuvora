using Kuvora.Api.ErrorHandling;
using Kuvora.Api.Extensions;
using Kuvora.Application;
using Kuvora.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Falta ConnectionStrings:Default.");

    builder.Services.AddInfrastructure(connectionString);
    builder.Services.AddApplication();

    builder.Services.AddJwtAuthentication();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "La aplicación terminó de forma inesperada");
}
finally
{
    Log.CloseAndFlush();
}