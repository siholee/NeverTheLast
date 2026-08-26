using System.IO;
using Managers.UI.Theme;
using UnityEditor;
using UnityEngine;

namespace Tools.Editor
{
    /// <summary>
    /// <see cref="StatusIcons"/>가 그리는 아이콘 전부를 한 장의 PNG로 뽑는다.
    ///
    /// 아이콘은 코드로 그리기 때문에 플레이 중에 실제로 그 상태가 걸려야만 눈으로 볼 수 있다.
    /// 선 굵기나 모양을 다듬을 때마다 전투를 돌릴 수는 없으므로, 메뉴 한 번으로
    /// 전 조합(그림 × 방향)을 한눈에 늘어놓은 대조표를 만든다.
    ///
    /// 결과는 프로젝트 밖(<c>{프로젝트}/Temp/status_icons.png</c>)에 떨어뜨린다 —
    /// Assets 안에 두면 스프라이트로 임포트되어 관리 대상이 늘어난다.
    /// </summary>
    public static class StatusIconSheetExporter
    {
        private const int Cell = 48;
        private const int Pad = 8;

        [MenuItem("Tools/Art/상태 아이콘 시트 내보내기")]
        public static void Export()
        {
            var glyphs = (StatusGlyph[])System.Enum.GetValues(typeof(StatusGlyph));
            var arrows = (StatusArrow[])System.Enum.GetValues(typeof(StatusArrow));

            int width = glyphs.Length * (Cell + Pad) + Pad;
            int height = arrows.Length * (Cell + Pad) + Pad;

            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var background = new Color[width * height];
            for (int i = 0; i < background.Length; i++)
            {
                background[i] = new Color(0.086f, 0.090f, 0.098f, 1f);
            }

            sheet.SetPixels(background);

            for (int row = 0; row < arrows.Length; row++)
            {
                for (int col = 0; col < glyphs.Length; col++)
                {
                    Sprite sprite = StatusIcons.Get(glyphs[col], arrows[row]);
                    Blit(sheet, sprite.texture,
                        Pad + col * (Cell + Pad),
                        height - Pad - (row + 1) * Cell - row * Pad);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "status_icons.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            Debug.Log($"[아이콘] 상태 아이콘 시트를 저장했습니다: {path}");
        }

        /// <summary>아이콘 한 장을 시트에 얹는다. 원본보다 칸이 크면 최근접으로 늘린다.</summary>
        private static void Blit(Texture2D sheet, Texture source, int x, int y)
        {
            var readable = (Texture2D)source;
            for (int py = 0; py < Cell; py++)
            {
                for (int px = 0; px < Cell; px++)
                {
                    int sx = px * readable.width / Cell;
                    int sy = py * readable.height / Cell;
                    Color pixel = readable.GetPixel(sx, sy);

                    Color under = sheet.GetPixel(x + px, y + py);
                    sheet.SetPixel(x + px, y + py, Color.Lerp(under, Color.white, pixel.a));
                }
            }
        }
    }
}
