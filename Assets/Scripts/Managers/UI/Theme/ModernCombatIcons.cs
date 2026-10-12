using System.Collections.Generic;
using BaseClasses;
using UnityEngine;

namespace Managers.UI.Theme
{
    /// <summary>미니멀 고딕 전투 아이콘. 첨두형 실루엣·얇은 각진 획·균일 여백.</summary>
    public static class ModernCombatIcons
    {
        private static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Element(BaseEnums.UnitElement element) => Build("element_" + element, p =>
        {
            switch(element)
            {
                case BaseEnums.UnitElement.Pyro:
                    Flame(p); break;
                case BaseEnums.UnitElement.Hydro:
                    Drop(p); break;
                case BaseEnums.UnitElement.Dendro:
                    Leaf(p); break;
                case BaseEnums.UnitElement.Anemo:
                    Wind(p); break;
                case BaseEnums.UnitElement.Electro:
                    Bolt(p); break;
                case BaseEnums.UnitElement.Cryo:
                    Snow(p); break;
                case BaseEnums.UnitElement.Geo:
                    p.Path(16,28,26,16,16,4,6,16,16,28); p.Path(16,28,16,4); break;
                case BaseEnums.UnitElement.Void:
                    p.Path(16,28,24,20,24,12,16,4,8,12,8,20,16,28); p.Diamond(16,16,4); break;
                default:
                    Spark(p); break;
            }
        });

        public static Sprite Status(StatusGlyph glyph, StatusArrow arrow = StatusArrow.None) => Build("status_" + glyph + "_" + arrow, p =>
        {
            switch(glyph)
            {
                case StatusGlyph.Buff: p.Path(7,14,16,23,25,14); p.Path(16,23,16,5); break;
                case StatusGlyph.Debuff: p.Path(7,18,16,9,25,18); p.Path(16,9,16,27); break;
                case StatusGlyph.Attack: p.Path(16,28,19,23,19,12,16,9,13,12,13,23,16,28); p.Path(9,10,23,10); p.Path(16,10,16,4); break;
                case StatusGlyph.Defense: Shield(p); p.Path(16,23,16,10); break;
                case StatusGlyph.Shield: Shield(p); break;
                case StatusGlyph.Guard: Shield(p); p.Path(10,16,14,12,22,21); break;
                case StatusGlyph.Speed: p.Path(6,7,15,16,6,25); p.Path(17,7,26,16,17,25); break;
                case StatusGlyph.Crit: Spark(p); break;
                case StatusGlyph.Focus:
                    p.Diamond(16,16,8); p.Path(16,28,16,21); p.Path(16,11,16,4); p.Path(4,16,11,16); p.Path(21,16,28,16); break;
                case StatusGlyph.Heal: p.Path(16,5,16,27); p.Path(5,16,27,16); break;
                case StatusGlyph.Poison:
                    p.Path(12,27,20,27); p.Path(13,27,13,20,6,8,8,5,24,5,26,8,19,20,19,27); p.Diamond(16,12,2); break;
                case StatusGlyph.Burn: Flame(p); break;
                case StatusGlyph.Bleed: Drop(p); break;
                case StatusGlyph.Shock: Bolt(p); break;
                case StatusGlyph.Weathering: Wind(p); break;
                case StatusGlyph.Freeze: Snow(p); break;
                case StatusGlyph.Stun:
                    p.Path(7,9,5,23,16,28,27,21,27,10,17,5,11,9,11,19,18,21,22,17,20,12,16,12); break;
                case StatusGlyph.Airborne: p.Path(7,17,16,26,25,17); p.Path(16,26,16,9); p.Path(7,5,25,5); break;
                case StatusGlyph.Silence:
                    p.Path(8,7,8,20,16,28,24,20,24,7,8,7); p.Path(7,25,25,7); break;
                case StatusGlyph.Root:
                    p.Path(14,20,14,26,10,29,4,25,4,19,9,15,14,20); p.Path(18,12,18,6,22,3,28,7,28,13,23,17,18,12); p.Path(11,21,21,11); break;
                case StatusGlyph.Taunt:
                    p.Path(16,28,26,20,26,11,16,4,6,11,6,20,16,28); p.Path(16,22,16,15); p.Diamond(16,10,.7f); break;
                case StatusGlyph.Thorns:
                    p.Path(6,8,25,25); p.Path(10,12,8,20,16,17); p.Path(18,19,17,27,25,25); break;
                case StatusGlyph.Mark:
                    p.Diamond(16,19,8); p.Path(16,11,16,4); p.Path(4,19,8,19); p.Path(24,19,28,19); break;
            }
            if(arrow != StatusArrow.None)
            {
                // 합성 아이콘 호출도 별도 방향 표식과 같은 선형 언어를 사용한다.
                float sign = arrow == StatusArrow.Up ? 1 : -1;
                p.Path(23,26-sign*2,26,26+sign*2,29,26-sign*2);
            }
        });

