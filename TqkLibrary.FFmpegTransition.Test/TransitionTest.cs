using FFmpegArgs.Cores.Enums;
using FFmpegArgs.Filters.VideoFilters;
using FFmpegArgs.Filters.VideoSources;
using System.Collections.Generic;
using System.IO;
using TqkLibrary.FFmpegTransition.Enums;
using TqkLibrary.FFmpegTransition.Interfaces;
using TqkLibrary.FFmpegTransition.Transitions;

namespace TqkLibrary.FFmpegTransition.Test
{
    [TestClass]
    public class TransitionTest
    {
        public TransitionTest()
        {
        }

        void Render(ITransition translation, params string[] filenames)
        {
            var names = new List<string>()
            {
                translation.GetType().Name,
            };
            names.AddRange(filenames);

            TimeSpan totalDuration = TimeSpan.FromSeconds(2);
            TimeSpan stepDuration = translation.IsConcat ? totalDuration / 2 : totalDuration;

            FFmpegArg ffmpegArg = new FFmpegArg().OverWriteOutput();

            ImageMap first_map = ffmpegArg
                .AddImagesInput(new ImageFileInput(".\\Resources\\Acro Trip ED.mp4").Duration(stepDuration))
                .First()
                .FpsFilter()
                    .Fps(24).MapOut;

            ImageMap second_map = ffmpegArg
                .AddImagesInput(new ImageFileInput(".\\Resources\\Mayonaka Punch OP.mp4").Duration(stepDuration))
                .First()
                .FpsFilter()
                    .Fps(24).MapOut;

            ImageMap imageMap = translation.MakeTransition(first_map, second_map, totalDuration, 24);
            imageMap = imageMap.TrimFilter().Duration(totalDuration).MapOut;

            ffmpegArg.AddOutput(new ImageFileOutput($"{string.Join(".", names)}.mp4", imageMap).AndSet(x => x.ImageStreams.First().Fps(24)));

            string workingDir = Directory.GetCurrentDirectory();
            ffmpegArg.Render(x => x.WithWorkingDirectory(workingDir)).Execute().EnsureSuccess();
        }

        [TestMethod]
        public void FadeInTwoTest()
        {
            Render(new FadeInTwoTransition());
        }

        [TestMethod]
        public void CrossFadeTest()
        {
            Render(new CrossFadeTransition());
        }

        [TestMethod]
        public void ExpandCircularTest()
        {
            Render(new ExpandTransition(CollapseExpandMode.Circular), nameof(CollapseExpandMode.Circular));
        }
        [TestMethod]
        public void ExpandVerticalTest()
        {
            Render(new ExpandTransition(CollapseExpandMode.Vertical), nameof(CollapseExpandMode.Vertical));
        }
        [TestMethod]
        public void ExpandHorizontalTest()
        {
            Render(new ExpandTransition(CollapseExpandMode.Horizontal), nameof(CollapseExpandMode.Horizontal));
        }
        [TestMethod]
        public void ExpandBothTest()
        {
            Render(new ExpandTransition(CollapseExpandMode.Both), nameof(CollapseExpandMode.Both));
        }

        [TestMethod]
        public void CollapseCircularTest()
        {
            Render(new CollapseTransition(CollapseExpandMode.Circular), nameof(CollapseExpandMode.Circular));
        }
        [TestMethod]
        public void CollapseVerticalTest()
        {
            Render(new CollapseTransition(CollapseExpandMode.Vertical), nameof(CollapseExpandMode.Vertical));
        }
        [TestMethod]
        public void CollapseHorizontalTest()
        {
            Render(new CollapseTransition(CollapseExpandMode.Horizontal), nameof(CollapseExpandMode.Horizontal));
        }
        [TestMethod]
        public void CollapseBothTest()
        {
            Render(new CollapseTransition(CollapseExpandMode.Both), nameof(CollapseExpandMode.Both));
        }

        static IEnumerable<object[]> AllTransitions()
        {
            yield return new object[] { new CrossFadeTransition(), "" };
            yield return new object[] { new FadeInTwoTransition(), "" };
            yield return new object[] { new ClockTransition(), "" };
            foreach (CollapseExpandMode mode in Enum.GetValues<CollapseExpandMode>())
            {
                yield return new object[] { new CollapseTransition(mode), mode.ToString() };
                yield return new object[] { new ExpandTransition(mode), mode.ToString() };
            }
        }

        [TestMethod]
        [DynamicData(nameof(AllTransitions), DynamicDataSourceType.Method)]
        public void KeepAlpha_RendersInAlphaPixelFormat(TransitionBase transition, string mode)
        {
            transition.PixelFormat = PixFmt.yuva420p;
            transition.KeepAlpha = true;
            Render(transition, mode, "KeepAlpha");
        }

