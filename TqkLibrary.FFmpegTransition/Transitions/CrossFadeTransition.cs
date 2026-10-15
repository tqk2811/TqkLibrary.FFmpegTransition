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
                // Layers with alpha: mix linearly in premultiplied space, so two opaque layers stay opaque through the fade,
                // transparent areas stay transparent, and a layer over a transparent area keeps its own colour at the mixed alpha
                // (a plain per-plane mix would also mix in the black colour of transparent pixels and darken it):
                //   outA = Aa*p + Ab*(1-p);  outC = (Ca*Aa*p + Cb*Ab*(1-p)) / outA
                // geq reads one input only, so the layers are put side by side (second left, first right), mixed in rgba (alpha
                // weighting of yuv chroma would need the 128 offset and the subsampled alpha) and the left half is kept.
                string progress = $"min(T/{totalDuration.TotalSeconds},1)";
                string secondWeight = $"alpha(X,Y)*{progress}";
                string firstWeight = $"alpha(X+W/2,Y)*(1-{progress})";
                string alphaSum = $"({secondWeight}+{firstWeight})";
                string Mix(string channel)
                    => $"if(gt({alphaSum},0),({channel}(X,Y)*{secondWeight}+{channel}(X+W/2,Y)*{firstWeight})/{alphaSum},0)";
                return second_imageMap
                    .FormatFilter(PixFmt.rgba).MapOut
                    .HstackFilter(first_imageMap.FormatFilter(PixFmt.rgba).MapOut)
                        .Shortest(true)
                        .MapOut
                    .GeqFilter()
                        .R(Mix("r"))
                        .G(Mix("g"))
                        .B(Mix("b"))
                        .A(alphaSum)
                        .MapOut
                    .CropFilter()
                        .W("iw/2")
                        .H("ih")
                        .X("0")
                        .Y("0")
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
