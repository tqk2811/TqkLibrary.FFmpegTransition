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
            string[] keepColor = { "p(X,Y)", "p(X,Y)", "p(X,Y)" };
            byte[] raw = RenderRgba(transition, keepColor, alpha, keepColor, alpha, name);
            return raw.Where((_, i) => i % 4 == 3).ToArray();
        }

        /// <summary>
        /// Renders the transition (1 s, 24 fps) between two 64x64 yuva420p layers whose Y/Cb/Cr geq expressions and alpha are given
        /// (first layer, then second) and returns the raw rgba bytes of every output frame.
        /// </summary>
        static byte[] RenderRgba(TransitionBase transition, string[] firstYuv, int firstAlpha, string[] secondYuv, int secondAlpha, string name)
        {
            TimeSpan totalDuration = TimeSpan.FromSeconds(1);
            TimeSpan stepDuration = transition.IsConcat ? totalDuration / 2 : totalDuration;
            FFmpegArg ffmpegArg = new FFmpegArg().OverWriteOutput();
            // A 64x64 clip with the given colour and alpha (FFmpegArg needs a real input; a color source alone is refused).
            ImageMap Layer(string file, string[] yuv, int alpha) => ffmpegArg
                .AddImagesInput(new ImageFileInput(file).Duration(stepDuration))
                .First()
                .FpsFilter().Fps(24).MapOut
                .ScaleFilter().W("64").H("64").MapOut
                .FormatFilter(PixFmt.yuva420p).MapOut
                .GeqFilter().Lum(yuv[0]).Cb(yuv[1]).Cr(yuv[2]).A($"{alpha}").MapOut;

            ImageMap result = transition.MakeTransition(
                Layer(".\\Resources\\Acro Trip ED.mp4", firstYuv, firstAlpha),
                Layer(".\\Resources\\Mayonaka Punch OP.mp4", secondYuv, secondAlpha),
                totalDuration, 24);
            string path = Path.Combine(Directory.GetCurrentDirectory(), $"{name}.rgba");
            ffmpegArg.AddOutput(new ImageFileOutput(path, result.FormatFilter(PixFmt.rgba).MapOut).AndSet(x => x.Format(MuxingFileFormat.rawvideo)));
            ffmpegArg.Render(new FFmpegRenderConfig()).Execute().EnsureSuccess();

            byte[] raw = File.ReadAllBytes(path);
            Assert.IsTrue(raw.Length >= 64 * 64 * 4 * 12, $"{name}: {raw.Length} bytes");
            return raw;
        }

        /// <summary>rgba of the centre pixel of the given frame.</summary>
        static byte[] CentrePixel(byte[] raw, int frame)
        {
            int offset = frame * 64 * 64 * 4 + (32 * 64 + 32) * 4;
            return raw.Skip(offset).Take(4).ToArray();
        }

        static void AssertNear(int expected, int actual, int tolerance, string what)
            => Assert.IsTrue(Math.Abs(expected - actual) <= tolerance, $"{what}: expected {expected} +-{tolerance}, got {actual}");

        static readonly string[] Black = { "0", "128", "128" };
        static readonly string[] Red = { "81", "90", "240" };
        static readonly string[] DarkGrey = { "60", "128", "128" };
        static readonly string[] LightGrey = { "200", "128", "128" };

        [TestMethod]
        [DataRow(PixFmt.yuva420p)]
        [DataRow(PixFmt.rgba)]
        public void KeepAlpha_CrossFadeOverTransparent_KeepsLayerColour(PixFmt pixFmt)
        {
            var transition = new CrossFadeTransition { PixelFormat = pixFmt, KeepAlpha = true };
            // First layer fully transparent (black colour, alpha 0), second an opaque solid red.
            byte[] raw = RenderRgba(transition, Black, 0, Red, 255, $"CrossFade.overTransparent.{pixFmt}");

            byte[] red = CentrePixel(raw, 23);
            byte[] mid = CentrePixel(raw, 12);
            Assert.IsTrue(red[0] > 200 && red[1] < 50 && red[2] < 50, $"end frame {string.Join(",", red)}");
            AssertNear(128, mid[3], 8, "alpha at mid-transition");
            for (int c = 0; c < 3; c++)
                AssertNear(red[c], mid[c], 10, $"channel {c} at mid-transition");
        }

        [TestMethod]
        [DataRow(PixFmt.yuva420p)]
        [DataRow(PixFmt.rgba)]
        public void KeepAlpha_CrossFadeOpaqueLayers_MixesColourAndStaysOpaque(PixFmt pixFmt)
        {
            var transition = new CrossFadeTransition { PixelFormat = pixFmt, KeepAlpha = true };
            byte[] raw = RenderRgba(transition, DarkGrey, 255, LightGrey, 255, $"CrossFade.opaque.{pixFmt}");

            byte[] first = CentrePixel(raw, 0);
            byte[] second = CentrePixel(raw, 23);
            byte[] mid = CentrePixel(raw, 12);
            Assert.IsTrue(second[0] - first[0] > 100, $"first {first[0]}, second {second[0]}");
            AssertNear(255, mid[3], 2, "alpha at mid-transition");
            // Frame 0 is p=0 and frame 23 is p=23/24, so the midpoint of the two ends sits ~1/48 off the true p=0.5 mix.
            for (int c = 0; c < 3; c++)
                AssertNear((first[c] + second[c]) / 2, mid[c], 8, $"channel {c} at mid-transition");
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