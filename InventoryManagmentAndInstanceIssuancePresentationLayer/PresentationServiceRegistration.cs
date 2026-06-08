using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using InfrastructureLayer.Security;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Text;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer
{
    /// <summary>
    /// Registers presentation-layer concerns: JWT bearer authentication bound to
    /// <see cref="JwtOptions"/>, request localization, and the current-principal accessor.
    /// </summary>
    public static class PresentationServiceRegistration
    {
        /// <summary>Adds presentation dependencies and authentication.</summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">App configuration (JWT section).</param>
        public static IServiceCollection AddPresentation(
            this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentTenant, CurrentTenant>();
            services.AddLocalization(options => options.ResourcesPath = "Resources");
            // PresentationServiceRegistration.AddPresentation — after AddLocalization(...)
            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supported = new[] { new CultureInfo("en"), new CultureInfo("ar") };
                options.DefaultRequestCulture = new RequestCulture("en");
                options.SupportedCultures = supported;
                options.SupportedUICultures = supported;
                options.RequestCultureProviders = new IRequestCultureProvider[]
                {
        new QueryStringRequestCultureProvider(),         // ?culture=ar wins
        new AcceptLanguageHeaderRequestCultureProvider() // then Accept-Language
                };
            });
            // PresentationServiceRegistration.AddPresentation — add after AddLocalization(...)
            services.Configure<RequestLocalizationOptions>(options =>
            {
                var supported = new[] { new CultureInfo("en"), new CultureInfo("ar") };
                options.DefaultRequestCulture = new RequestCulture("en");
                options.SupportedCultures = supported;
                options.SupportedUICultures = supported;
                // Order: explicit ?culture= wins, then the Accept-Language header.
                options.RequestCultureProviders = new IRequestCultureProvider[]
                {
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
                };
            });

            JwtOptions jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwt.Issuer,
                        ValidAudience = jwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                        // ... existing validation params ...
                        NameClaimType = JwtTokenGenerator.UsernameClaim // User.Identity.Name resolves to username
                    };
                });

            services.AddAuthorization();
            return services;
        }
    }
}
