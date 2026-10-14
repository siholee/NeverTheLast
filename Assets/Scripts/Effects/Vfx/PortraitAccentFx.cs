using Effects.Vfx.Presets;
using Entities;
using UnityEngine;

namespace Effects.Vfx
{
    /// <summary>원화 밖의 작은 충돌점으로 의미를 전달한다. 피해·자원 판정은 호출한 코드만 소유한다.</summary>
    public static class PortraitAccentFx
    {
        public static void WindStrikeFlow(Unit target)
        {
            if(target==null)return;
            float card=VfxSpace.CardSize;
            Vector3 center=StatusMotifFx.Anchor(target);
            center.x+=.09f*card;
            int layer=VfxSpace.SortingLayer(target);
            // 회복 안개와 구분되는 세 줄 돌풍. 구매 팩의 약한 착탄 면과 같은 정점에만 뜬다.
            for(int i=-1;i<=1;i++)
            {
                Vector3 at=center+new Vector3(i*.018f,i*.07f,0)*card;
                Vector2 from=new Vector2(.42f,.083f)*card,to=new Vector2(.60f,.083f)*card;
                SpriteFx.Play(VfxSpace.Crescent,at,new Color(.13f,.29f,.37f,.76f),false,
                    from*new Vector2(1f,1.45f),to*new Vector2(1f,1.45f),.25f,
                    fadeIn:0f,fadeOutFrom:.56f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+145)?.Follow(target.transform);
                SpriteFx.Play(VfxSpace.Crescent,at,new Color(.78f,.92f,.98f,.94f),false,
                    from,to,.25f,fadeIn:0f,fadeOutFrom:.56f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+146)?.Follow(target.transform);
            }
        }

        public static void WindFlow(Unit caster)
        {
            if (caster == null) return;
            float c = VfxSpace.CardSize;
            Vector3 p = VfxSpace.WorldPoint(caster) + Vector3.right * (caster.IsEnemy ? .45f : -.45f) * c;
            p.y = VfxSpace.PortraitHudTop(caster) + .13f * c;
            int layer = VfxSpace.SortingLayer(caster);
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = p + new Vector3(i * .035f, (i - 1) * .06f) * c;
                Vector2 size = new(.26f * c, .09f * c);
                SpriteFx.Play(VfxSpace.Crescent, at, new Color(.17f,.27f,.39f,.70f),false,
                    size*1.12f,size*1.20f,.36f,-12f,
                    fadeIn:0f,fadeOutFrom:.55f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+145);
                SpriteFx.Play(VfxSpace.Crescent, at, new Color(.71f,.87f,.97f,.92f),false,
                    size,size*1.08f,.36f,-12f,
                    fadeIn:0f,fadeOutFrom:.55f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+146);
            }
        }

        public static void OverflowRipple(Unit target)
        {
            if (target == null) return;
            float c = VfxSpace.CardSize;
            Vector3 p = StatusMotifFx.Anchor(target) + Vector3.right * .18f * c;
            int layer = VfxSpace.SortingLayer(target);
            for (int i = 0; i < 2; i++)
            {
                Vector2 from = Vector2.one * (.16f + i * .05f) * c;
                Vector2 to = Vector2.one * (.36f + i * .08f) * c;
                SpriteFx.Play(VfxSpace.PressureRing,p,new Color(.16f,.28f,.39f,.62f),false,
                    from*1.08f,to*1.08f,.28f,
                    fadeIn:.02f,fadeOutFrom:.54f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+145);
                SpriteFx.Play(VfxSpace.PressureRing,p,new Color(.69f,.90f,.87f,.86f),false,
                    from,to,.28f,
                    fadeIn:.02f,fadeOutFrom:.54f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+146);
            }
        }

        /// <summary>경호자와 보호받는 아군을 손 아래의 얇은 연결선으로 잇는다. 4배속에서도 12프레임 남는다.</summary>
        public static void GuardLink(Unit protector,Unit protectedUnit)
        {
            if(protector==null||protectedUnit==null)return;
            float c=VfxSpace.CardSize;
            Vector3 from=VfxSpace.CastPoint(protector,protectedUnit);
            Vector3 to=VfxSpace.CastPoint(protectedUnit,protector);
            Vector3 knee=(from+to)*.5f+Vector3.down*(c*.22f);
            float life=VfxTiming.AtLeastFrames(.40f,12);
            int layer=SortingLayer.NameToID("CombatHud");
            if(layer==0)layer=VfxSpace.SortingLayer(protector);
            void Stroke(Vector3 a,Vector3 b,float width)
            {
                Vector3 delta=b-a;float angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
                Vector2 size=new(delta.magnitude,width*c);
                SpriteFx.Play(VfxSpace.GlyphLine,(a+b)*.5f,new Color(.10f,.23f,.38f,.76f),false,
                    size*new Vector2(1f,1.6f),size*new Vector2(1f,1.6f),life,angle,fadeIn:.05f,fadeOutFrom:.70f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+58)?.HoldUnscaled(.26f);
                SpriteFx.Play(VfxSpace.GlyphLine,(a+b)*.5f,new Color(.65f,.84f,1f,.94f),false,
                    size,size,life,angle,fadeIn:.05f,fadeOutFrom:.70f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+59)?.HoldUnscaled(.26f);
            }
            Stroke(from,knee,.022f);Stroke(knee,to,.022f);
            Vector3 shield=StatusMotifFx.Anchor(protector)+Vector3.right*(c*.34f);
            shield.y=Mathf.Max(shield.y,VfxSpace.PortraitHudTop(protector)+c*.16f);
            Vector2[] outline={new(-.12f,.10f),new(.12f,.10f),new(.09f,-.04f),new(0,-.13f),new(-.09f,-.04f),new(-.12f,.10f)};
            for(int i=0;i<outline.Length-1;i++)Stroke(shield+(Vector3)(outline[i]*c),shield+(Vector3)(outline[i+1]*c),.012f);
        }

        private static Vector3 Edge(Unit caster, Unit target, out float side)
        {
            side = VfxSpace.AimFrom(caster, target).Direction.x >= 0f ? -1f : 1f;
            var view = CardMotion.ViewOf(target);
            if (view?.PortraitRenderer == null) return VfxSpace.PortraitPoint(target);
            Bounds bounds = view.PortraitRenderer.bounds;
            return new Vector3(bounds.center.x + side * (bounds.extents.x - .14f * VfxSpace.CardSize),
                Mathf.Lerp(bounds.min.y, bounds.max.y, .67f), 0f);
        }

