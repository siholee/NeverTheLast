using System;
using UnityEngine;

namespace Managers.Stage
{
    /// <summary>VisualSD 진형 시안의 ROW·rowDist·place·slotPos. 위가 0인 1920×1080 픽셀 좌표다.</summary>
    public static class StageLayout
    {
        public const float WidthPx = 1920f, HeightPx = 1080f;
        // 30px를 월드 1로 잡는다. 인원·화면비와 무관한 무대 높이다.
        public const float WorldHeight = 36f;
        public const float CenterX = 960f, FarY = 560f, NearY = 870f, BodyHeightPx = 250f;

        public readonly struct Slot
        {
            public readonly Vector2 Pixel;
            public readonly float Depth01, Scale;

            public Slot(Vector2 pixel, float depth01, float scale)
            {
                Pixel = pixel;
                Depth01 = depth01;
                Scale = scale;
            }
        }

        // 전열 공석 시 후열의 표시 column을 0으로 고르는 것은 호출부의 몫이다.
        public static Slot ForBattle(int side, int column, int index, int count)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
            if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            float t = count == 1 ? 0.5f : count == 2 ? (index == 0 ? 0.2f : 0.8f) : (float)index / (count - 1);
            return Place(side, column, t, index);
        }

        public static Slot ForPreparation(int side, int column, int row)
        {
            if (row < 0 || row > 3) throw new ArgumentOutOfRangeException(nameof(row));
            return Place(side, column, row / 3f, row);
        }

        public static float ColumnDistance(int column, float t, int i)
        {
            if (column < 0 || column > 1) throw new ArgumentOutOfRangeException(nameof(column));
            return (column == 0 ? 190f : 540f) * (0.80f + 0.25f * t) + 30f * (t - 0.5f);
        }

        public static float DepthScale(float t) => 0.84f + 0.22f * t;

        private static Slot Place(int side, int column, float t, int i)
        {
            if (side != -1 && side != 1) throw new ArgumentOutOfRangeException(nameof(side));
            return new Slot(new Vector2(CenterX + side * ColumnDistance(column, t, i),
                FarY + (NearY - FarY) * t), t, DepthScale(t));
        }

        /// <summary>기준 픽셀을 직교 카메라의 월드 원점 깊이에 투영한다.</summary>
        public static Vector3 PixelToWorld(Vector2 pixel, Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (!camera.orthographic) throw new ArgumentException("직교 카메라가 필요합니다.", nameof(camera));
            float depth = camera.WorldToViewportPoint(Vector3.zero).z;
            float viewWidthPx = HeightPx * camera.aspect;
            return camera.ViewportToWorldPoint(new Vector3(
                0.5f + (pixel.x - CenterX) / viewWidthPx, 1f - pixel.y / HeightPx, depth));
        }
    }
}
