using System;
using ApplicationLayer.BatchUpload;
using ApplicationLayer.CardFiles;
using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using ApplicationLayer.ServicesContracts;
using InfrastructureLayer.Data;
using InfrastructureLayer.Data.Interceptors;
using InfrastructureLayer.Reporting;
using InfrastructureLayer.Security;
using InfrastructureLayer.Services;
using InfrastructureLayer.Storage;
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

            // PAN Storage Redesign: deliberately a separate secret from BatchCipherOptions (key
            // separation) — same fail-fast pattern as the JWT signing key and the batch-cipher
            // master secret, so a misconfigured PanHash secret surfaces as a clear startup error
            // rather than a confusing failure on the first batch upload.
            services.AddOptions<PanHashOptions>()
                .Bind(configuration.GetSection(PanHashOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.MasterSecret), "PanHash MasterSecret is required.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Salt), "PanHash Salt is required.")
                .Validate(o => Convert.TryFromBase64String(o.Salt, new byte[128], out _), "PanHash Salt must be valid base64.")
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

            // Card File Generation, Phase 9.6: defaulted, so no configuration is required for the
            // endpoint to be safe out of the box.
            services.Configure<CardFileOptions>(configuration.GetSection(CardFileOptions.SectionName));

            // Batch Upload Phased Plan, Phase 7.
            // Stateless, no dependencies -> Singleton, matching Pbkdf2PasswordHasher's precedent.
            services.AddSingleton<IBatchRowParser, BatchRowParser>();
            services.AddSingleton<ICardFileWriter, CardFileWriter>();
            services.AddSingleton<IFailedRowsReportBuilder, FailedRowsReportBuilder>();

            // Card File Generation, Phase 9.2/9.6: one implementation, two narrow contracts. Both
            // resolve to the SAME registration lifetime and type so encrypt and decrypt can never
            // drift apart in key derivation. Registered separately rather than via a shared
            // instance because BatchFileCipher is stateless -- two instances behave identically.
            services.AddScoped<IBatchFileDecryptor, BatchFileCipher>();
            services.AddScoped<IBatchFileEncryptor, BatchFileCipher>();

            // PAN Storage Redesign: Scoped (not Singleton) so the derived-key cache inside
            // PanFingerprintGenerator lives for exactly one request/batch upload — see the class
            // doc comment for why that scope matters.
            services.AddScoped<IPanFingerprintGenerator, PanFingerprintGenerator>();

            services.AddScoped<IBatchUploadService, BatchUploadService>();
            services.AddScoped<System.Func<IBatchUploadService>>(sp => sp.GetRequiredService<IBatchUploadService>);

            services.AddScoped<ICardFileGenerationService, CardFileGenerationService>();
            services.AddScoped<System.Func<ICardFileGenerationService>>(sp => sp.GetRequiredService<ICardFileGenerationService>);

            services.AddScoped<ITransferComposer, TransferComposer>();
            services.AddScoped<ITransferService, TransferService>();
            services.AddScoped<System.Func<ITransferService>>(sp => sp.GetRequiredService<ITransferService>);
            services.AddScoped<IDisposalService, DisposalService>();
            services.AddScoped<System.Func<IDisposalService>>(sp => sp.GetRequiredService<IDisposalService>);
            services.AddScoped<IBranchRequestFulfilment, BranchRequestFulfilment>();
            services.AddScoped<IBranchRequestService, BranchRequestService>();
            services.AddScoped<System.Func<IBranchRequestService>>(sp => sp.GetRequiredService<IBranchRequestService>);

            // Printing Module, Phase 4: defaulted (see PrintImageOptions), so no configuration is
            // required for the endpoint to be safe out of the box - same reasoning as CardFileOptions.
            services.Configure<PrintImageOptions>(configuration.GetSection(PrintImageOptions.SectionName));
            services.AddScoped<IPrintImageStorage, LocalDiskPrintImageStorage>();
            services.AddScoped<IPrintImageService, PrintImageService>();
            services.AddScoped<System.Func<IPrintImageService>>(sp => sp.GetRequiredService<IPrintImageService>);

            // Printing Module, Phase 5.
            services.AddScoped<IPrinterConfigurationService, PrinterConfigurationService>();
            services.AddScoped<System.Func<IPrinterConfigurationService>>(sp => sp.GetRequiredService<IPrinterConfigurationService>);

            // Printing Module, Phase 6.
            services.AddScoped<IProductPrintConfigComposer, ProductPrintConfigComposer>();
            services.AddScoped<IProductPrintConfigurationService, ProductPrintConfigurationService>();
            services.AddScoped<System.Func<IProductPrintConfigurationService>>(sp => sp.GetRequiredService<IProductPrintConfigurationService>);

            return services;

           
        }
    }
}
