using DomainLayer.Common;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Security
{
    /// <summary>
    /// Translates <see cref="Result"/> outcomes into HTTP responses using
    /// <see cref="ErrorCategory"/> as the single source of truth for status codes (API Spec §2.5).
    /// Centralizes the mapping so controllers stay thin and consistent.
    /// </summary>
    public static class ResultExtensions
    {
        /// <summary>Maps a valueless result: 200 on success, mapped error otherwise.</summary>
        /// <param name="result">The operation outcome.</param>
        public static IActionResult ToHttpResponse(this Result result)
            => result.IsSuccess ? new OkResult() : Problem(result.Error);

        /// <summary>Maps a typed result: 200 with the value on success, mapped error otherwise.</summary>
        /// <typeparam name="T">The success value type.</typeparam>
        /// <param name="result">The operation outcome.</param>
        public static IActionResult ToHttpResponse<T>(this Result<T> result)
            => result.IsSuccess ? new OkObjectResult(result.Value) : Problem(result.Error);

        private static IActionResult Problem(Error error)
        {
            int status = error.Category switch
            {
                ErrorCategory.Validation => StatusCodes.Status422UnprocessableEntity,
                ErrorCategory.Conflict => StatusCodes.Status409Conflict,
                ErrorCategory.NotFound => StatusCodes.Status404NotFound,
                ErrorCategory.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorCategory.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status400BadRequest
            };

            return new ObjectResult(new { error.Code, error.Message }) { StatusCode = status };
        }
    }
}
