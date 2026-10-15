using FFmpegArgs.Cores.Maps;
using FFmpegArgs.Filters;
using FFmpegArgs.Filters.VideoFilters;
using System;
using TqkLibrary.FFmpegTransition.Interfaces;

namespace TqkLibrary.FFmpegTransition.Transitions
{
    public class ClockTransition : TransitionBase
    {
        public override bool IsConcat => false;
        public bool Contrary { get; set; } = false;
        protected override ImageMap Make(ImageMap first_imageMap, ImageMap second_imageMap, TimeSpan totalDuration, double fps)
        {
            // (0.5W, 0.5H) -> (0.5W, 0) => vecto v1 = (0.5W-0.5W,0-0.5H)   = (0        ,    -0.5*H)
            // (0.5W, 0.5H) -> (X, Y) => vecto v2                           = (X - 0.5*W ,   Y - 0.5*H);
            // cos(v1,v2) = (a1*a2 + b1*b2)/[sqrt(a1*a1 + b1*b1) * sqrt(a2*a2 + b2*b2)]
            // = (-0.5*H * (Y - 0.5*H))/(sqrt(0.5*H*0.5*H) * sqrt((X - 0.5*W)*(X - 0.5*W) + (Y - 0.5*H)*(Y - 0.5*H)))
            //0 degrees => 1, 90 degrees => 0, 180 degrees => -1:   cos range 1 -> -1, acos 0 -> PI
            //                                                      cos range -1 -> 1, acos PI -> 0
            var cos_result = "((-0.5*H * (Y - 0.5*H))/(0.5*H * sqrt((X - 0.5*W)*(X - 0.5*W) + (Y - 0.5*H)*(Y - 0.5*H))))";
            var expr = this.Contrary ?
                        $"if(" +
                            $"lt(T,{totalDuration.TotalSeconds})," +
                            $"if(" +
                                $"lte(" +
                                    $"if(" +
                                        $"gte(X,W/2)," +
                                        $"2*PI-acos({cos_result})," +// 0 -> -PI => 2PI -> PI
                                        $"acos({cos_result}))," +// -PI -> -2PI => PI -> 0
                                    $"T*2*PI/{totalDuration.TotalSeconds})," +//0 -> 2 PI
                                $"A," +
                                $"B)," +
                            $"A)"
                            :
                        $"if(" +
                            $"lt(T,{totalDuration.TotalSeconds})," +
                            $"if(" +
                                $"lte(" +
                                    $"if(" +
                                        $"gte(X,W/2)," +
                                        $"acos({cos_result})," +// 0 -> PI
                                        $"2*PI-acos({cos_result}))," +// PI -> 0 => 2PI -  (PI -> 0) = PI -> 2PI
                                    $"T*2*PI/{totalDuration.TotalSeconds})," +//0 -> 2 PI
                                $"A," +
                                $"B)," +
                            $"A)";

            return second_imageMap
                            .BlendFilterOn(first_imageMap)
                                .Shortest(true)
                                .All_Expr(expr)
                                .MapOut;
        }
    }
}
