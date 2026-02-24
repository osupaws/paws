using System;
using System.IO;
using System.Threading.Tasks;
using ImageMagick;
using Paws.Core.Abstractions.Interfaces.Services;
using Paws.Core.Abstractions.Models;
using IStorageService = Paws.Core.Abstractions.Interfaces.Services.IStorageService;
using IImageProcessor = Paws.Core.Abstractions.Interfaces.Services.IImageProcessor;

namespace Paws.Host.Services.Core
{
    public class ImageProcessorService : IImageProcessor
    {
        private readonly IStorageService _storage;

        public ImageProcessorService(IStorageService storage)
        {
            _storage = storage;
        }

        public async Task<Stream> ProcessImageAsync(Stream input, ImageProcessOptions options)
        {
            var output = new MemoryStream();

            using (var image = new MagickImage(input))
            {
                // 1. Resize
                if (options.Width.HasValue || options.Height.HasValue)
                {
                    var size = new MagickGeometry((uint)(options.Width ?? 0), (uint)(options.Height ?? 0))
                    {
                        IgnoreAspectRatio = !options.PreserveAspectRatio
                    };
                    image.Resize(size);
                }

                // 2. Format & Quality
                if (!string.IsNullOrEmpty(options.TargetFormat))
                {
                    image.Format = Enum.TryParse<MagickFormat>(options.TargetFormat, true, out var format)
                        ? format
                        : MagickFormat.Jpg;
                }

                image.Quality = (uint)options.Quality;

                // 3. Background (for transparency flattening)
                if (!string.IsNullOrEmpty(options.BackgroundColor))
                {
                    image.BackgroundColor = new MagickColor(options.BackgroundColor);
                    image.Alpha(AlphaOption.Remove);
                }

                await image.WriteAsync(output);
            }

            output.Position = 0;
            return output;
        }

        public async Task<Stream> ProcessAssetAsync(string assetId, ImageProcessOptions options)
        {
            using (var assetStream = _storage.GetAssetStream(assetId))
            {
                return await ProcessImageAsync(assetStream, options);
            }
        }

        public Task<string> GetImageFormatAsync(Stream input)
        {
            using (var image = new MagickImage(input))
            {
                return Task.FromResult(image.Format.ToString().ToLowerInvariant());
            }
        }
    }
}
