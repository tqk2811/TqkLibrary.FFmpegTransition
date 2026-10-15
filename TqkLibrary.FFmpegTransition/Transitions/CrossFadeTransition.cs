using FFmpegArgs.Cores.Enums;
using FFmpegArgs.Cores.Maps;
using FFmpegArgs.Filters;
using FFmpegArgs.Filters.VideoFilters;
using System;
using TqkLibrary.FFmpegTransition.Interfaces;

namespace TqkLibrary.FFmpegTransition.Transitions
{
    public class CrossFadeTransition : TransitionBase
    {
        public override bool IsConcat => false;

        protected override ImageMap Make(ImageMap first_imageMap, ImageMap second_imageMap, TimeSpan totalDuration, double fps)
        {
            if (this.KeepAlpha)
            {
                // Layers with alpha: mix every plane (alpha too) linearly, so two opaque layers stay opaque through the fade and
                // transparent areas stay transparent (fade alpha + overlay would composite the two half-transparent layers instead).
                // A = second, B = first (the same order as the other blend transitions).
                string progress = $"min(T/{totalDuration.TotalSeconds},1)";
                return second_imageMap
                    .BlendFilterOn(first_imageMap)
                        .Shortest(true)
                        .All_Expr($"A*{progress}+B*(1-{progress})")
                        .MapOut;
            }
            var fade_out = first_imageMap
                .FormatFilter(PixFmt.argb)
                    .MapOut
                .FadeFilter()
                    .Type(FadeType.Out)
                    .StartFrame(0)
                    .Alpha(true)
                    .NbFrames((int)(totalDuration.TotalSeconds * fps))
                    .MapOut;
            var fade_in = second_imageMap
                .FormatFilter(PixFmt.argb)
                    .MapOut
                .FadeFilter()
                    .Type(FadeType.In)
                    .StartFrame(0)
                    .Alpha(true)
                    .NbFrames((int)(totalDuration.TotalSeconds * fps))
                    .MapOut;
            return fade_out.OverlayFilterOn(fade_in).Format(OverlayPixFmt.yuv420).MapOut;
        }
    }
}
