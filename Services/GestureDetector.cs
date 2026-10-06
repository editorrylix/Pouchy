using Pouchy.Models;

namespace Pouchy.Services
{
    public enum GestureKind
    {
        None,
        Shake,
        EdgeBump,
    }

    /// <summary>
    /// Pure gesture recognition over a stream of cursor positions, fed while the
    /// left button is held. No Win32 calls, so it can be unit-tested.
    /// </summary>
    public sealed class GestureDetector
    {
        private const int EdgeTolerancePx = 2;
        private const int SameEdgeDistancePx = 100;

        private readonly Func<AppSettings> _settings;
        private readonly List<(int X, long TimeMs)> _history = new();

        private int _screenLeft;
        private int _screenRight; // exclusive

        private bool _atEdge;
        private int _bumpCount;
        private long _lastBumpTime;
        private int? _lastBumpX;

        public GestureDetector(Func<AppSettings> settings, int screenLeft, int screenWidth)
        {
            _settings = settings;
            SetScreenBounds(screenLeft, screenWidth);
        }

        public void SetScreenBounds(int left, int width)
        {
            _screenLeft = left;
            _screenRight = left + Math.Max(width, 1);
        }

        public bool HasState => _history.Count > 0 || _bumpCount > 0 || _atEdge;

        public void Reset()
        {
            _history.Clear();
            _atEdge = false;
            _bumpCount = 0;
        }

        public GestureKind Process(int x, int y, long nowMs)
        {
            var s = _settings();

            if (s.EdgeBumpEnabled && DetectEdgeBump(x, nowMs, s))
            {
                Reset();
                return GestureKind.EdgeBump;
            }

            if (s.ShakeEnabled && DetectShake(x, nowMs, s))
            {
                Reset();
                return GestureKind.Shake;
            }

            return GestureKind.None;
        }

        private bool DetectEdgeBump(int x, long now, AppSettings s)
        {
            bool isAtEdge = x <= _screenLeft + EdgeTolerancePx - 1 || x >= _screenRight - EdgeTolerancePx;
            if (!isAtEdge)
            {
                _atEdge = false;
                return false;
            }
            if (_atEdge) return false; // Still resting on the edge from the last bump.

            _atEdge = true;
            bool timedOut = now - _lastBumpTime > s.EdgeBumpWindowMs;
            bool otherEdge = _lastBumpX is int lastX && Math.Abs(lastX - x) > SameEdgeDistancePx;
            if (timedOut || otherEdge) _bumpCount = 0;

            _bumpCount++;
            _lastBumpTime = now;
            _lastBumpX = x;
            return _bumpCount >= s.EdgeBumpCount;
        }

        private bool DetectShake(int x, long now, AppSettings s)
        {
            // History is in time order, so expired points are all at the front.
            int expired = 0;
            while (expired < _history.Count && now - _history[expired].TimeMs > s.ShakeWindowMs)
            {
                expired++;
            }
            if (expired > 0) _history.RemoveRange(0, expired);
            _history.Add((x, now));

            if (_history.Count < s.ShakeReversals + 2) return false;

            int reversals = 0;
            int direction = 0;
            int anchorX = _history[0].X;
            for (int i = 1; i < _history.Count; i++)
            {
                int dx = _history[i].X - anchorX;
                if (Math.Abs(dx) < s.ShakeMinDistance) continue;

                int newDirection = Math.Sign(dx);
                if (direction != 0 && newDirection != direction) reversals++;
                direction = newDirection;
                anchorX = _history[i].X;
            }
            return reversals >= s.ShakeReversals;
        }
    }
}
