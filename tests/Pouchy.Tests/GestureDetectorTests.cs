using Pouchy.Models;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    public class GestureDetectorTests
    {
        private const int ScreenWidth = 1920;
        private readonly AppSettings _settings = new();

        private GestureDetector CreateDetector() => new(() => _settings, 0, ScreenWidth);

        /// <summary>Feeds a left-right wiggle and returns the first gesture recognised.</summary>
        private static GestureKind Wiggle(GestureDetector detector, int amplitude, int strokes, int msPerStroke, long start = 0)
        {
            long time = start;
            for (int i = 0; i <= strokes; i++)
            {
                int x = 900 + (i % 2 == 0 ? 0 : amplitude);
                var result = detector.Process(x, 500, time);
                if (result != GestureKind.None) return result;
                time += msPerStroke;
            }
            return GestureKind.None;
        }

        [Fact]
        public void FastWiggle_IsShake()
        {
            Assert.Equal(GestureKind.Shake, Wiggle(CreateDetector(), amplitude: 60, strokes: 8, msPerStroke: 50));
        }

        [Fact]
        public void SmallMovements_AreNotShake()
        {
            Assert.Equal(GestureKind.None, Wiggle(CreateDetector(), amplitude: 10, strokes: 12, msPerStroke: 50));
        }

        [Fact]
        public void SlowWiggle_OutsideTimeWindow_IsNotShake()
        {
            Assert.Equal(GestureKind.None, Wiggle(CreateDetector(), amplitude: 60, strokes: 8, msPerStroke: 400));
        }

        [Fact]
        public void Shake_Disabled_IsIgnored()
        {
            _settings.ShakeEnabled = false;
            Assert.Equal(GestureKind.None, Wiggle(CreateDetector(), amplitude: 60, strokes: 12, msPerStroke: 50));
        }

        [Fact]
        public void ShakeMinDistance_IsRespected()
        {
            _settings.ShakeMinDistance = 80;
            Assert.Equal(GestureKind.None, Wiggle(CreateDetector(), amplitude: 60, strokes: 12, msPerStroke: 50));
        }

        [Fact]
        public void ShakeReversals_IsRespected()
        {
            _settings.ShakeReversals = 2;
            // Four strokes give three direction changes.
            Assert.Equal(GestureKind.Shake, Wiggle(CreateDetector(), amplitude: 60, strokes: 4, msPerStroke: 50));
        }

        [Fact]
        public void Reset_ClearsShakeHistory()
        {
            var detector = CreateDetector();
            Wiggle(detector, amplitude: 60, strokes: 4, msPerStroke: 50);
            detector.Reset();
            Assert.False(detector.HasState);
            Assert.Equal(GestureKind.None, Wiggle(detector, amplitude: 60, strokes: 3, msPerStroke: 50, start: 300));
        }

        [Fact]
        public void TwoQuickBumpsOnSameEdge_IsEdgeBump()
        {
            var detector = CreateDetector();
            Assert.Equal(GestureKind.None, detector.Process(ScreenWidth - 1, 500, 0));
            Assert.Equal(GestureKind.None, detector.Process(ScreenWidth - 200, 500, 100));
            Assert.Equal(GestureKind.EdgeBump, detector.Process(ScreenWidth - 1, 500, 200));
        }

        [Fact]
        public void RestingOnEdge_CountsAsOneBump()
        {
            var detector = CreateDetector();
            for (int t = 0; t < 500; t += 50)
            {
                Assert.Equal(GestureKind.None, detector.Process(0, 300 + t, t));
            }
        }

        [Fact]
        public void SlowBumps_AreNotEdgeBump()
        {
            var detector = CreateDetector();
            detector.Process(0, 500, 0);
            detector.Process(300, 500, 500);
            Assert.Equal(GestureKind.None, detector.Process(0, 500, 1600));
        }

        [Fact]
        public void BumpsOnOppositeEdges_AreNotEdgeBump()
        {
            var detector = CreateDetector();
            detector.Process(0, 500, 0);
            detector.Process(900, 500, 100);
            Assert.Equal(GestureKind.None, detector.Process(ScreenWidth - 1, 500, 200));
        }

        [Fact]
        public void EdgeBump_Disabled_IsIgnored()
        {
            _settings.EdgeBumpEnabled = false;
            var detector = CreateDetector();
            detector.Process(0, 500, 0);
            detector.Process(300, 500, 100);
            Assert.Equal(GestureKind.None, detector.Process(0, 500, 200));
        }

        [Fact]
        public void EdgeBump_UsesVirtualScreenOffset()
        {
            // Secondary monitor to the left of the primary: virtual screen starts at -1920.
            var detector = new GestureDetector(() => _settings, -1920, 3840);
            detector.Process(-1920, 500, 0);
            detector.Process(-1500, 500, 100);
            Assert.Equal(GestureKind.EdgeBump, detector.Process(-1920, 500, 200));
        }
    }
}
