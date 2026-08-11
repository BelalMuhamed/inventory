using ApplicationLayer.Options;
using InfrastructureLayer;
using InfrastructureLayer.Data;
using InfrastructureLayer.Logging;
using InfrastructureLayer.Storage;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Filters;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Middleware;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Swagger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using System.Reflection;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            // Build the encryptor from secrets (fail fast if the password is missing, like the JWT key).
            LogEncryptionOptions logOpts =
                builder.Configuration.GetSection(LogEncryptionOptions.SectionName).Get<LogEncryptionOptions>()
                ?? new LogEncryptionOptions();
            if (string.IsNullOrWhiteSpace(logOpts.Password) || string.IsNullOrWhiteSpace(logOpts.Salt))
            {
                throw new InvalidOperationException(
                    $"Encrypted logging requires '{LogEncryptionOptions.SectionName}:Password' and ':Salt' " +
                    "via user-secrets (development) or environment variables (production).");
            }

            var encryptor = new LogEncryptor(logOpts.Password, Convert.FromBase64String(logOpts.Salt));
            string logDir = Path.Combine(builder.Environment.ContentRootPath, logOpts.Directory);
            var formatter = new CompactJsonFormatter();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                // Exception file: anything carrying an exception.
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Exception is not null)
                    .WriteTo.Sink(new EncryptedFileSink(Path.Combine(logDir, logOpts.ExceptionFileName), encryptor, formatter)))
                // Error file: Warning+ with no exception.
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(e => e.Exception is null && e.Level >= LogEventLevel.Warning)
                    .WriteTo.Sink(new EncryptedFileSink(Path.Combine(logDir, logOpts.ErrorFileName), encryptor, formatter)))
                .CreateLogger();

            builder.Host.UseSerilog();

            // Add services to the container.

            // Program.cs — replace builder.Services.AddControllers();
            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<LocalizeErrorResultFilter>();
            });            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            // Replace the default [ApiController] 400 model-validation response with our 422 envelope.
            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = ValidationResponseFactory.Build;
            });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(op =>
            {
                

                op.OperationFilter<AcceptLanguageHeaderOperationFilter>();

                // PresentationServiceRegistration.AddPresentation — inside services.AddSwaggerGen(options => { ... })
                op.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,   // paste the raw token; Swagger adds the "Bearer " prefix
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Paste your JWT access token below (without the 'Bearer ' prefix)."
                });

                op.AddSecurityRequirement(new OpenApiSecurityRequirement
{
    {
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer"
            }
        },
        Array.Empty<string>()
    }
});

                // PresentationServiceRegistration.AddPresentation — inside services.AddSwaggerGen(options => { ... })
              

               
                

            });

            // Fail fast if the JWT signing key is missing: a misconfigured secret should surface
            // as a clear startup error, not as confusing 401s at request time.
            EnsureJwtSigningKeyPresent(builder.Configuration);

            // Onion composition: inner layers are wired before the presentation concerns that depend on them.
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddPresentation(builder.Configuration);
            // In Program.cs, where services are configured:
            builder.Services.AddAuthorization(AuthorizationPolicies.Register);
            var app = builder.Build();

            // Apply migrations and seed the bootstrap system admin on startup (idempotent).
            await DbSeeder.MigrateAndSeedAsync(app.Services);

            // Global exception handling must sit first so it wraps the entire downstream pipeline.
            app.UseMiddleware<GlobalExceptionMiddleware>();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            // Printing Module, Phase 4: serves uploaded print-configuration images at
            // PrintImageOptions.PublicBaseUrl, mapped directly to the resolved physical root -
            // LocalDiskPrintImageStorage.ResolvePhysicalRoot, so this can never point somewhere
            // different from where uploads are actually written. Deliberately its own
            // StaticFileOptions rather than a default app.UseStaticFiles() over wwwroot: uploaded
            // tenant content stays out of the wwwroot tree entirely, and this mapping still works
            // if the app has no wwwroot at all.
            // Unauthenticated by design, like any other static-file middleware - it runs outside
            // the MVC pipeline, before [Authorize] would apply. The physical file name is an
            // unguessable GUID and the path embeds no other tenant data, so this is the same
            // security posture as any link-shared asset. Flagged: if these images need to be
            // access-controlled instead, this needs a dedicated authenticated endpoint rather
            // than static-file middleware.
            PrintImageOptions printImageOptions =
                app.Services.GetRequiredService<IOptions<PrintImageOptions>>().Value;
            string printImagePhysicalRoot =
                LocalDiskPrintImageStorage.ResolvePhysicalRoot(app.Environment, printImageOptions);
            Directory.CreateDirectory(printImagePhysicalRoot);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(printImagePhysicalRoot),
                RequestPath = printImageOptions.PublicBaseUrl,
            });

            // Authentication must run before authorization so the principal is established first.
            app.UseAuthentication();
            app.UseAuthorization();

            // Program.cs — add the middleware BEFORE app.MapControllers() (and before auth is fine too)
            app.UseRequestLocalization();
            app.MapControllers();

            await app.RunAsync();
        }

        private static void EnsureJwtSigningKeyPresent(IConfiguration configuration)
        {
            string? signingKey = configuration[$"{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException(
                    $"JWT signing key is not configured. Set '{JwtOptions.SectionName}:{nameof(JwtOptions.SigningKey)}' " +
                    "via user-secrets (development) or an environment variable (production). " +
                    "It must never be committed to appsettings.json.");
            }
        }
    }
}
