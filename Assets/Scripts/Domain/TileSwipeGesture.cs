using System;

namespace Pyatnashki.Domain
{
    /// <summary>One pointer gesture, one legal move. Deltas use board-local coordinates.</summary>
    public sealed class TileSwipeGesture
    {
        private int dx, dy, version;
        private double threshold, maximumDistance;
        private bool active, consumed;

        public bool Begin(int source, int empty, double tileSize, double fraction, int boardVersion)
        {
            Cancel();
            if (source < 0 || source >= 16 || empty < 0 || empty >= 16)
                throw new ArgumentOutOfRangeException(nameof(source));
            if (tileSize <= 0 || double.IsNaN(tileSize) || double.IsInfinity(tileSize))
                throw new ArgumentOutOfRangeException(nameof(tileSize));
            if (fraction <= 0 || fraction > 1 || double.IsNaN(fraction))
                throw new ArgumentOutOfRangeException(nameof(fraction));
            dx = empty % 4 - source % 4;
            dy = source / 4 - empty / 4;
            if (Math.Abs(dx) + Math.Abs(dy) != 1) return false;
            threshold = tileSize * fraction;
            version = boardVersion;
            maximumDistance = 0;
            active = true;
            consumed = false;
            return true;
        }

        public bool TryDrag(double x, double y, int boardVersion)
        {
            if (!active || consumed) return false;
            if (boardVersion != version) { Cancel(); return false; }
            maximumDistance = Math.Max(maximumDistance, Math.Sqrt(x * x + y * y));
            if (maximumDistance < threshold) return false;
            consumed = true;
            double parallel = x * dx + y * dy;
            double perpendicular = dx == 0 ? x : y;
            return parallel >= threshold && parallel > Math.Abs(perpendicular);
        }

        public bool TryRelease(double x, double y, int boardVersion)
        {
            bool swipe = TryDrag(x, y, boardVersion);
            bool tap = active && !consumed && boardVersion == version && maximumDistance < threshold;
            Cancel();
            return swipe || tap;
        }

        public void Cancel() { active = false; consumed = true; }
    }
}
