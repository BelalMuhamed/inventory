namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for print-configuration image uploads, bound from the <c>"PrintImages"</c>
    /// section (Printing Module, decision Q-10).
    /// </summary>
    public sealed class PrintImageOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "PrintImages";

        /// <summary>
        /// Physical root directory images are written under, relative to the content root (e.g.
        /// <c>wwwroot/uploads/products</c>). Each tenant gets its own subdirectory beneath this
        /// root — <c>{RootPath}/{tenantId}/{guid}.{extension}</c> — so a duplicate file name from
        /// one tenant can never collide with, or overwrite, another tenant's file (decision Q-10).
        /// </summary>
        public string RootPath { get; set; } = "wwwroot/uploads/products";

        /// <summary>
        /// Public URL prefix matching <see cref="RootPath"/> (e.g. <c>/uploads/products</c>),
        /// used to build the <c>imagePath</c> returned to clients. Kept separate from
        /// <see cref="RootPath"/> so the physical location and the served URL can diverge without
        /// a code change.
        /// </summary>
        public string PublicBaseUrl { get; set; } = "/uploads/products";

        /// <summary>Maximum accepted upload size in bytes. Defaults to 5 MB.</summary>
        public long MaxSizeBytes { get; set; } = 5 * 1024 * 1024;

        /// <summary>
        /// Allowed file extensions (lowercase, with leading dot). Extension is one input to the
        /// upload check; the file's actual content is independently verified via magic-byte
        /// signature sniffing (decision Q-10) — an allowed extension alone never admits a file.
        /// </summary>
        public string[] AllowedExtensions { get; set; } = { ".png", ".jpg", ".jpeg" };
    }
}
