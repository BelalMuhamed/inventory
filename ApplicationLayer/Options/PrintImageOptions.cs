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
        /// Physical root directory images are written under, resolved relative to the
        /// application's content root (<see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ContentRootPath"/>,
        /// the same base Program.cs already resolves <c>LogEncryptionOptions.Directory</c>
        /// against) when not already absolute — see
        /// <c>LocalDiskPrintImageStorage.ResolvePhysicalRoot</c>. Deliberately not
        /// <c>wwwroot</c>-relative: Program.cs maps this exact resolved path to
        /// <see cref="PublicBaseUrl"/> via its own <c>StaticFileOptions</c>, so uploaded content
        /// never has to share a directory tree with other static web assets, and works
        /// identically whether or not the app even has a <c>wwwroot</c>. Each tenant gets its own
        /// subdirectory beneath this root — <c>{RootPath}/{tenantId}/{guid}.{extension}</c> — so a
        /// duplicate file name from one tenant can never collide with, or overwrite, another
        /// tenant's file (decision Q-10).
        /// </summary>
        public string RootPath { get; set; } = "uploads/products";

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
