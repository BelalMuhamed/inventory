using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using ApplicationLayer.ServicesContracts;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Interceptors;
using InfrastructureLayer.Security;
using InfrastructureLayer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace InfrastructureLayer
{
    /// <summary>
    /// Registers infrastructure services (EF Core context, unit of work, repositories, security
    /// primitives, and the service façade) with the DI container. Called from the Presentation
    /// composition root.
    /// </summary>
    public static class InfrastructureServiceRegistration
    {
        /// <summary>Adds all infrastructure dependencies.</summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">App configuration (connection strings, JWT section).</param>
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<AuditSaveChangesInterceptor>();
           
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
                options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
            });
            services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey), "JWT SigningKey is required.")
            .ValidateOnStart();
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            // InfrastructureLayer/InfrastructureServiceRegistration.cs  (modified registrations only)
            //   ... existing registrations unchanged ...
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<IAuditLogger, AuditLogger>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITenantService, TenantService>();          // <-- added
            services.AddScoped<IServiceManager, ServiceManager>();
            services.AddScoped<System.Func<IAuthService>>(sp => sp.GetRequiredService<IAuthService>);
            services.AddScoped<System.Func<ITenantService>>(sp => sp.GetRequiredService<ITenantService>);  // <-- added

            return services;

           
        }
    }
}
