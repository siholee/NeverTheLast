using System;
using System.Collections;
using Effects.Vfx.Presets;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>이시스의 영구 수호령은 고해상도 벡터 문양으로 표시. 시전은 기존 구매 Handpaint 시트를 사용한다.</summary>
    public static class EgyptianReworkFx
    {
        private static readonly Sprite[] Glyphs=new Sprite[3];
        public static Sprite GuardianGlyph(int index)
        {
            index=Mathf.Clamp(index,0,2);if(Glyphs[index]!=null)return Glyphs[index];
            const int size=192;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="IsisGuardianGlyph"+index,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[size*size];
            Vector2[][] paths=index switch
            {
                0=>new[]{new[]{new Vector2(.5f,.78f),new(.35f,.67f),new(.35f,.52f),new(.5f,.43f),new(.65f,.52f),new(.65f,.67f),new(.5f,.78f)},new[]{new Vector2(.5f,.43f),new(.5f,.15f)},new[]{new Vector2(.25f,.4f),new(.75f,.4f)}},
                1=>new[]{new[]{new Vector2(.14f,.5f),new(.35f,.67f),new(.65f,.67f),new(.86f,.5f),new(.65f,.33f),new(.35f,.33f),new(.14f,.5f)},new[]{new Vector2(.5f,.63f),new(.4f,.5f),new(.5f,.37f),new(.6f,.5f),new(.5f,.63f)}},
                _=>new[]{new[]{new Vector2(.5f,.20f),new(.12f,.72f),new(.27f,.68f),new(.5f,.38f),new(.73f,.68f),new(.88f,.72f),new(.5f,.20f)},new[]{new Vector2(.5f,.38f),new(.5f,.73f)},new[]{new Vector2(.31f,.42f),new(.23f,.6f)},new[]{new Vector2(.69f,.42f),new(.77f,.6f)}}
            };
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                Vector2 p=new((x+.5f)/size,(y+.5f)/size);float distance=10;
                foreach(var path in paths)for(int i=1;i<path.Length;i++)
                {Vector2 a=path[i-1],d=path[i]-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude));distance=Mathf.Min(distance,Vector2.Distance(p,a+d*t));}
                byte alpha=(byte)Mathf.RoundToInt(Mathf.Clamp01((.015f-distance)*size+ .5f)*255);
                pixels[y*size+x]=new Color32(255,255,255,alpha);
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            return Glyphs[index]=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100);
        }
        public static IEnumerator Chant(Unit caster,bool blood,bool ultimate=false)
        {
            if(caster==null)yield break;
            Color light=blood?new Color(.97f,.77f,.76f):new Color(.98f,.92f,.73f);
            Color edge=blood?new Color(.81f,.43f,.48f):new Color(.85f,.70f,.40f);
            HandpaintFx.PlayAt(VfxSpace.PortraitPoint(caster),VfxSpace.SortingLayer(caster),HandpaintFx.Impact1White,
                Vector2.one*(ultimate?1f:.65f),light,edge,peakAlpha:.62f,glowAlpha:.10f,calmLight:true);
            yield return new WaitForSeconds(ultimate ? .48f : .36f);
        }
        public static IEnumerator Slash(Unit caster,Unit target,Action impact)
        {
            yield return CardMotion.Charge(caster,target,(point,direction)=>
            {
                ImpactFx.Slam(point,direction,new Color(.86f,.61f,.62f),1f);
                impact?.Invoke();
            });
        }
    }
}
