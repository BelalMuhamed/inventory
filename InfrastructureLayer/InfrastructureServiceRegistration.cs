using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using ApplicationLayer.ServicesContracts;
using InfrastructureLayer.Data;
using InfrastructureLayer.Security;
using InfrastructureLayer.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IServiceManager, ServiceManager>();
            services.AddScoped<System.Func<IAuthService>>(sp => sp.GetRequiredService<IAuthService>);

            return services;
        }
    }
}
