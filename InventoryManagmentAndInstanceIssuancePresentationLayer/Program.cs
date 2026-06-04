using ApplicationLayer.Options;
using InfrastructureLayer;
using InfrastructureLayer.Data;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Middleware;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            // Replace the default [ApiController] 400 model-validation response with our 422 envelope.
            builder.Services.Configure<ApiBehaviorOptions>(options =>
            {
                options.InvalidModelStateResponseFactory = ValidationResponseFactory.Build;
            });
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

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

            // Authentication must run before authorization so the principal is established first.
            app.UseAuthentication();
            app.UseAuthorization();


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
