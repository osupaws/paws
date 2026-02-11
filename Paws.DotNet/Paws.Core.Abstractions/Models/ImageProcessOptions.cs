namespace Paws.Core.Abstractions.Models
{
    public class ImageProcessOptions
    {
        public int? Width { get; set; }
        public int? Height { get; set; }

        /// <summary>
        /// Target format (e.g., "jpg", "png", "webp", "bmp").
        /// </summary>
        public string? TargetFormat { get; set; }

        /// <summary>
        /// Quality for lossy formats (1-100).
        /// </summary>
        public int Quality { get; set; } = 90;

        /// <summary>
        /// If true, maintains aspect ratio when resizing.
        /// </summary>
        public bool PreserveAspectRatio { get; set; } = true;

        /// <summary>
        /// Background color for transparency removal (e.g., "#FFFFFF").
        /// </summary>
        public string? BackgroundColor { get; set; }
    }
}
