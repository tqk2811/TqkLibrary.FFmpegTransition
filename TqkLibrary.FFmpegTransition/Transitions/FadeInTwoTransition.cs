using FFmpegArgs.Cores.Maps;
using FFmpegArgs.Filters.Multimedia;
using FFmpegArgs.Filters.VideoFilters;
using System;
using System.Linq;
using TqkLibrary.FFmpegTransition.Interfaces;

namespace TqkLibrary.FFmpegTransition.Transitions
{
    public class FadeInTwoTransition : TransitionBase
    {
        public override bool IsConcat => true;
        protected override ImageMap Make(ImageMap first_imageMap, ImageMap second_imageMap, TimeSpan totalDuration, double fps)
        {
            TimeSpan stepDuration = TimeSpan.FromSeconds(totalDuration.TotalSeconds / 2);
            // KeepAlpha: fade to/from transparent instead of black.
            var fadeOutFilter = first_imageMap.FadeFilter().Type(FadeType.Out).StartFrame(0).NbFrames((int)(stepDuration.TotalSeconds * fps));
            var fadeInFilter = second_imageMap.FadeFilter().Type(FadeType.In).StartFrame(0).NbFrames((int)(stepDuration.TotalSeconds * fps));
            if (this.KeepAlpha)
            {
                fadeOutFilter.Alpha(true);
                fadeInFilter.Alpha(true);
            }
            var fade_out = fadeOutFilter.MapOut;
            var fade_in = fadeInFilter.MapOut;
            ConcatFilter concatFilter = new(new ConcatGroup(fade_out), new ConcatGroup(fade_in));
            return concatFilter.ImageMapsOut.First();//.TrimFilter().Duration(totalDuration).MapOut;
        }
    }
}
