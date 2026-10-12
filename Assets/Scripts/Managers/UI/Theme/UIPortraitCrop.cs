using System.Collections.Generic;
using UnityEngine;

namespace Managers.UI.Theme
{
    /// <summary>작은 HUD에는 상반신 원본 대신 얼굴 중심 크롭을 사용한다. 원본과 GUID는 보존한다.</summary>
    public static class UIPortraitCrop
    {
        private static readonly Dictionary<int, Sprite> Faces = new();
        public static Sprite Face(Sprite source)
        {
            if (source == null) return null;
            int id = source.GetInstanceID();
            if (Faces.TryGetValue(id, out Sprite cached) && cached != null) return cached;
            Rect r = source.rect;
            // 초상 자산의 머리와 어깨가 있는 상단 중앙. 작은 이름표까지 원화가 침범하지 않는다.
            Rect crop = new(r.x + r.width * .24f, r.y + r.height * (source.name.Contains("CHANDRA") ? .29f : .43f), r.width * .52f, r.height * .52f);
            Sprite face = Sprite.Create(source.texture, crop, new Vector2(.5f,.5f), source.pixelsPerUnit,
                0, SpriteMeshType.FullRect);
            face.name = source.name + ".HUDFace";
            face.hideFlags = HideFlags.HideAndDontSave;
            Faces[id] = face;
            return face;
        }
    }
}
