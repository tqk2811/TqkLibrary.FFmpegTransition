using FFmpegArgs.Cores.Enums;
using FFmpegArgs.Cores.Maps;
using FFmpegArgs.Filters.VideoFilters;
using System;
using TqkLibrary.FFmpegTransition.Interfaces;

namespace TqkLibrary.FFmpegTransition.Transitions
{
    /// <summary>
    /// Shared settings of every transition. With the defaults (<see cref="PixelFormat"/> null, <see cref="KeepAlpha"/> false) a
    /// transition builds exactly the same graph as before these settings existed.
    /// </summary>
    public abstract class TransitionBase : ITransition
    {
        public abstract bool IsConcat { get; }

        /// <summary>
        /// Pixel format both inputs are converted to before the transition and the result is given in; null = each transition keeps
        /// its own formats (CrossFade outputs yuv420 unless <see cref="KeepAlpha"/>).
        /// </summary>
        public PixFmt? PixelFormat { get; set; }

        /// <summary>
        /// Treats the inputs as transparent layers: the fades work on the alpha channel and the result keeps it (e.g. two overlays on
        /// transparent canvases, composited later onto a background). Pair it with an alpha <see cref="PixelFormat"/> (yuva420p, argb...).
        /// </summary>
        public bool KeepAlpha { get; set; }

        public ImageMap MakeTransition(ImageMap first_imageMap, ImageMap second_imageMap, TimeSpan totalDuration, double fps)
        {
            if (first_imageMap is null) throw new ArgumentNullException(nameof(first_imageMap));
            if (second_imageMap is null) throw new ArgumentNullException(nameof(second_imageMap));
            if (this.PixelFormat is PixFmt inputFormat)
            {
                first_imageMap = first_imageMap.FormatFilter(inputFormat).MapOut;
                second_imageMap = second_imageMap.FormatFilter(inputFormat).MapOut;
            }
            ImageMap result = this.Make(first_imageMap, second_imageMap, totalDuration, fps);
            return this.PixelFormat is PixFmt outputFormat ? result.FormatFilter(outputFormat).MapOut : result;
        }

        /// <summary>geq alpha of a visible pixel of a masked layer: opaque, or with <see cref="KeepAlpha"/> the layer's own alpha.</summary>
        protected string VisibleAlpha => this.KeepAlpha ? "alpha(X,Y)" : "255";

        /// <summary>Overlays a masked layer on <paramref name="first_imageMap"/>; with <see cref="KeepAlpha"/> the output keeps an alpha channel.</summary>
        protected ImageMap OverlayOnFirst(ImageMap masked, ImageMap first_imageMap)
        {
            var overlay = masked.OverlayFilterOn(first_imageMap);
            return this.KeepAlpha ? overlay.Format(OverlayPixFmt.rgb).MapOut : overlay.MapOut;
        }

        /// <summary>The transition itself, on inputs already converted to <see cref="PixelFormat"/> when it is set.</summary>
        protected abstract ImageMap Make(ImageMap first_imageMap, ImageMap second_imageMap, TimeSpan totalDuration, double fps);
    }
}
