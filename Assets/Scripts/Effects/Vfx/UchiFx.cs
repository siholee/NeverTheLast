using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Entities;
using Entities.View;
using Managers;
using UnityEngine;

namespace Effects.Vfx
{
    public static class UchiFx
    {
        private static Sprite _paper;
        public static void Created(Unit clone)
        {
            if (clone == null) return;
            clone.gameObject.AddComponent<SummonCardView>().Bind(clone);
            VfxComposer.PlaySlot("uchi.clone", "cast", 0, clone.SummonOwner, clone,
                VfxSpace.WorldPoint(clone), VfxSpace.SortingLayer(clone));
            AudioManager.PlayCue("uchi.clone.resolve");
        }

        public static IEnumerator PrepareStrike(Unit clone)
        {
            Unit target = Codes.Normal.UchiCloneStrike.Enemies(clone.SummonOwner).FirstOrDefault();
            VfxComposer.PlayScreen("uchi.strike", clone, clone);
            VfxComposer.PlaySlot("uchi.strike", "cast", 0, clone, target, VfxSpace.WorldPoint(clone), VfxSpace.SortingLayer(clone));
            var view = clone.GetComponent<SummonCardView>();
            if(view != null) yield return view.Advance(target);
            else yield return new WaitForSeconds(.24f);
        }

        /// <summary>대상은 기존 판정 코드가 고른다. 이 함수는 난수를 쓰거나 피해를 가하지 않는다.</summary>
        public static void Strike(Unit clone, Unit target, float scale = 1f, bool ultimate = false)
        {
            if (clone == null || target == null) return;
            string key = ultimate ? "uchi.manchunhwau" : scale < 1f ? "uchi.overflow" : "uchi.strike";
            VfxComposer.PlaySlot(key, "hit", 0, clone, target, VfxSpace.PortraitPoint(target), VfxSpace.SortingLayer(target));
            CardMotion.RakeReaction(target, VfxSpace.AimFrom(clone,target).Direction,.34f);
            AudioManager.PlayCue(key+".hit");
        }

        public static void Guard(Unit clone, Unit attacker)
        {
            clone.GetComponent<SummonCardView>()?.Guard(attacker);
            VfxComposer.PlaySlot("uchi.guard", "cast", 0, clone.SummonOwner, attacker,
                VfxSpace.WorldPoint(clone), VfxSpace.SortingLayer(clone));
            AudioManager.PlayCue("uchi.guard.hit");
        }

        public static IEnumerator PrepareUltimate(Unit caster, IReadOnlyList<Unit> clones)
        {
            VfxComposer.PlayScreen("uchi.manchunhwau",caster,caster);
            VfxComposer.PlaySlot("uchi.manchunhwau","cast",0,caster,caster,VfxSpace.WorldPoint(caster),VfxSpace.SortingLayer(caster));
            Unit direction = Codes.Normal.UchiCloneStrike.Enemies(caster).FirstOrDefault();
            // 대상 추첨은 기존 순회 판정 안에 남기고 표시 방향만 읽어 세 장을 동시에 부분 전진시킨다.
            foreach(Unit clone in clones)
                if(clone != null && clone.GetComponent<SummonCardView>() is SummonCardView view)
                    VfxRunner.Run(view.Advance(direction));
            yield return new WaitForSeconds(.24f);
        }

        public static void PaperDeparture(Vector3 point, Sprite portrait, Vector3 scale, Color tint, Unit clone)
        {
            int layer=VfxSpace.SortingLayer(clone);
            SpriteRenderer ghost=VfxSpace.NewSprite(null,"ClonePaperDeparture",portrait,tint,layer,VfxSpace.OverlaySortingOrder-10,false);
            var ownerView=CardMotion.ViewOf(clone.SummonOwner);
            if(ownerView?.PortraitRenderer!=null)ghost.sortingOrder=ownerView.PortraitRenderer.sortingOrder-1;
            ghost.transform.position=point;ghost.transform.localScale=scale;
            VfxRunner.Run(Fade(ghost,tint,clone.GetInstanceID(),point));
            float c=VfxSpace.CardSize;
            // 작은 종이 조각만 규칙적으로 흩어 전투 난수열을 건드리지 않는다.
            for(int i=0;i<5;i++)
            {
                float angle=42f+i*27f;
                SpriteFx.Play(Paper(),point+new Vector3((i-2)*.045f,.10f+i*.025f)*c,
                    new Color(.87f,.91f,.68f,.74f),false,new Vector2(.045f,.10f)*c,
                    new Vector2(.018f,.05f)*c,.26f,angle,fadeOutFrom:.1f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+20);
            }
            AudioManager.PlayCue("uchi.depart.resolve");
            Trace("clone_depart_start", clone.GetInstanceID(), point);
        }

        private static Sprite Paper()
        {
            if (_paper != null) return _paper;
            // 부적은 광물 파편과 구별되는 찢긴 종이·먹선 한 장으로 만든다. 신규 검수 자산을 Resources에 넣지 않는다.
            var texture = new Texture2D(24, 40, TextureFormat.RGBA32, false);
            var pixels = new Color[24 * 40];
            for (int y = 2; y < 38; y++)
                for (int x = 3; x < 21; x++)
                {
                    bool torn = (y < 5 && x % 5 < 2) || (y > 34 && x % 7 < 3);
                    bool ink = x >= 10 && x <= 13 && y >= 10 && y <= 30 ||
                               y % 8 < 2 && y > 8 && y < 31 && x >= 7 && x <= 17;
                    pixels[y * 24 + x] = torn ? Color.clear : ink ? new Color(.14f, .30f, .29f, .9f) : new Color(.96f, .91f, .72f, .92f);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            _paper = Sprite.Create(texture, new Rect(0, 0, 24, 40), new Vector2(.5f, .5f), 40f);
            return _paper;
        }

        private static IEnumerator Fade(SpriteRenderer ghost,Color color, int instanceId, Vector3 point)
        {
            for(float t=0f;t<.26f;t+=Time.deltaTime)
            {
                if(ghost==null)yield break;
                ghost.color=new Color(color.r,color.g,color.b,color.a*(1f-t/.26f));
                yield return null;
            }
            if(ghost!=null)UnityEngine.Object.Destroy(ghost.gameObject);
            Trace("clone_depart_end", instanceId, point);
        }
        private static void Trace(string tag, int instanceId, Vector3 point)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Core.VerifyEvents.Raise(FormattableString.Invariant($"{tag}|{instanceId}|{point.x:F5}|{point.y:F5}"));
#endif
        }
    }
}
