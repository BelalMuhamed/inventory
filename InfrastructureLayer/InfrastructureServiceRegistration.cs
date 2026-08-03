using ApplicationLayer.BatchUpload;
using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using ApplicationLayer.ServicesContracts;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Interceptors;
using InfrastructureLayer.Reporting;
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

            // Batch Upload Phased Plan, Phase 7: same fail-fast pattern as the JWT signing key —
            // a misconfigured master secret should surface as a clear startup error, not a
            // confusing decrypt failure on the first upload attempt.
            services.AddOptions<BatchCipherOptions>()
                .Bind(configuration.GetSection(BatchCipherOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.MasterSecret), "BatchCipher MasterSecret is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Salt), "BatchCipher Salt is required.")
                .ValidateOnStart();
            // InfrastructureLayer/InfrastructureServiceRegistration.cs  (modified registrations only)
            //   ... existing registrations unchanged ...
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
            services.AddScoped<IAuditLogger, AuditLogger>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ITenantService, TenantService>();         
            services.AddScoped<IServiceManager, ServiceManager>();
            services.AddScoped<IBranchService, BranchService>();
            services.AddScoped<System.Func<IBranchService>>(sp => sp.GetRequiredService<IBranchService>);
            services.AddScoped<System.Func<IAuthService>>(sp => sp.GetRequiredService<IAuthService>);
            services.AddScoped<System.Func<ITenantService>>(sp => sp.GetRequiredService<ITenantService>);
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<System.Func<IProductService>>(sp => sp.GetRequiredService<IProductService>);
            services.AddScoped<IStockService, StockService>();
            services.AddScoped<System.Func<IStockService>>(sp => sp.GetRequiredService<IStockService>);

            services.AddScoped<IProductItemService, ProductItemService>();
            services.AddScoped<System.Func<IProductItemService>>(sp => sp.GetRequiredService<IProductItemService>);

            // Batch Upload Phased Plan, Phase 7.
            // Stateless, no dependencies -> Singleton, matching Pbkdf2PasswordHasher's precedent.
            services.AddSingleton<IBatchRowParser, BatchRowParser>();
            services.AddSingleton<IFailedRowsReportBuilder, FailedRowsReportBuilder>();
            // Stateless per call but takes IOptions<T> -> Scoped, matching JwtTokenGenerator's precedent.
            services.AddScoped<IBatchFileCipher, BatchFileCipher>();
            services.AddScoped<IBatchUploadService, BatchUploadService>();
            services.AddScoped<System.Func<IBatchUploadService>>(sp => sp.GetRequiredService<IBatchUploadService>);

            return services;

           
        }
    }
}