        public static Sprite Arrow(StatusArrow arrow) => arrow == StatusArrow.None ? null : Build("arrow_" + arrow, p =>
        {
            if(arrow == StatusArrow.Up) { p.Path(7,14,16,23,25,14); p.Path(16,23,16,7); }
            else { p.Path(7,18,16,9,25,18); p.Path(16,9,16,25); }
        });

        public static Sprite Overflow => Build("overflow", p => { p.Diamond(7,16,.8f); p.Diamond(16,16,.8f); p.Diamond(25,16,.8f); });

        public static Sprite AllElements => Build("all_elements", p =>
        {
            p.Diamond(16,16,4);
            for(int i=0;i<7;i++) { float angle=i*Mathf.PI*2/7; var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)); p.Path(16+direction.x*8,16+direction.y*8,16+direction.x*12,16+direction.y*12); }
        });

        private static void Shield(Paths p) => p.Path(16,28,26,24,26,14,16,4,6,14,6,24,16,28);
        private static void Spark(Paths p) => p.Path(16,29,18,19,25,16,18,13,16,3,14,13,7,16,14,19,16,29);
        private static void Bolt(Paths p) => p.Path(19,28,7,14,15,14,12,4,25,19,17,19,19,28);
        private static void Flame(Paths p)
        {
            p.Path(16,28,9,18,7,11,11,5,21,5,25,12,21,21,20,15,16,28);
        }
        private static void Drop(Paths p)
        {
            p.Path(16,28,24,16,25,11,22,5,16,4,10,5,7,11,8,16,16,28);
        }
        private static void Leaf(Paths p)
        {
            p.Path(5,6,7,18,17,27,27,28,26,17,16,7,5,6); p.Path(5,6,23,24);
        }
        private static void Wind(Paths p)
        {
            p.Path(5,23,23,23,27,27); p.Path(5,16,27,16,23,11); p.Path(5,9,16,9);
        }
        private static void Snow(Paths p)
        {
            p.Path(16,4,16,28); p.Path(6,10,26,22); p.Path(6,22,26,10);
            p.Path(12,25,16,21,20,25); p.Path(12,7,16,11,20,7);
        }

        // 32단위 좌표/1.6단위 각진 획. 사각 끝과 절제된 첨두형을 유지한다.
        private static Sprite Build(string key, System.Action<Paths> draw)
        {
            if(Cache.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
            var paths = new Paths(); draw(paths);
            const int size = 128; const float scale = size / 32f;
            var pixels = new Color[size * size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                var p = new Vector2((x+.5f)/scale,(y+.5f)/scale); float distance = float.MaxValue;
                foreach(var path in paths.Items) for(int i=1;i<path.Length;i++)
                {
                    Vector2 a=path[i-1], b=path[i], delta=b-a;
                    float length=delta.magnitude;
                    if(length < .001f) continue;
                    Vector2 axis=delta/length, center=(a+b)*.5f, offset=p-center;
                    float along=Mathf.Abs(Vector2.Dot(offset,axis))-length*.5f-.8f;
                    float across=Mathf.Abs(offset.x*axis.y-offset.y*axis.x)-.8f;
                    float sdf=Mathf.Min(Mathf.Max(along,across),0)+new Vector2(Mathf.Max(along,0),Mathf.Max(across,0)).magnitude;
                    distance=Mathf.Min(distance,sdf);
                }
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(.5f-distance*scale));
            }
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false) { name="GothicCombat_"+key, hideFlags=HideFlags.HideAndDontSave, filterMode=FilterMode.Bilinear, wrapMode=TextureWrapMode.Clamp };
            texture.SetPixels(pixels); texture.Apply();
            sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            sprite.name=texture.name; sprite.hideFlags=HideFlags.HideAndDontSave; Cache[key]=sprite; return sprite;
        }

        private sealed class Paths
        {
            public readonly List<Vector2[]> Items = new();
            public void Path(params float[] xy)
            {
                var path=new Vector2[xy.Length/2]; for(int i=0;i<path.Length;i++) path[i]=new Vector2(xy[i*2],xy[i*2+1]); Items.Add(path);
            }
            public void Diamond(float x,float y,float radius) => Path(x,y+radius,x+radius,y,x,y-radius,x-radius,y,x,y+radius);
        }
    }
}