        public static void Fire(Unit caster, Unit target, string skillKey)
        {
            if (target == null) return;
            float c = VfxSpace.CardSize;
            Vector3 p = Edge(caster, target, out float side);
            int layer = VfxSpace.SortingLayer(target);
            bool agni=skillKey is "agni.follow_up" or "agni.focused_follow_up";
            if(agni) p=StatusMotifFx.Anchor(target);
            Color warm = agni?new Color(.91f,.40f,.16f,.86f):new Color(.99f, .73f, .23f, .86f);
            Color rim = new(.90f, .42f, .20f, .46f);
            SpriteFx.Play(VfxArt.ImpactStar,p,agni?new Color(.91f,.46f,.22f,.88f):new Color(1f,.83f,.45f,.95f),false,
                .26f*c,.34f*c,.13f,fadeIn:0f,fadeOutFrom:.50f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+64);
            SpriteFx.Play(VfxArt.Starburst, p, warm, true, .15f*c, .36f*c, .09f,
                fadeIn: 0f, fadeOutFrom: .42f, sortingLayer: layer, order: VfxSpace.OverlaySortingOrder+65);
            SpriteFx.Play(VfxArt.Shockwave, p, rim, false, .22f*c, .46f*c, .19f,
                fadeIn: .03f, fadeOutFrom: .36f, sortingLayer: layer, order: VfxSpace.OverlaySortingOrder+61);
            if (skillKey == "himiko.ember")
            {
                // 부채 화염은 두 짧은 혀가 바깥으로 벌어지고, 조각가의 곧은 쌍화염과 구분된다.
                for (int i=-1; i<=1; i++)
                    HandpaintFx.PlayAt(p + Vector3.up*i*.055f*c, layer, HandpaintFx.FireAttack1Red,
                        new Vector2(.46f,.26f), new Color(1f,.74f,.38f,.86f), warm,
                        (side>0f?0f:180f)+i*24f, 62, .68f, .20f, heldFrame:4,peakHoldSeconds:.08f,desaturate:1f,steadyBody:true,startFrame:4,calmLight:true);
            }
            else if (skillKey == "pygmalion.ember")
            {
                var flame = new HandpaintSheet("Fire3_Yellow",3,3,60f,new Vector2(.5f,.5f));
                for (int i=-1; i<=1; i+=2)
                    HandpaintFx.PlayAt(p+Vector3.right*i*.055f*c,layer,flame,new Vector2(.18f,.34f),
                        new Color(1f,.78f,.40f,.90f),warm,i*12f,62,.70f,.20f,heldFrame:4,peakHoldSeconds:.08f,desaturate:1f,steadyBody:true,startFrame:4,calmLight:true);
            }
            VfxParticles.Burst(p,new ParticleSpec
            {
                Sprite=VfxArt.Glint, Additive=true, StartColor=warm, EndColor=new Color(1f,.60f,.22f,0f),
                Lifetime=new Vector2(.20f,.28f), Speed=new Vector2(.42f,.85f)*c,
                Angle=side>0f?35f:145f, Spread=48f, StartSize=new Vector2(.035f,.065f)*c,
                EndSizeScale=.30f, Gravity=.45f*c, Drag=2f, Spin=new Vector2(30f,70f), Stretch=1.6f,
            },6,layer,VfxSpace.OverlaySortingOrder+67);
        }

        public static void Moon(Unit caster, Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;
            Vector3 p=Edge(caster,target,out float side);
            int layer=VfxSpace.SortingLayer(target);
            SpriteFx.Play(VfxArt.Crescent,p,new Color(.30f,.40f,.65f,.52f),false,
                .34f*c,.39f*c,.23f,side>0f?20f:160f,fadeIn:0f,fadeOutFrom:.56f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+61);
            SpriteFx.Play(VfxArt.Crescent,p,new Color(.93f,.96f,1f,.86f),true,
                .30f*c,.35f*c,.21f,side>0f?20f:160f,fadeIn:0f,fadeOutFrom:.52f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+62);
        }

