using Managers.UI.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Managers.UI.Theme
{
    /// <summary>전 화면의 문서 머리. 본문은 밝게, 제목 띠만 남색으로 시선을 고정한다.</summary>
    public static class UIArtDirection
    {
        public static void Header(Transform parent, float height = 88f)
        {
            Image band = UIBuild.Panel("NavigationInk", parent, UITheme.AccentDim, UIShapes.Corner.BottomRight, 12);
            band.raycastTarget = false;
            UIBuild.Anchor(band.rectTransform, new Vector2(0f, 1f), Vector2.one);
            band.rectTransform.offsetMin = new Vector2(0f, -height);
            band.rectTransform.offsetMax = Vector2.zero;
            Image line = UIBuild.Solid("NavigationCyan", band.transform, UITheme.AccentCyan);
            line.raycastTarget = false;
            UIBuild.Anchor(line.rectTransform, Vector2.zero, new Vector2(0.25f, 0f));
            line.rectTransform.sizeDelta = new Vector2(0f, 3f);
            Image stripes = UIBuild.Solid("NavigationStripes", band.transform, Color.white);
            stripes.sprite = UIShapes.DiagonalStripes(20, new Color(1f, 1f, 1f, 0.07f), Color.clear, 0.12f);
            stripes.type = Image.Type.Tiled; stripes.raycastTarget = false;
            UIBuild.Anchor(stripes.rectTransform, new Vector2(0.78f, 0f), Vector2.one);
        }

    }
}