        /// <summary>Renders the transition between two solid 64x64 layers of the given alpha and returns the alpha byte of every output pixel.</summary>
        static byte[] RenderAlpha(TransitionBase transition, int alpha, string name)
        {
            TimeSpan totalDuration = TimeSpan.FromSeconds(1);
            TimeSpan stepDuration = transition.IsConcat ? totalDuration / 2 : totalDuration;
            FFmpegArg ffmpegArg = new FFmpegArg().OverWriteOutput();
            // A 64x64 clip whose alpha is forced to the given value (FFmpegArg needs a real input; a color source alone is refused).
            ImageMap Layer(string file) => ffmpegArg
                .AddImagesInput(new ImageFileInput(file).Duration(stepDuration))
                .First()
                .FpsFilter().Fps(24).MapOut
                .ScaleFilter().W("64").H("64").MapOut
                .FormatFilter(PixFmt.yuva420p).MapOut
                .GeqFilter().Lum("p(X,Y)").Cb("p(X,Y)").Cr("p(X,Y)").A($"{alpha}").MapOut;

            ImageMap result = transition.MakeTransition(Layer(".\\Resources\\Acro Trip ED.mp4"), Layer(".\\Resources\\Mayonaka Punch OP.mp4"), totalDuration, 24);
            string path = Path.Combine(Directory.GetCurrentDirectory(), $"{name}.rgba");
            ffmpegArg.AddOutput(new ImageFileOutput(path, result.FormatFilter(PixFmt.rgba).MapOut).AndSet(x => x.Format(MuxingFileFormat.rawvideo)));
            ffmpegArg.Render(new FFmpegRenderConfig()).Execute().EnsureSuccess();

            byte[] raw = File.ReadAllBytes(path);
            Assert.IsTrue(raw.Length >= 64 * 64 * 4 * 12, $"{name}: {raw.Length} bytes");
            return raw.Where((_, i) => i % 4 == 3).ToArray();
        }

        [TestMethod]
        [DynamicData(nameof(AllTransitions), DynamicDataSourceType.Method)]
        public void KeepAlpha_TransparentLayersStayTransparent(TransitionBase transition, string mode)
        {
            transition.PixelFormat = PixFmt.yuva420p;
            transition.KeepAlpha = true;

            byte[] transparent = RenderAlpha(transition, 0, $"{transition.GetType().Name}{mode}.transparent");
            byte[] opaque = RenderAlpha(transition, 255, $"{transition.GetType().Name}{mode}.opaque");

            Assert.IsTrue(transparent.All(x => x < 8), $"max alpha {transparent.Max()}");
            // FadeInTwo fades through transparent on purpose; every other transition shows an opaque layer at every instant.
            if (!transition.IsConcat)
                Assert.IsTrue(opaque.All(x => x > 247), $"min alpha {opaque.Min()}");
        }

        [TestMethod]
        public void PixelFormat_ConvertsInputsAndOutput()
        {
            FFmpegArg ffmpegArg = new FFmpegArg();
            ImageMap first = ffmpegArg.AddImagesInput(new ImageFileInput("a.mp4")).First();
            ImageMap second = ffmpegArg.AddImagesInput(new ImageFileInput("b.mp4")).First();
            ImageMap result = new ClockTransition { PixelFormat = PixFmt.yuva420p }.MakeTransition(first, second, TimeSpan.FromSeconds(1), 24);
            ffmpegArg.AddOutput(new ImageFileOutput("out.mp4", result));

            string args = string.Join(" ", ffmpegArg.Render(new FFmpegRenderConfig()).ArgumentsList);

            Assert.AreEqual(3, System.Text.RegularExpressions.Regex.Matches(args, "format=pix_fmts=yuva420p|format=yuva420p").Count, args);
        }

        [TestMethod]
        public void Defaults_KeepTheLegacyCrossFadeOutput()
        {
            FFmpegArg ffmpegArg = new FFmpegArg();
            ImageMap first = ffmpegArg.AddImagesInput(new ImageFileInput("a.mp4")).First();
            ImageMap second = ffmpegArg.AddImagesInput(new ImageFileInput("b.mp4")).First();
            ImageMap result = new CrossFadeTransition().MakeTransition(first, second, TimeSpan.FromSeconds(1), 24);
            ffmpegArg.AddOutput(new ImageFileOutput("out.mp4", result));

            string args = string.Join(" ", ffmpegArg.Render(new FFmpegRenderConfig()).ArgumentsList);

            StringAssert.Contains(args, "format=yuv420");
            Assert.IsFalse(args.Contains("yuva420p"), args);
        }

        [TestMethod]
        public void ClockTest()
        {
            Render(new ClockTransition());
        }
        [TestMethod]
        public void ClockContraryTest()
        {
            Render(new ClockTransition() { Contrary = true }, nameof(ClockTransition.Contrary));
        }
    }
}