        // 넓은 분진과 별도로 몸통에 비가산 충돌 코어를 남긴다. 판정 콜백은 소유하지 않는다.
        public static void HeavyContact(Unit target, Color color, bool moon, bool explosion=false)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;
            Vector3 p=StatusMotifFx.Anchor(target);
            int layer=VfxSpace.SortingLayer(target);
            Sprite shape=moon?VfxArt.Crescent:explosion?VfxArt.Starburst:VfxArt.ImpactStar;
            Vector2 size=moon?new Vector2(.46f,.42f):explosion?new Vector2(.56f,.56f):new Vector2(.74f,.24f);
            SpriteFx.Play(shape,p,new Color(.20f,.30f,.43f,.25f),false,
                size*c,size*1.12f*c,.24f,moon?-28f:0f,fadeIn:0f,fadeOutFrom:.58f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+66);
            color=Color.Lerp(color,Color.white,.82f);color.a=.96f;
            SpriteFx.Play(shape,p,color,false,size*.90f*c,size*1.04f*c,.23f,
                moon?-28f:0f,fadeIn:0f,fadeOutFrom:.56f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+67);
            // 넓은 불투명 원판 대신 작은 타격점만 밝게 남긴다.
            SpriteFx.Play(VfxSpace.Glow,p,new Color(.96f,.98f,1f,.42f),false,
                .09f*c,.16f*c,.13f,fadeIn:0f,fadeOutFrom:.38f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+68);
        }

        // 사수의 피격은 연무 덩어리 대신 한 방향의 관통 획과 작은 접점을 남긴다.
        public static void EnemyPierce(Unit caster,Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+Vector3.up*.04f*c;
            int layer=VfxSpace.SortingLayer(target);float angle=VfxSpace.AimFrom(caster,target).AngleDegrees;
            Vector2 size=new Vector2(.62f,.038f)*c;
            SpriteFx.Play(VfxSpace.ClawCue,p,new Color(.08f,.14f,.22f,.47f),false,
                size*new Vector2(1.06f,1.7f),size*new Vector2(1.25f,1.1f),.21f,rotation:angle,
                fadeIn:0f,fadeOutFrom:.52f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+150);
            SpriteFx.Play(VfxSpace.ClawCue,p,new Color(1f,.69f,.61f,.88f),false,
                size,size*new Vector2(1.22f,.60f),.21f,rotation:angle,
                fadeIn:0f,fadeOutFrom:.52f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+151);
            SpriteFx.Play(VfxSpace.HardDot,p,new Color(.94f,.94f,.94f,.94f),false,
                .07f*c,.04f*c,.13f,fadeIn:0f,fadeOutFrom:.45f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+152);
        }

        public static void EnemyCrusher(Unit target)
        {
            if(target==null)return;
            SmallExplosion(target);
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+Vector3.right*.22f*c;
            int layer=VfxSpace.SortingLayer(target);
            for(int i=0;i<4;i++)
            {
                float a=(i*90f+25f)*Mathf.Deg2Rad;Vector3 direction=new Vector3(Mathf.Cos(a),Mathf.Sin(a)*.65f,0f);
                Vector3 q=p+direction*.20f*c;Vector2 size=new Vector2(.065f,.10f)*c;
                SpriteFx edge=SpriteFx.Play(VfxSpace.CrystalFacet,q,new Color(.08f,.14f,.22f,.46f),false,
                    size*1.30f,size,.24f,rotation:i*43f,spin:80f,fadeIn:0f,fadeOutFrom:.35f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+154);
                SpriteFx face=SpriteFx.Play(VfxSpace.CrystalFacet,q,new Color(1f,.69f,.61f,.90f),false,
                    size,size*.78f,.24f,rotation:i*43f,spin:80f,fadeIn:0f,fadeOutFrom:.35f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+155);
                VfxRunner.Run(FragmentTravel(edge,face,q,(direction*.20f+Vector3.down*.10f)*c));
            }
        }

        // 소형 폭발의 중심과 바깥 흐름만 보강한다. 판정/구매 시트의 정점은 기존 경로가 소유한다.
        public static void SmallExplosion(Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+new Vector3(.22f,.08f,0f)*c;
            int layer=VfxSpace.SortingLayer(target);
            // 작은 폭발은 채운 원판이 아닌 여섯 방향의 짧은 파열과 얇은 팽창 림으로 읽힌다.
            for(int i=0;i<3;i++)
            {
                Vector2 size=new Vector2(.38f,.031f)*c;
                SpriteFx.Play(VfxSpace.ClawCue,p,new Color(.08f,.14f,.22f,.49f),false,
                    size*new Vector2(1.03f,1.7f),size*new Vector2(1.16f,1.2f),.20f,rotation:15f+i*60f,
                    fadeIn:0f,fadeOutFrom:.40f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+150);
                SpriteFx.Play(VfxSpace.ClawCue,p,new Color(.94f,.94f,.94f,.96f),false,
                    size,size*new Vector2(1.15f,.65f),.20f,rotation:15f+i*60f,
                    fadeIn:0f,fadeOutFrom:.40f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+151);
            }
            SpriteFx.Play(VfxSpace.HardDot,p,new Color(1f,1f,1f,.94f),false,
                .075f*c,.055f*c,.13f,fadeIn:0f,fadeOutFrom:.45f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+153);
            SpriteFx.Play(VfxArt.Shockwave,p,new Color(.08f,.14f,.22f,.46f),false,
                .26f*c,.56f*c,.24f,fadeIn:0f,fadeOutFrom:.55f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148);
            SpriteFx.Play(VfxArt.Shockwave,p,new Color(1f,.75f,.72f,.54f),false,
                .24f*c,.52f*c,.24f,fadeIn:0f,fadeOutFrom:.55f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+149);
        }

        // 물리 압력은 채운 먼지 면보다 외곽이 비치는 타원 충격파와 짧은 접촉선을 쓴다.
        public static void PressureHit(Unit target,bool punch)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+new Vector3(.18f,.06f,0f)*c;
            int layer=VfxSpace.SortingLayer(target);
            if(punch)
            {
                Vector2 size=new Vector2(.48f,.33f)*c;
                SpriteFx.Play(VfxSpace.PressureRing,p,new Color(.08f,.14f,.22f,.49f),false,
                    size,size*1.30f,.23f,rotation:-12f,fadeIn:0f,fadeOutFrom:.45f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+146);
                SpriteFx.Play(VfxSpace.PressureRing,p,new Color(.76f,.86f,1f,.90f),false,
                    size*.91f,size*1.20f,.23f,rotation:-12f,fadeIn:0f,fadeOutFrom:.45f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+147);
            }
            for(int i=0;i<3;i++)
            {
                Vector2 size=new Vector2(i==1?.34f:.27f,punch?.034f:.046f)*c;
                SpriteFx.Play(VfxSpace.ClawCue,p,new Color(.08f,.14f,.22f,.49f),false,
                    size*new Vector2(1.05f,1.65f),size*new Vector2(1.22f,1.10f),.18f,rotation:25f+i*60f,
                    fadeIn:0f,fadeOutFrom:.48f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148);
                Color contact=punch?new Color(.94f,.94f,.94f,.90f):new Color(.70f,.80f,.96f,.92f);
                SpriteFx.Play(VfxSpace.ClawCue,p,contact,false,
                    size,size*new Vector2(1.20f,.65f),.18f,rotation:25f+i*60f,
                    fadeIn:0f,fadeOutFrom:.48f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+149);
            }
        }

        public static void SmokeSweep(Unit target,bool afterglow)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+new Vector3(afterglow?.43f:.35f,afterglow?-.02f:.03f,0f)*c;
            int layer=VfxSpace.SortingLayer(target);
            for(int i=0;i<2;i++)
            {
                Vector3 q=p+Vector3.up*(i-.5f)*.09f*c;
                Vector2 size=new Vector2(afterglow?.40f:.58f,afterglow?.11f:.20f)*c;
                Sprite shape=afterglow?VfxSpace.SmokeWisp:VfxSpace.SmokeRibbon;
                SpriteFx.Play(shape,q,new Color(.10f,.15f,.24f,afterglow?.34f:.49f),false,
                    size*new Vector2(1f,1.45f),size*new Vector2(1.30f,1.20f),.25f,rotation:i==0?6f:-4f,
                    fadeIn:.02f,fadeOutFrom:.40f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+146);
                Color ribbon=afterglow?new Color(.84f,.87f,.98f,.62f):new Color(.67f,.73f,.90f,.95f);
                SpriteFx.Play(shape,q,ribbon,false,
                    size,size*new Vector2(1.30f,.70f),.25f,rotation:i==0?6f:-4f,
                    fadeIn:.02f,fadeOutFrom:.40f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+147);
            }
        }

        public static void CrystalFragments(Unit target,bool debris)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+new Vector3(.18f,.025f,0f)*c;
            int layer=VfxSpace.SortingLayer(target);
            int count=debris?6:4;
            for(int i=0;i<count;i++)
            {
                float angle=(i*360f/count+25f)*Mathf.Deg2Rad;
                Vector3 direction=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle)*.55f,0f);
                Vector3 q=p+direction*.17f*c+(debris?Vector3.down*.08f*c:Vector3.zero);
                Vector2 size=(debris?new Vector2(.066f,.11f):new Vector2(.11f,.22f))*c;
                SpriteFx edge=SpriteFx.Play(VfxSpace.CrystalFacet,q,new Color(.08f,.15f,.23f,.46f),false,
                    size*1.30f,size*1.01f,.24f,rotation:i*37f,spin:90f,fadeIn:0f,fadeOutFrom:.30f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+146);
                SpriteFx face=SpriteFx.Play(VfxSpace.CrystalFacet,q,new Color(.70f,.84f,1f,.94f),false,
                    size,size*.78f,.24f,rotation:i*37f,spin:90f,fadeIn:0f,fadeOutFrom:.30f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+147);
                VfxRunner.Run(FragmentTravel(edge,face,q,(direction*.22f+(debris?Vector3.down*.15f:Vector3.zero))*c));
            }
        }

        private static System.Collections.IEnumerator FragmentTravel(SpriteFx edge,SpriteFx face,Vector3 origin,Vector3 travel)
        {
            for(float age=0f;age<.24f;age+=Time.deltaTime)
            {
                Vector3 p=origin+travel*Mathf.Clamp01(age/.24f);
                if(edge!=null)edge.transform.position=p;if(face!=null)face.transform.position=p;
                yield return null;
            }
        }

        // 짐승의 준비는 공격 흔적과 구별되는 짧은 평행 발톱 세 획으로 읽힌다.
        public static void BeastPrepare(Unit caster)
        {
            if(caster==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(caster);
            var view=CardMotion.ViewOf(caster);
            if(view?.PortraitRenderer!=null)
            {
                Bounds bounds=view.PortraitRenderer.bounds;
                p=new Vector3(bounds.min.x+.045f*c,VfxSpace.PortraitHudTop(caster)+.18f*c,0f);
            }
            int layer=VfxSpace.SortingLayer(caster);
            for(int i=0;i<3;i++)
            {
                Vector3 q=p+new Vector3((i-1)*.060f,(i-1)*.025f,0f)*c;
                Vector2 size=new Vector2(.24f,.038f)*c;
                SpriteFx.Play(VfxSpace.ClawCue,q,new Color(.08f,.14f,.22f,.44f),false,
                    size*new Vector2(1f,1.8f),size*new Vector2(1.08f,1.2f),.28f,rotation:65f,
                    fadeIn:.025f,fadeOutFrom:.60f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+147);
                SpriteFx.Play(VfxSpace.ClawCue,q,new Color(.79f,.90f,1f,.90f),false,
                    size,size*new Vector2(1.08f,.65f),.28f,rotation:65f,
                    fadeIn:.025f,fadeOutFrom:.60f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148);
            }
        }

        public static void CrystalWard(Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target);
            int layer=VfxSpace.SortingLayer(target);
            for(int i=0;i<3;i++)
            {
                Vector3 q=p+new Vector3((i-1)*.22f,i==1?.16f:.04f,0f)*c;
                Vector2 size=new Vector2(.09f,.14f)*c;
                SpriteFx.Play(VfxSpace.CrystalFacet,q,new Color(.08f,.14f,.22f,.47f),false,
                    size*1.30f,size*1.20f,.40f,rotation:(i-1)*20f,
                    fadeIn:.06f,fadeOutFrom:.62f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148);
                SpriteFx.Play(VfxSpace.CrystalFacet,q,new Color(.66f,.88f,.96f,.90f),false,
                    size,size*.95f,.40f,rotation:(i-1)*20f,
                    fadeIn:.06f,fadeOutFrom:.62f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+149);
            }
        }

        public static void MoonBlessing(Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(target)+new Vector3(.28f,.15f,0f)*c;
            int layer=VfxSpace.SortingLayer(target);
            HandpaintFx.PlayAt(p,layer,HandpaintFx.Impact1White,new Vector2(.30f,.30f),
                new Color(.75f,.79f,.98f,.18f),Color.clear,orderOffset:140,
                peakAlpha:.08f,glowAlpha:0f,desaturate:1f,calmLight:true);
            SpriteFx.Play(VfxSpace.BlessingMoon,p,new Color(.15f,.22f,.36f,.72f),false,
                .34f*c,.35f*c,.42f,rotation:-25f,fadeIn:.06f,fadeOutFrom:.62f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148);
            SpriteFx.Play(VfxSpace.BlessingMoon,p,new Color(.79f,.83f,1f,.94f),false,
                .28f*c,.29f*c,.42f,rotation:-25f,fadeIn:.06f,fadeOutFrom:.62f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+149);
            for(int i=0;i<2;i++)
            {
                Vector3 q=p+new Vector3(i==0?.17f:.12f,i==0?.11f:-.10f,0f)*c;
                float size=(i==0?.045f:.032f)*c;
                SpriteFx.Play(VfxArt.Glint,q,new Color(.23f,.32f,.50f,.64f),false,
                    size*1.35f,size*1.35f,.42f,fadeIn:.06f,fadeOutFrom:.62f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+150);
                SpriteFx.Play(VfxArt.Glint,q,new Color(.92f,.94f,1f,.94f),false,
                    size,size,.42f,fadeIn:.06f,fadeOutFrom:.62f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+151);
            }
        }

        // 용의 가슴 보석 대신 입가에서 공격 방향으로 흐르는 짧은 숨결을 보여준다.
        public static Vector3 DragonMouth(Unit caster)
        {
            var view=CardMotion.ViewOf(caster);
            if(view?.PortraitRenderer==null)return VfxSpace.PortraitPoint(caster);
            Bounds bounds=view.PortraitRenderer.bounds;
            return new Vector3(bounds.center.x+.03f*VfxSpace.CardSize,
                Mathf.Lerp(VfxSpace.PortraitHudTop(caster),bounds.max.y,.62f)-.055f*VfxSpace.CardSize,0f);
        }

        public static void DragonBreathPrepare(Unit caster,Unit target)
        {
            if(caster==null)return;
            float c=VfxSpace.CardSize;Vector3 mouth=DragonMouth(caster);
            VfxAim toward=VfxSpace.AimFrom(caster,target);
            VfxAim aim=new VfxAim(toward.Direction.x>=0f?Vector3.right:Vector3.left);
            int layer=VfxSpace.SortingLayer(caster);
            Vector3 tip=mouth+aim.Direction*.35f*c;
            Vector2 size=new Vector2(.56f,.18f)*c;
            SpriteFx.Play(VfxSpace.SmokeRibbon,tip,new Color(.08f,.14f,.24f,.46f),false,
                size*new Vector2(.50f,1.3f),size,.34f,rotation:aim.AngleDegrees,
                fadeIn:.10f,fadeOutFrom:.68f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+147);
            SpriteFx.Play(VfxSpace.SmokeRibbon,tip,new Color(.68f,.74f,1f,.96f),false,
                size*new Vector2(.50f,.80f),size*new Vector2(1f,.72f),.34f,rotation:aim.AngleDegrees,
                fadeIn:.10f,fadeOutFrom:.68f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148);
            // 리본만으로는 갈기 장식처럼 읽히므로 입에서 앞으로 뻗는 얇은 방향 코어를 분리한다.
            Vector2 jet=new Vector2(.60f,.16f)*c;
            SpriteFx.Play(VfxSpace.BreathCone,tip,new Color(.10f,.18f,.30f,.49f),false,
                jet*new Vector2(.60f,1.35f),jet*new Vector2(1f,1.15f),.30f,rotation:aim.AngleDegrees,
                fadeIn:.08f,fadeOutFrom:.70f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+151);
            SpriteFx.Play(VfxSpace.BreathCone,tip,new Color(.62f,.73f,.96f,.88f),false,
                jet*new Vector2(.60f,.75f),jet*new Vector2(1f,.72f),.30f,rotation:aim.AngleDegrees,
                fadeIn:.08f,fadeOutFrom:.70f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+152);
            SpriteFx.Play(VfxSpace.HardDot,mouth,new Color(.94f,.94f,.94f,.90f),false,
                .035f*c,.075f*c,.30f,fadeIn:.12f,fadeOutFrom:.68f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+150);
            for(int i=0;i<6;i++)
            {
                Vector3 origin=mouth+aim.Direction*(.10f+(i%3)*.06f)*c+Vector3.up*((i-2.5f)*.028f*c);
                var mote=SpriteFx.Play(VfxSpace.HardDot,origin,new Color(.83f,.86f,1f,.72f),false,
                    .045f*c,.024f*c,.27f,fadeIn:.02f,fadeOutFrom:.72f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+149);
                VfxRunner.Run(BreathMote(mote,origin,mouth));
            }
        }

        private static System.Collections.IEnumerator BreathMote(SpriteFx mote,Vector3 from,Vector3 to)
        {
            for(float age=0f;age<.27f;age+=Time.deltaTime)
            {
                if(mote!=null)mote.transform.position=Vector3.Lerp(from,to,Mathf.Clamp01(age/.27f));
                yield return null;
            }
        }

        // 중량 공격은 폭발 대신 손 가까운 작은 코어로 기운이 수렴한다.
        public static void HeavyPrepare(Unit caster)
        {
            if(caster==null)return;
            float c=VfxSpace.CardSize;Vector3 p=StatusMotifFx.Anchor(caster);
            int layer=VfxSpace.SortingLayer(caster);
            // 링 중앙은 완전히 비운다. 근백색도 자동 채색으로 크림색이 되지 않도록 무채색 코어를 쓴다.
            SpriteFx.Play(VfxSpace.Ring,p,new Color(.08f,.14f,.22f,.46f),false,
                .70f*c,.18f*c,.30f,fadeIn:.02f,fadeOutFrom:.70f,ease:SpriteFx.Ease.InCubic,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+147);
            SpriteFx.Play(VfxSpace.Ring,p,new Color(.78f,.88f,1f,.74f),false,
                .63f*c,.12f*c,.30f,fadeIn:.02f,fadeOutFrom:.70f,ease:SpriteFx.Ease.InCubic,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148);
            SpriteFx.Play(VfxSpace.HardDot,p,new Color(.94f,.94f,.94f,.92f),false,
                .06f*c,.095f*c,.22f,fadeIn:.02f,fadeOutFrom:.65f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+151);
            VfxRunner.Run(ChargeThreads(p,layer));
        }

        private static System.Collections.IEnumerator ChargeThreads(Vector3 center,int layer)
        {
            float c=VfxSpace.CardSize;var threads=new SpriteFx[4];var origins=new Vector3[4];
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1f:1f;Vector3 p=center+new Vector3(side*.26f,.07f,0f)*c;
                Vector2 size=new Vector2(.18f,.027f)*c;
                threads[i*2]=SpriteFx.Play(VfxSpace.ClawCue,p,new Color(.08f,.14f,.22f,.46f),false,
                    size*new Vector2(1f,1.7f),size*.4f,.26f,rotation:side*-18f,fadeIn:.04f,fadeOutFrom:.70f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+149);
                threads[i*2+1]=SpriteFx.Play(VfxSpace.ClawCue,p,new Color(.93f,.93f,.93f,.92f),false,
                    size,size*.3f,.26f,rotation:side*-18f,fadeIn:.04f,fadeOutFrom:.70f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+150);
                origins[i*2]=origins[i*2+1]=p;
            }
            for(float age=0f;age<.26f;age+=Time.deltaTime)
            {
                float t=Mathf.Clamp01(age/.26f);
                for(int i=0;i<threads.Length;i++)if(threads[i]!=null)threads[i].transform.position=Vector3.Lerp(origins[i],center,t);
                yield return null;
            }
        }

        // 범위기 예고: 피해나 상태를 만들지 않는 작은 충전 표식으로 전 대상의 몸통을 함께 짚는다.
        public static void VajraField(System.Collections.Generic.IReadOnlyList<Unit> targets)
        {
            if(targets==null)return;
            float c=VfxSpace.CardSize;
            foreach(Unit target in targets)
            {
                if(target==null||!target.isActive)continue;
                Vector3 p=StatusMotifFx.Anchor(target)+new Vector3(.24f,.04f)*c;
                int layer=VfxSpace.SortingLayer(target);
                SpriteFx.Play(VfxSpace.PressureRing,p,new Color(.18f,.25f,.37f,.78f),false,
                    Vector2.one*.30f*c,Vector2.one*.36f*c,.30f,
                    fadeIn:.05f,fadeOutFrom:.70f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+143);
                SpriteFx.Play(VfxSpace.PressureRing,p,new Color(.98f,.88f,.64f,.74f),false,
                    Vector2.one*.25f*c,Vector2.one*.30f*c,.30f,
                    fadeIn:.05f,fadeOutFrom:.70f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+144);
                SpriteFx.Play(VfxArt.Diamond,p,new Color(.18f,.25f,.37f,.62f),false,
                    new Vector2(.08f,.13f)*c,new Vector2(.11f,.17f)*c,.22f,
                    fadeIn:.07f,fadeOutFrom:.64f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+145);
                SpriteFx.Play(VfxArt.Diamond,p,new Color(.98f,.89f,.66f,.82f),false,
                    new Vector2(.055f,.09f)*c,new Vector2(.08f,.13f)*c,.22f,
                    fadeIn:.07f,fadeOutFrom:.64f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+146);
            }
        }

        // 무기는 시전자의 손에만 둔다. 적 몸통에는 세 갈래의 짧은 접촉 잔광을 남긴다.
        public static void VajraContact(Unit caster,Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;
            Vector3 p=StatusMotifFx.Anchor(target)+new Vector3(.20f,.08f,0f)*c;
            int layer=VfxSpace.SortingLayer(target);
            float angle=0f;
            Quaternion turn=Quaternion.Euler(0f,0f,angle);
            void Line(Vector2 a,Vector2 b,float width)
            {
                Vector2 d=b-a;Vector3 q=p+turn*(Vector3)((a+b)*.5f*c);
                Vector2 size=new Vector2(d.magnitude,width)*c;
                float rot=angle+Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg;
                SpriteFx.Play(VfxSpace.GlyphLine,q,new Color(.17f,.23f,.36f,.90f),false,
                    size*new Vector2(1.10f,1.85f),size*new Vector2(1.10f,1.85f),.25f,rotation:rot,
                    fadeIn:0f,fadeOutFrom:.56f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+148);
                SpriteFx.Play(VfxSpace.GlyphLine,q,new Color(1f,.94f,.76f,.96f),false,
                    size,size,.25f,rotation:rot,fadeIn:0f,fadeOutFrom:.56f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+149);
            }
            Line(new(-.06f,-.09f),new(.035f,.10f),.027f);
            Line(new(-.09f,-.03f),new(-.06f,.085f),.021f);
            Line(new(.045f,-.045f),new(.115f,.045f),.021f);
        }

        // 열화 부여의 정점은 공격용 짧은 잔광보다 오래 유지해 별도 상태로 읽힌다.
        public static void BlazeMark(Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;
            Vector3 p=StatusMotifFx.Anchor(target)+new Vector3(.32f,.25f,0f)*c;
            int layer=VfxSpace.SortingLayer(target);
            for(int i=0;i<3;i++)
            {
                Vector3 q=p+new Vector3((i-1)*.065f,i==1?.035f:0f,0f)*c;
                Vector2 size=new Vector2(.17f,.27f+(i==1?.06f:0f))*c;
                SpriteFx.Play(VfxSpace.FlameTongue,q,new Color(.18f,.22f,.32f,.76f),false,
                    size*1.22f,size*new Vector2(.95f,1.22f),.42f,rotation:(i-1)*-12f,
                    fadeIn:.04f,fadeOutFrom:.58f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+145);
                SpriteFx.Play(VfxSpace.FlameTongue,q,new Color(1f,.69f,.45f,.94f),false,
                    size,size*new Vector2(.85f,1.16f),.42f,rotation:(i-1)*-12f,
                    fadeIn:.04f,fadeOutFrom:.58f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+146);
                SpriteFx.Play(VfxSpace.FlameTongue,q-Vector3.up*.025f*c,new Color(1f,.96f,.77f,.95f),false,
                    size*new Vector2(.42f,.66f),size*new Vector2(.35f,.82f),.42f,
                    fadeIn:.04f,fadeOutFrom:.58f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+147);
            }
        }

        // 구매 원화의 잔광 위에 산호색 불꽃 끝과 작은 불씨를 남긴다. 전면 섬광은 사용하지 않는다.
        public static void FlameStrikeCore(Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 point=StatusMotifFx.Anchor(target)+new Vector3(.18f,.20f,0)*c;
            int layer=VfxSpace.SortingLayer(target);
            for(int i=0;i<3;i++)
            {
                float side=i-1;
                Vector3 origin=point+new Vector3(side*.065f,-.035f,0)*c;
                Vector2 from=new Vector2(.24f,.38f+(i==1?.08f:0f))*c;
                Vector2 to=from*new Vector2(.42f,1.40f);
                float life=.15f+i*.025f;
                SpriteFx tongue=SpriteFx.Play(VfxSpace.FlameLick,origin,new Color(1f,.60f,.28f,.40f),false,
                    from,to,life,side*-18f,spin:side*75f,fadeIn:0f,fadeOutFrom:.08f,ease:SpriteFx.Ease.Linear,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+69+i);
                VfxRunner.Run(FlameRise(tongue,origin,.13f*c,life));
            }
            SpriteFx.Play(VfxSpace.FlameLick,point,new Color(1f,.92f,.64f,.34f),true,
                new Vector2(.12f,.24f)*c,new Vector2(.045f,.37f)*c,.14f,rotation:-10f,
                fadeIn:0f,fadeOutFrom:.10f,ease:SpriteFx.Ease.Linear,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+73);
            for(int i=0;i<3;i++)
                VfxRunner.Run(FlameEmber(point+new Vector3((i-1)*.09f,.10f+i*.02f,0)*c,
                    target,.045f*c,.22f,.16f*c));
        }

        private static System.Collections.IEnumerator FlameRise(SpriteFx tongue,Vector3 origin,float rise,float life)
        {
            for(float age=0f;age<life&&tongue!=null;age+=Time.deltaTime)
            {tongue.transform.position=origin+Vector3.up*rise*Mathf.Clamp01(age/life);yield return null;}
        }

        public static void FlameCore(Unit target,float scale=1f,float life=.20f)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 point=StatusMotifFx.Anchor(target);
            Vector2 size=new Vector2(.30f,.36f)*c*scale;
            int layer=VfxSpace.SortingLayer(target);
            SpriteFx.Play(VfxSpace.FlameTongue,point,new Color(.86f,.32f,.20f,.76f),false,
                size*1.15f,size*1.23f,life,fadeIn:0f,fadeOutFrom:.58f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+69);
            SpriteFx.Play(VfxSpace.FlameTongue,point,new Color(1f,.69f,.31f,.88f),false,
                size,size*1.08f,life,fadeIn:0f,fadeOutFrom:.58f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+70);
            SpriteFx.Play(VfxSpace.FlameTongue,point+Vector3.down*.025f*c*scale,new Color(1f,.95f,.72f,.94f),false,
                size*new Vector2(.34f,.45f),size*new Vector2(.40f,.49f),life,fadeIn:0f,fadeOutFrom:.56f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+71);
            for(int i=0;i<3;i++)
            {
                Vector3 ember=point+new Vector3((i-1)*.085f,.18f+(i%2)*.055f,0)*c*scale;
                VfxRunner.Run(FlameEmber(ember,target,.018f*c*scale,life,.09f*c*scale));
            }
        }

        private static System.Collections.IEnumerator FlameEmber(Vector3 point,Unit target,float size,float life,float rise)
        {
            SpriteFx ember=SpriteFx.Play(VfxSpace.SoftDot,point,new Color(1f,.63f,.29f,.74f),false,
                size,size*.35f,life,fadeIn:0f,fadeOutFrom:.30f,
                sortingLayer:VfxSpace.SortingLayer(target),order:VfxSpace.OverlaySortingOrder+72);
            for(float age=0f;age<life&&ember!=null;age+=Time.deltaTime)
            {ember.transform.position=point+Vector3.up*rise*Mathf.Clamp01(age/life);yield return null;}
        }

        public static void FireContact(Unit target,bool sculptor)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 point=StatusMotifFx.Anchor(target);
            int layer=VfxSpace.SortingLayer(target);
            Vector2 extent=sculptor?new Vector2(.16f,.30f):new Vector2(.30f,.16f);
            float life=VfxTiming.AtLeastFrames(.18f,8);
            SpriteFx.Play(VfxArt.ImpactStar,point,new Color(.69f,.31f,.16f,.54f),false,
                extent*c,extent*1.25f*c,life,sculptor?0f:20f,fadeIn:0f,fadeOutFrom:.52f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+65);
            SpriteFx.Play(VfxArt.ImpactStar,point,new Color(1f,.89f,.63f,.96f),false,
                extent*.82f*c,extent*1.08f*c,life,sculptor?0f:20f,fadeIn:0f,fadeOutFrom:.50f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+66);
        }

        public static void LightningContact(Unit target)
        {
            if(target==null)return;
            float c=VfxSpace.CardSize;Vector3 point=StatusMotifFx.Anchor(target);
            int layer=VfxSpace.SortingLayer(target);
            float life=VfxTiming.AtLeastFrames(.20f,8);
            Vector2[] bolt={new(-.04f,.22f),new(.06f,.06f),new(-.06f,-.02f),new(.04f,-.18f)};
            void Stroke(Vector2 a,Vector2 b)
            {
                Vector2 delta=b-a;float angle=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
                Vector3 center=point+(Vector3)((a+b)*.5f*c);
                Vector2 size=new(delta.magnitude*c,.016f*c);
                SpriteFx.Play(VfxSpace.GlyphLine,center,new Color(.24f,.20f,.40f,.72f),false,
                    size*new Vector2(1f,1.7f),size*new Vector2(1f,1.7f),life,angle,
                    fadeIn:0f,fadeOutFrom:.50f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+70);
                SpriteFx.Play(VfxSpace.GlyphLine,center,new Color(.72f,.58f,.94f,.96f),false,
                    size,size,life,angle,fadeIn:0f,fadeOutFrom:.50f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+71);
            }
            for(int i=0;i<bolt.Length-1;i++)Stroke(bolt[i],bolt[i+1]);
            Stroke(bolt[1],new Vector2(.20f,.13f));
            SpriteFx.Play(VfxArt.ImpactStar,point,new Color(.72f,.58f,.94f,.82f),false,
                .12f*c,.24f*c,life,20f,fadeIn:0f,fadeOutFrom:.45f,
                sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+72);
        }

        public static void AquaSlash(Unit target,Color color,bool finisher)
        {
            if(target==null)return;
            Contact(target,color);
            if(!finisher)return;
            float c=VfxSpace.CardSize;Vector3 point=StatusMotifFx.Anchor(target);
            SpriteFx.Play(VfxArt.Crescent,point,new Color(.25f,.62f,.67f,.65f),false,
                new Vector2(.38f,.18f)*c,new Vector2(.58f,.25f)*c,.23f,-34f,18f,
                fadeIn:0f,fadeOutFrom:.56f,sortingLayer:VfxSpace.SortingLayer(target),order:VfxSpace.OverlaySortingOrder+65);
        }

        public static void Contact(Unit target, Color color)
        {
            if(target==null)return;
            SpriteFx.Play(VfxArt.ImpactStar,VfxSpace.PortraitPoint(target),new Color(.18f,.28f,.40f,.55f),false,
                .12f*VfxSpace.CardSize,.28f*VfxSpace.CardSize,.13f,fadeIn:0f,fadeOutFrom:.40f,
                sortingLayer:VfxSpace.SortingLayer(target),order:VfxSpace.OverlaySortingOrder+61);
            SpriteFx.Play(VfxArt.ImpactStar,VfxSpace.PortraitPoint(target),color,true,
                .10f*VfxSpace.CardSize,.25f*VfxSpace.CardSize,.13f,fadeIn:0f,fadeOutFrom:.40f,
                sortingLayer:VfxSpace.SortingLayer(target),order:VfxSpace.OverlaySortingOrder+62);
        }

        /// <summary>동시 일제사격에서도 얼음 파편과 월광의 실루엣을 구분한다. 판정은 원래 코드가 맡는다.</summary>
        public static void ElementContact(Unit target,string key)
        {
            if(target==null)return;
            float card=VfxSpace.CardSize;
            Vector3 point=StatusMotifFx.Anchor(target);
            int layer=VfxSpace.SortingLayer(target);
            if(key=="tsukuyomi.normal")
            {
                point+=new Vector3(.10f,.03f)*card;
                SpriteFx.Play(VfxArt.Crescent,point,new Color(.17f,.23f,.37f,.86f),false,
                    .32f*card,.38f*card,.26f,-25f,15f,fadeIn:0f,fadeOutFrom:.60f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+69,preserveInk:true);
                SpriteFx.Play(VfxArt.Crescent,point,new Color(.50f,.39f,.70f,.82f),false,
                    .28f*card,.34f*card,.26f,-25f,15f,fadeIn:0f,fadeOutFrom:.60f,
                    sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+70);
            }
            else if(key=="napoleon.normal")
            {
                point.x+=.10f*card;
                for(int i=0;i<2;i++)
                {
                    Vector3 at=point+Vector3.up*(i-.5f)*.07f*card;
                    Vector2 size=new(.32f*card,.060f*card);
                    SpriteFx.Play(VfxSpace.Crescent,at,new Color(.15f,.27f,.39f,.84f),false,
                        size*1.12f,size*1.25f,.24f,-16f,fadeIn:0f,fadeOutFrom:.56f,
                        sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+69,preserveInk:true);
                    SpriteFx.Play(VfxSpace.Crescent,at,new Color(.65f,.86f,.96f,.94f),false,
                        size,size*1.10f,.24f,-16f,fadeIn:0f,fadeOutFrom:.56f,
                        sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+70);
                }
            }
            else
            {
                point.x+=.08f*card;
                for(int i=0;i<6;i++)
                {
                    float angle=i*60f;
                    Vector3 ray=Quaternion.Euler(0f,0f,angle)*Vector3.up;
                    SpriteFx.Play(VfxArt.Diamond,point+ray*.12f*card,new Color(.17f,.26f,.39f,.86f),false,
                        new Vector2(.056f,.125f)*card,new Vector2(.066f,.15f)*card,.24f,angle,
                        fadeIn:0f,fadeOutFrom:.62f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+69,preserveInk:true);
                    SpriteFx.Play(VfxArt.Diamond,point+ray*.12f*card,new Color(.48f,.74f,.87f,.90f),false,
                        new Vector2(.045f,.11f)*card,new Vector2(.055f,.135f)*card,.24f,angle,
                        fadeIn:0f,fadeOutFrom:.62f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+70);
                }
            }
        }

        public static void CastRim(Unit caster,Unit target,Color color)
        {
            if(caster==null)return;
            var view=CardMotion.ViewOf(caster);
            if(view?.PortraitRenderer==null)return;
            Bounds bounds=view.PortraitRenderer.bounds;
            float c=VfxSpace.CardSize;
            float side=VfxSpace.AimFrom(caster,target).Direction.x>=0f?1f:-1f;
            Vector3 p=new(bounds.center.x+side*(bounds.extents.x+.08f*c),
                Mathf.Lerp(bounds.min.y,bounds.max.y,.48f),bounds.center.z);
            color.a=.52f;
            SpriteFx.Play(VfxArt.Crescent,p,color,false,new Vector2(.30f,.65f)*c,new Vector2(.36f,.70f)*c,
                .22f,side>0f?15f:165f,fadeIn:.05f,fadeOutFrom:.58f,
                sortingLayer:VfxSpace.SortingLayer(caster),order:VfxSpace.OverlaySortingOrder+56);
        }

        public static void SelfGuard(Unit caster,Unit target,bool book,Color? tint=null,bool instant=false,bool guard=false)
        {
            if(caster==null)return;
            Vector3 point=VfxSpace.CastPoint(caster,target);
            Color color=tint??(book?new Color(.73f,.86f,.66f,.88f):new Color(.61f,.83f,.90f,.88f));
            color.a=.88f;
            float c=VfxSpace.CardSize;
            if(book || guard)
            {
                // 새 HUD와 짧은 원화에서도 눈을 가로지르지 않도록 실제 칸의 손 옆 공간을 사용한다.
                float handSide = caster.IsEnemy ? -1f : 1f;
                if(guard)handSide=-handSide;
                point = VfxSpace.WorldPoint(caster) + Vector3.right * handSide * .50f * c;
                point.y = VfxSpace.PortraitHudTop(caster) + (guard?.12f:.23f) * c;
                float motifScale = .70f;
                int layer=VfxSpace.SortingLayer(caster);
                void Line(Vector2 a,Vector2 b)
                {
                    Vector2 delta=b-a;
                    Vector3 p=point+(Vector3)((a+b)*.5f*c*motifScale);
                    float rotation=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;
                    Vector2 size=new(delta.magnitude*c*motifScale,.014f*c*motifScale);
                    SpriteFx.Play(VfxSpace.GlyphLine,p,new Color(.10f,.22f,.24f,.48f),false,
                        size*new Vector2(1.04f,1.55f),size*new Vector2(1.06f,1.55f),.36f,rotation,
                        fadeIn:.08f,fadeOutFrom:.65f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+59);
                    SpriteFx.Play(VfxSpace.GlyphLine,p,book?new Color(.78f,.70f,.39f,.92f):new Color(.38f,.72f,.65f,.92f),false,
                        size*.95f,size,.36f,rotation,fadeIn:.08f,fadeOutFrom:.65f,
                        sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+60);
                }
                if(book)
                {
                    for(int page=-1;page<=1;page+=2)
                        SpriteFx.Play(VfxSpace.RoundedRect,point+Vector3.right*page*.075f*c,
                            new Color(.94f,.92f,.80f,.88f),false,
                            new Vector2(.13f,.19f)*c,new Vector2(.13f,.19f)*c,.36f,page*7f,
                            fadeIn:.08f,fadeOutFrom:.65f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+58);
                    Line(new(0,-.12f),new(0,.12f));
                    for(int side=-1;side<=1;side+=2)
                    {
                        Line(new(0,.12f),new(side*.21f,.17f));
                        Line(new(side*.21f,.17f),new(side*.21f,-.08f));
                        Line(new(side*.21f,-.08f),new(0,-.12f));
                        for(int i=0;i<3;i++)Line(new(side*.04f,.06f-i*.06f),new(side*.16f,.09f-i*.06f));
                    }
                }
                else
                {
                    SpriteFx.Play(VfxSpace.ShieldVeil,point,new Color(.65f,.87f,.88f,.46f),false,
                        new Vector2(.27f,.25f)*c,new Vector2(.27f,.25f)*c,.36f,
                        fadeIn:.08f,fadeOutFrom:.65f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+58);
                    Line(new(-.19f,.14f),new(.19f,.14f));
                    Line(new(.19f,.14f),new(.14f,-.06f));
                    Line(new(.14f,-.06f),new(0,-.18f));
                    Line(new(0,-.18f),new(-.14f,-.06f));
                    Line(new(-.14f,-.06f),new(-.19f,.14f));
                    SpriteFx.Play(VfxArt.Diamond,point,new Color(.22f,.46f,.48f,.84f),false,
                        new Vector2(.050f,.085f)*c,new Vector2(.050f,.085f)*c,.36f,
                        fadeIn:.08f,fadeOutFrom:.65f,sortingLayer:layer,order:VfxSpace.OverlaySortingOrder+61);
                }
                return;
            }
            SpriteFx.Play(book?VfxArt.RuneOrnate:VfxArt.RuneRadiant,point,color,false,
                new Vector2(.44f,.38f)*c,new Vector2(.52f,.44f)*c,.30f,fadeIn:instant?0f:.12f,fadeOutFrom:.65f,
                sortingLayer:VfxSpace.SortingLayer(caster),order:VfxSpace.OverlaySortingOrder+60);
            SpriteFx.Play(VfxSpace.Ring,point,color,false,new Vector2(.42f,.18f)*c,new Vector2(.46f,.20f)*c,
                .32f,fadeIn:instant?0f:.12f,fadeOutFrom:.60f,sortingLayer:VfxSpace.SortingLayer(caster),order:VfxSpace.OverlaySortingOrder+59);
        }
    }
}
