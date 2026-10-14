using Effects.Vfx.Presets;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>등장·기본 충돌·집중 해제의 구매 손그림 연출. 기존 Hovl 세트의 수치만 호환한다.</summary>
    public static class CommonHandpaintFx
    {
        public static void Flash(HovlProjectileSet set, Vector3 point, Vector3 direction, float cardSize,
            int layer, int order, float scale = 1f, float alpha = 1f)
        {
            if(set==null)return;
            Color body=BattleVfxStyle.Body(set.Tint,set.Tint); body.a=Mathf.Min(.82f,set.Alpha*alpha);
            float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            scale *= Mathf.Clamp(set.FlashScale/.30f,.30f,1.8f);
            HandpaintFx.PlayAt(point,layer,HandpaintFx.Slash5Blue,new Vector2(.88f,.45f)*scale,
                body,BattleVfxStyle.Light(set.Tint),angle,order-VfxSpace.OverlaySortingOrder,.74f,.14f,desaturate:1f,calmLight:true);
        }
        public static void Hit(HovlProjectileSet set, Vector3 point, Vector3 direction, float cardSize,
            int layer, int order, float scale = 1f, float alpha = 1f)
        {
            if(set==null)return;
            Contact(point,direction,set.Tint,Mathf.Clamp(set.HitScale/.44f,.30f,1.8f)*scale,layer,order,alpha);
        }
        public static void Contact(Vector3 point,Vector3 direction,Color tint,float scale=1f,int layer=0,int order=0,float alpha=1f)
        {
            if(order==0)order=VfxSpace.OverlaySortingOrder+35;
            Color body=BattleVfxStyle.Body(tint,tint);body.a=Mathf.Min(.88f,alpha);
            float angle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;
            HandpaintFx.PlayAt(point,layer,HandpaintFx.Impact1White,new Vector2(.95f,.66f)*scale,body,
                BattleVfxStyle.Light(tint),angle,order-VfxSpace.OverlaySortingOrder,.82f,.18f,desaturate:1f,calmLight:true);
            HandpaintFx.PlayAt(point,layer,HandpaintFx.Smoke5Blue,new Vector2(.85f,.35f)*scale, new Color(body.r,body.g,body.b,.34f),
                BattleVfxStyle.Light(tint),angle,order-VfxSpace.OverlaySortingOrder-2,.40f,.08f,desaturate:1f,calmLight:true);
        }
    }
}
