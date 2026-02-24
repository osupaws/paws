using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OsuParsers.Decoders;
using OsuParsers.Storyboards;
using OsuParsers.Storyboards.Interfaces;

namespace Paws.Host.Services.Lazer
{
    public static class LazerStoryboardHelper
    {
        public static List<string> GetStoryboardAssetPaths(string filePath)
        {
            var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string firstLine = "";
                using (var reader = new StreamReader(filePath))
                {
                    firstLine = reader.ReadLine() ?? "";
                }

                if (firstLine.StartsWith("osu file format v"))
                {
                    var beatmap = BeatmapDecoder.Decode(filePath);
                    if (!string.IsNullOrEmpty(beatmap.GeneralSection.AudioFilename)) assets.Add(beatmap.GeneralSection.AudioFilename);
                    if (!string.IsNullOrEmpty(beatmap.EventsSection.BackgroundImage)) assets.Add(beatmap.EventsSection.BackgroundImage);
                    if (!string.IsNullOrEmpty(beatmap.EventsSection.Video)) assets.Add(beatmap.EventsSection.Video);

                    if (beatmap.EventsSection.Storyboard != null)
                        ExtractStoryboardAssets(beatmap.EventsSection.Storyboard, assets);

                    foreach (var obj in beatmap.HitObjects)
                    {
                        if (obj.Extras != null && !string.IsNullOrEmpty(obj.Extras.SampleFileName))
                            assets.Add(obj.Extras.SampleFileName);
                    }
                }
                else
                {
                    var sb = StoryboardDecoder.Decode(filePath);
                    ExtractStoryboardAssets(sb, assets);
                }
            }
            catch (Exception)
            {
                // Rethrow or handle? For now, we return what we found (likely empty)
                throw;
            }

            return assets.ToList();
        }

        private static void ExtractStoryboardAssets(Storyboard sb, HashSet<string> assets)
        {
            if (sb == null) return;
            void ProcessLayer(List<IStoryboardObject> layer)
            {
                if (layer == null) return;
                foreach (var obj in layer)
                {
                    if (!string.IsNullOrEmpty(obj.FilePath)) assets.Add(obj.FilePath);
                }
            }
            ProcessLayer(sb.BackgroundLayer);
            ProcessLayer(sb.FailLayer);
            ProcessLayer(sb.PassLayer);
            ProcessLayer(sb.ForegroundLayer);
            ProcessLayer(sb.OverlayLayer);
            ProcessLayer(sb.SamplesLayer);
        }
    }
}
