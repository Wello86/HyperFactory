using System.Reflection;
using BrownEvents.Api.Data;
using BrownEvents.Api.Services;
using BrownEvents.Api.SwaggerExamples;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.Filters;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
// Root cause of concurrency crashes under load: this was previously AddSingleton, so every
// concurrent HTTP request shared one AppDbContext instance. EF Core's DbContext/ChangeTracker
// and the underlying Npgsql connection are not thread-safe, so overlapping requests raced on
// the same instance and threw InvalidOperationException ("a second operation was started on
// this context before a previous operation completed"). AddScoped gives each request its own
// instance, matching how the already-Scoped services below expect to consume it.
builder.Services.AddScoped<AppDbContext>(sp =>
{
    var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connectionString)
        .UseLazyLoadingProxies();
    if (builder.Environment.IsDevelopment())
    {
        optionsBuilder.EnableSensitiveDataLogging();
    }
    return new AppDbContext(optionsBuilder.Options);
});

builder.Services.AddScoped<IConferenceService, ConferenceService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ISpeakerService, SpeakerService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<ISessionRegistrationService, SessionRegistrationService>();

builder.Services.AddControllers();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "DELETE")
            .WithHeaders("Content-Type", "Authorization");
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BrownEvents API",
        Version = "v1",
        Description = "API for managing conferences, sessions, speakers, and attendee registrations."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));

    c.ExampleFilters();
});
builder.Services.AddSwaggerExamplesFromAssemblyOf<ConferenceExample>();

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
    context.RequestServices.GetRequiredService<ILogger<Program>>()
        .LogError(exceptionFeature?.Error, "Unhandled exception processing request");

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";

    var problem = new ProblemDetails
    {
        Title = "An unexpected error occurred.",
        Status = StatusCodes.Status500InternalServerError,
        Detail = "An unexpected error occurred while processing the request.",
    };
    await context.Response.WriteAsJsonAsync(problem);
}));

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Default");
app.MapControllers();

// Seed demo data on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    await DataSeeder.SeedAsync(context);
}

app.Run();

public partial class Program { }
