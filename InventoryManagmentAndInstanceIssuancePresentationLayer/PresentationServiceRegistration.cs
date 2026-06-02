using System.Text;
using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

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

            JwtOptions jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

            services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwt.Issuer,
                        ValidAudience = jwt.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
                    };
                });

            services.AddAuthorization();
            return services;
        }
    }
}
