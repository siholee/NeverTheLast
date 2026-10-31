using System;

namespace Combat
{
    /// <summary>전투 판정 난수. 기본 게임은 Unity 난수를 유지하고, 시드 세션은 호스트와 무관하게 같은 수열을 쓴다.</summary>
    public static class CombatRandom
    {
        private static bool _seeded;
        private static uint _state;
        public static uint SeedState => _state;
        public static IDisposable BeginSeed(int seed)
        {
            var scope = new SeedScope(_seeded, _state);
            _state = unchecked((uint)seed) ^ 0x9E3779B9u;
            if (_state == 0) _state = 0x6D2B79F5u;
            _seeded = true;
            return scope;
        }

        private static uint Next()
        {
            uint x = _state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            return _state = x;
        }

        public static float value => _seeded ? (Next() >> 8) * (1f / 16777216f) : UnityEngine.Random.value;
        public static int Range(int min, int max)
        {
            if (!_seeded) return UnityEngine.Random.Range(min, max);
            if (max <= min) return min;
            uint width = unchecked((uint)((long)max - min));
            uint threshold = unchecked(0u - width) % width;
            uint sample;
            do { sample = Next(); } while (sample < threshold);
            return (int)((long)min + sample % width);
        }
        public static float Range(float min, float max) => _seeded ? min + (max - min) * value : UnityEngine.Random.Range(min, max);

        private sealed class SeedScope : IDisposable
        {
            private readonly bool _previousSeeded;
            private readonly uint _previousState;
            private bool _disposed;
            public SeedScope(bool seeded, uint state) { _previousSeeded = seeded; _previousState = state; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true; _seeded = _previousSeeded; _state = _previousState;
            }
        }
    }
}
