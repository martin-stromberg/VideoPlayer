namespace VideoWebPlayer.Services.PlaylistCover
{
    /// <summary>
    /// The outcome of <see cref="PlaylistCoverValidator.ValidateUploadAsync"/>.
    /// </summary>
    public sealed class PlaylistCoverValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the uploaded file passed every validation rule.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the user-facing error message, or <see langword="null"/> when <see cref="IsValid"/> is <see langword="true"/>.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Gets the width, in pixels, of the validated image, or <see langword="null"/> when <see cref="IsValid"/> is <see langword="false"/>.
        /// </summary>
        public int? Width { get; }

        /// <summary>
        /// Gets the height, in pixels, of the validated image, or <see langword="null"/> when <see cref="IsValid"/> is <see langword="false"/>.
        /// </summary>
        public int? Height { get; }

        /// <summary>
        /// Gets the MIME type of the format the validated image bytes actually contain (detected from the
        /// image data, not taken from the client-supplied header), or <see langword="null"/> when
        /// <see cref="IsValid"/> is <see langword="false"/>.
        /// </summary>
        public string? ContentType { get; }

        private PlaylistCoverValidationResult(bool isValid, string? errorMessage, int? width, int? height, string? contentType)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
            Width = width;
            Height = height;
            ContentType = contentType;
        }

        /// <summary>
        /// Builds a successful result, carrying the dimensions of the validated image.
        /// </summary>
        /// <param name="width">The width, in pixels, of the validated image.</param>
        /// <param name="height">The height, in pixels, of the validated image.</param>
        /// <param name="contentType">The MIME type of the format the image bytes actually contain.</param>
        /// <returns>The successful validation result.</returns>
        public static PlaylistCoverValidationResult Success(int width, int height, string contentType)
            => new(true, null, width, height, contentType);

        /// <summary>
        /// Builds a failed result carrying the given user-facing error message.
        /// </summary>
        /// <param name="errorMessage">The user-facing error message describing why validation failed.</param>
        /// <returns>The failed validation result.</returns>
        public static PlaylistCoverValidationResult Failure(string errorMessage)
            => new(false, errorMessage, null, null, null);
    }
}
