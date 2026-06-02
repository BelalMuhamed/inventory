namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Common
{
    /// <summary>
    /// Uniform response envelope returned by every endpoint, on both success and failure.
    /// Clients branch on <see cref="Success"/>: when true, <see cref="Data"/> holds the payload
    /// (a resource or a <c>PaginatedResponse</c>) and <see cref="Error"/> is null; when false,
    /// <see cref="Error"/> describes the failure and <see cref="Data"/> is null.
    /// <see cref="TraceId"/> is present on every response to correlate client reports with logs.
    /// </summary>
    /// <typeparam name="T">Type of the success payload.</typeparam>
    public sealed class ApiResponse<T>
    {
        /// <summary>True when the operation succeeded.</summary>
        public bool Success { get; init; }

        /// <summary>The payload on success; null on failure.</summary>
        public T? Data { get; init; }

        /// <summary>The failure detail on error; null on success.</summary>
        public ApiError? Error { get; init; }

        /// <summary>Correlation identifier for this request, echoed in logs and the response header.</summary>
        public string TraceId { get; init; } = string.Empty;

        /// <summary>Builds a successful envelope wrapping <paramref name="data"/>.</summary>
        public static ApiResponse<T> Ok(T data, string traceId) => new()
        {
            Success = true,
            Data = data,
            Error = null,
            TraceId = traceId
        };

        /// <summary>Builds a failed envelope carrying <paramref name="error"/>.</summary>
        public static ApiResponse<T> Fail(ApiError error, string traceId) => new()
        {
            Success = false,
            Data = default,
            Error = error,
            TraceId = traceId
        };
    }
}
