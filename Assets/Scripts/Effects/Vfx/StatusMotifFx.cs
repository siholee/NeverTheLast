using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Entities;
using Effects.Vfx.Presets;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>상태의 의미를 모양으로 읽히게 한다. 확정된 부여와 DOT 틱 뒤에만 호출한다.</summary>
    public static class StatusMotifFx
    {
        private sealed class Recent { public readonly Dictionary<string, float> Until = new(); }
        private static readonly ConditionalWeakTable<Unit, Recent> Shown = new();
        public static readonly string[] Kinds = { "burn", "shock", "poison", "curse", "frozen", "airborne", "stun", "bind", "slow", "mark", "erosion", "debuff" };

        /// <summary>정보판 위 몸통 안에 붙여 이웃 카드의 상태로 읽히지 않게 한다.</summary>
        public static Vector3 Anchor(Unit target)
        {
            var view = CardMotion.ViewOf(target);
            if (view?.PortraitRenderer == null) return VfxSpace.PortraitPoint(target);
            Bounds bounds = view.PortraitRenderer.bounds;
            return new Vector3(bounds.center.x,
                Mathf.Lerp(VfxSpace.PortraitHudTop(target), bounds.max.y, .32f), 0f);
        }

        public static string Kind(string key)
        {
            key = key?.ToLowerInvariant() ?? string.Empty;
            foreach (string kind in Kinds) if (key.Contains(kind)) return kind;
            if (key.Contains("electro")) return "shock";
            if (key.Contains("speed_down")) return "slow";
            return "debuff";
        }

        public static void Play(Unit target, string key, bool tick = false)
        {
            if (target == null || !target.isActive) return;
            string kind = Kind(key);
            Recent recent = Shown.GetOrCreateValue(target);
            string token = kind + (tick ? ".tick" : ".grant");
            if (recent.Until.TryGetValue(token, out float until) && Time.time < until) return;
            recent.Until[token] = Time.time + .18f;
            // 몸체의 문양과 분리하고 머리 위 피해/부여 알림의 공간을 비운다.
            Vector3 p = Anchor(target);
            float scale = (tick ? .80f : 1.00f) * (kind is "poison" or "stun" ? 1.5f : 1f);
            int layer = VfxSpace.SortingLayer(target), order = VfxSpace.OverlaySortingOrder + 62;
            Color color = kind switch
            {
                "burn" => new Color(.91f,.60f,.43f,.72f),
                "bleed" => new Color(.88f,.55f,.61f,.76f),
                "shock" or "curse" => new Color(.71f,.61f,.83f,.76f),
                "poison" => new Color(.56f,.75f,.48f,.76f),
                "erosion" => new Color(.42f,.69f,.62f,.78f),
                "frozen" or "airborne" => new Color(.49f,.73f,.81f,.78f),
                "stun" => new Color(.84f,.60f,.26f,.92f),
                _ => new Color(.62f,.63f,.78f,.72f)
            };
            void Stroke(Vector2 from, Vector2 to, float width = .026f)
            {
                Vector2 delta = to - from;
                Vector3 point = p + (Vector3)((from + to) * .5f * VfxSpace.CardSize * scale);
                float angle = Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
                Vector2 size = new(delta.magnitude * scale * VfxSpace.CardSize, width * VfxSpace.CardSize);
                Color ink = new(color.r*.32f,color.g*.34f,color.b*.38f,.48f);
                SpriteFx.Play(VfxSpace.GlyphLine,point,ink,false,size*new Vector2(1.04f,1.55f),size*new Vector2(1.08f,1.55f),.45f,angle,0,.08f,.30f,SpriteFx.Ease.OutCubic,layer,order)?.Follow(target.transform);
                SpriteFx.Play(VfxSpace.GlyphLine,point,color,false,size*.92f,size*1.12f,.42f,angle,0,.09f,.35f,SpriteFx.Ease.OutCubic,layer,order+1)?.Follow(target.transform);
            }
            switch(kind)
            {
                case "bleed":
                    Stroke(new Vector2(-.16f,.16f),new Vector2(-.10f,-.14f));
                    Stroke(new Vector2(0,.21f),new Vector2(.06f,-.18f));
                    Stroke(new Vector2(.16f,.14f),new Vector2(.20f,-.10f));
                    break;
                case "shock":
                    for(int i=0;i<2;i++)
                    {
                        float x=(i==0?-1:1)*.25f;
                        Stroke(new(x,.27f),new(x-.06f,.06f)); Stroke(new(x-.06f,.06f),new(x+.04f,.06f)); Stroke(new(x+.04f,.06f),new(x-.03f,-.17f));
                    }
                    break;
                case "curse":
                    SpriteFx.Play(VfxArt.Crescent,p,color,false,.31f*VfxSpace.CardSize*scale,.35f*VfxSpace.CardSize*scale,.50f,-32,25,.10f,.36f,SpriteFx.Ease.OutCubic,layer,order);
                    for(int i=0;i<3;i++) Stroke(new(-.18f+i*.18f,-.17f),new(-.18f+i*.18f,-.08f));
                    break;
                case "poison":
                    for(int i=0;i<3;i++)
                    {
                        Vector3 dot=p+new Vector3((i-1)*.12f,.02f+(i==1?.07f:0f),0)*VfxSpace.CardSize*scale;
                        float size=(i==1?.085f:.068f)*VfxSpace.CardSize*scale;
                        SpriteFx.Play(VfxSpace.Ring,dot,new Color(.33f,.19f,.38f,.55f),false,
                            size*1.18f,size*1.25f,.48f,fadeIn:.08f,fadeOutFrom:.34f,
                            sortingLayer:layer,order:order-1)?.Follow(target.transform);
                        SpriteFx.Play(VfxSpace.ToxicBubble,dot,new Color(.49f,.70f,.30f,.62f),false,
                            size,size*1.08f,.48f,fadeIn:.08f,fadeOutFrom:.34f,
                            sortingLayer:layer,order:order)?.Follow(target.transform);
                        SpriteFx.Play(VfxSpace.BubbleHighlight,dot,
                            new Color(.84f,.94f,.56f,.78f),false,size*.92f,size,.46f,
                            fadeIn:.08f,fadeOutFrom:.34f,sortingLayer:layer,order:order+1)?.Follow(target.transform);
                    }
                    break;
                case "burn":
                    // 베기 자국 같은 세 줄 대신 구매 원화의 한 불꽃과 밝은 심지를 사용한다.
                    HandpaintFx.PlayAt(p,layer,HandpaintFx.Fire1Red,new Vector2(.74f,.68f)*scale,
                        new Color(1f,.68f,.35f,.86f),new Color(1f,.94f,.73f,.92f),
                        orderOffset:62,peakAlpha:.26f,glowAlpha:.08f,heldFrame:7,
                        peakHoldSeconds:.04f,desaturate:.35f,steadyBody:true,calmLight:true,
                        paintContrast:.30f)?.ClipPortraitHud(target);
                    PortraitAccentFx.FlameCore(target,.75f*scale,.38f);
                    break;
                case "frozen":
                    for(int i=0;i<3;i++)
                    { float a=i*Mathf.PI/3; Vector2 d=new(Mathf.Cos(a)*.24f,Mathf.Sin(a)*.24f); Stroke(-d,d); }
                    break;
                case "airborne":
                    for(int i=0;i<2;i++) { float y=-.08f+i*.16f; Stroke(new(-.17f,y),new(0,y+.10f)); Stroke(new(0,y+.10f),new(.17f,y)); }
                    break;
                case "bind":
                    for(int i=0;i<3;i++)
                    { float x=(i-1)*.17f; Stroke(new(x-.07f,0),new(x,.09f)); Stroke(new(x,.09f),new(x+.07f,0)); Stroke(new(x+.07f,0),new(x,-.09f)); Stroke(new(x,-.09f),new(x-.07f,0)); }
                    break;
                case "stun":
                    // 같은 기절 별을 머리 위 짧은 궤도로 묶고 군청 받침으로 흰 갈기와 분리한다.
                    for(int i=0;i<3;i++)
                    {
                        Vector3 star=p+new Vector3((i-1)*.23f,.24f+(i==1?.05f:0f),0)*VfxSpace.CardSize*scale;
                        SpriteFx.Play(VfxArt.Glint,star,new Color(.11f,.20f,.31f,.88f),false,.20f*VfxSpace.CardSize*scale,.23f*VfxSpace.CardSize*scale,.48f,i*25,80,.08f,.38f,SpriteFx.Ease.OutCubic,layer,order-1);
                        SpriteFx.Play(VfxArt.Glint,star,new Color(.94f,.72f,.33f,.96f),false,.14f*VfxSpace.CardSize*scale,.17f*VfxSpace.CardSize*scale,.48f,i*25,80,.08f,.38f,SpriteFx.Ease.OutCubic,layer,order);
                    }
                    break;
                case "erosion":
                    // 깨진 방패 외곽과 하강 화살표로 방어 저하를 읽히게 하고 원화의 넓은 면을 비운다.
                    Stroke(new(-.20f,.18f),new(-.20f,-.02f));
                    Stroke(new(-.20f,-.02f),new(0,-.20f));
                    Stroke(new(0,-.20f),new(.20f,-.02f));
                    Stroke(new(.20f,-.02f),new(.20f,.18f));
                    Stroke(new(-.20f,.18f),new(-.08f,.15f));
                    Stroke(new(-.08f,.15f),new(-.015f,.23f),.034f);
                    Stroke(new(-.015f,.23f),new(.04f,.10f),.034f);
                    Stroke(new(.04f,.10f),new(.11f,.17f),.034f);
                    Stroke(new(.11f,.17f),new(.20f,.18f));
                    Stroke(new(0,.12f),new(0,-.07f));
                    Stroke(new(-.07f,0),new(0,-.07f));
                    Stroke(new(0,-.07f),new(.07f,0));
                    break;
                case "slow":
                {
                    // 캡·유리 면·모래를 함께 보여 교차선이 취소 X로 읽히지 않게 한다.
                    Vector3 q=p+new Vector3(.13f,.07f,0f)*VfxSpace.CardSize;
                    Vector2 size=new Vector2(.29f,.39f)*VfxSpace.CardSize;
                    SpriteFx.Play(VfxSpace.HourglassGlyph,q,new Color(.10f,.17f,.27f,.84f),false,
                        size*1.13f,size*1.13f,.46f,fadeIn:.06f,fadeOutFrom:.60f,sortingLayer:layer,order:order);
                    SpriteFx.Play(VfxSpace.HourglassGlyph,q,new Color(.78f,.88f,.98f,.94f),false,
                        size,size,.46f,fadeIn:.06f,fadeOutFrom:.60f,sortingLayer:layer,order:order+1);
                    SpriteFx.Play(VfxSpace.HourglassSand,q,new Color(.23f,.32f,.49f,.88f),false,
                        size,size,.46f,fadeIn:.06f,fadeOutFrom:.60f,sortingLayer:layer,order:order+2);
                    break;
                }
                case "mark":
                    SpriteFx.Play(VfxSpace.Ring,p,color,false,.29f*VfxSpace.CardSize,.33f*VfxSpace.CardSize,.46f,0,30,.12f,.40f,SpriteFx.Ease.OutCubic,layer,order);
                    Stroke(new(-.24f,0),new(-.14f,0)); Stroke(new(.14f,0),new(.24f,0));
                    break;
                default:
                    for(int i=0;i<2;i++) { float y=.08f-i*.15f; Stroke(new(-.13f,y),new(0,y-.10f)); Stroke(new(0,y-.10f),new(.13f,y)); }
                    break;
            }
        }
    }
}
