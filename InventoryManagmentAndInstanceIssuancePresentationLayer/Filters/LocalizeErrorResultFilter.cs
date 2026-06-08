// PresentationLayer/Filters/LocalizeErrorResultFilter.cs
using System.Threading.Tasks;
using ApplicationLayer.Localization;
using InventoryManagmentAndInstanceIssuancePresentationLayer.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Filters
{
    /// <summary>
    /// Centralized localization of error responses (Item 1, strategy B). Runs after every action,
    /// reads the <c>Error.Code</c> off the response envelope, and replaces the message with its
    /// culture-specific text (resolved from the <c>Accept-Language</c> header by the request
    /// localization middleware). Controllers and <c>ToHttpResponse</c> stay untouched.
    /// </summary>
    public sealed class LocalizeErrorResultFilter : IAsyncResultFilter
    {
        private readonly IStringLocalizer<Messages> _localizer;

        /// <summary>Creates the filter with the shared message localizer.</summary>
        public LocalizeErrorResultFilter(IStringLocalizer<Messages> localizer) => _localizer = localizer;

        /// <inheritdoc />
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.Result is ObjectResult { Value: ILocalizableApiResponse envelope } &&
                envelope.Error is { Code.Length: > 0 } error)
            {
                LocalizedString localized = error.Message is null
                    ? _localizer[error.Code]
                    : _localizer[error.Code, error.Message];

                // Fall back to the English default baked into the error if no resource entry exists.
                if (!localized.ResourceNotFound)
                {
                    envelope.ReplaceErrorMessage(localized.Value);
                }
            }

            await next();
        }
    }
}