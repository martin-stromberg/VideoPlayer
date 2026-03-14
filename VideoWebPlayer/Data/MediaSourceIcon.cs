namespace VideoWebPlayer.Data
{
	/// <summary>
	/// Stores an uploaded icon image for a media source.
	/// Kept separate from <see cref="Picture"/> to avoid heavy migrations on the main Pictures table.
	/// </summary>
	public class MediaSourceIcon
	{
		/// <summary>
		/// Gets or sets the icon identifier.
		/// </summary>
		public long Id { get; set; }
		/// <summary>
		/// Gets or sets the raw image data.
		/// </summary>
		public byte[] Data { get; set; } = [];
		/// <summary>
		/// Gets or sets the icon content type.
		/// </summary>
		public string ContentType { get; set; } = "image/png";
	}
}
