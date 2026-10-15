using FFmpegArgs.Cores.Maps;
using FFmpegArgs.Filters;
using FFmpegArgs.Filters.VideoFilters;
using System;
using TqkLibrary.FFmpegTransition.Enums;
using TqkLibrary.FFmpegTransition.Interfaces;

namespace TqkLibrary.FFmpegTransition.Transitions
{
    public class ExpandTransition : TransitionBase
    {
        public override bool IsConcat => false;
        readonly CollapseExpandMode collapseExpandMode;
        public ExpandTransition(CollapseExpandMode collapseExpandMode)
        {
            this.collapseExpandMode = collapseExpandMode;
        }


        protected override ImageMap Make(ImageMap first_imageMap, ImageMap second_imageMap, TimeSpan totalDuration, double fps)
        {
            double TRANSITION_DURATION = totalDuration.TotalSeconds;
            ImageMap imageMap = null;
            switch (this.collapseExpandMode)
            {
                case CollapseExpandMode.Circular:
                    imageMap = second_imageMap
                        .GeqFilter()
                            .Lum("p(X,Y)")
                            .A($"if(lte(pow(sqrt(pow(T/{TRANSITION_DURATION}*W/2,2)+pow(T/{TRANSITION_DURATION}*H/2,2)),2),pow(X-(W/2),2)+pow(Y-(H/2),2)),0,{this.VisibleAlpha})").MapOut;
                    imageMap = this.OverlayOnFirst(imageMap, first_imageMap);
                    break;

                case CollapseExpandMode.Horizontal:
                case CollapseExpandMode.Vertical:
                case CollapseExpandMode.Both:
                    {
                        string expr = this.collapseExpandMode switch
                        {
                            CollapseExpandMode.Vertical => $"if(lte(Y,(H/2)-(H/2)*T/{TRANSITION_DURATION})+gte(Y,(H/2)+(H/2)*T/{TRANSITION_DURATION}),B,A)",
                            CollapseExpandMode.Horizontal => $"if(lte(X,(W/2)-(W/2)*T/{TRANSITION_DURATION})+gte(X,(W/2)+(W/2)*T/{TRANSITION_DURATION}),B,A)",
                            CollapseExpandMode.Both => $"if((lte(X,(W/2)-(W/2)*T/{TRANSITION_DURATION})+lte(Y,(H/2)-(H/2)*T/{TRANSITION_DURATION}))+(gte(X,(W/2)+(W/2)*T/{TRANSITION_DURATION})+gte(Y,(H/2)+(H/2)*T/{TRANSITION_DURATION})),B,A)",
                            _ => throw new NotFiniteNumberException(this.collapseExpandMode.ToString()),
                        };

                        imageMap = second_imageMap
                            .BlendFilterOn(first_imageMap)
                                .Shortest(true)
                                .All_Expr(expr)
                                .MapOut;
                    }
                    break;

                default:
                    throw new NotFiniteNumberException(this.collapseExpandMode.ToString());
            }

            return imageMap;
        }
    }
}
