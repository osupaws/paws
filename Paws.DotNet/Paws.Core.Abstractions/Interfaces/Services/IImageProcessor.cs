using System.IO;
using System.Threading.Tasks;
using Paws.Core.Abstractions.Models;

namespace Paws.Core.Abstractions.Interfaces.Services
{
    /// <summary>
    /// Centralized image processing service using Magick.NET.
    /// available to all plugins without special permissions.
    /// </summary>
    public interface IImageProcessor
    {
        /// <summary>
        /// Processes a stored asset or a direct stream and returns the result as a stream.
        /// </summary>
        Task<Stream> ProcessImageAsync(Stream input, ImageProcessOptions options);

        /// <summary>
        /// Processes a managed asset.
        /// </summary>
        Task<Stream> ProcessAssetAsync(string assetId, ImageProcessOptions options);

        /// <summary>
        /// Identifies the format of an image.
        /// </summary>
        Task<string> GetImageFormatAsync(Stream input);
    }
}
