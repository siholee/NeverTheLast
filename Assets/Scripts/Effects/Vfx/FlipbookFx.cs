using System;
using System.Collections.Generic;
using UnityEngine;

namespace Effects.Vfx
{
    // 합성 실험 A/B/C는 같은 판정 프레임을 공유하고 시각 레이어만 바꾼다.
    /// <summary>플립북 한 겹의 합성·불투명도·형태 트윈을 함께 기술한다.</summary>
    public sealed class FlipbookCompositeStyle
    {
        public bool Enabled = true;
        public bool BlendFrames = true;
        public float BlendFraction = 0.62f;
        // 구매 명중만 한 패스에서 premultiplied 셀을 보간한다. 기존 합성은 기본값 false로 보존한다.
        public bool ContinuousBodyBlend;
        public int CoreLastFrame=6;
        public float SampleRate=60f;
        // 콜백 없는 지정 명중만 저배속 정점 뒤의 해체 단계를 렌더에서 건너뛰지 않는다.
        public int MaxCoreCellsPerUpdate;
        public bool SeedImpactProfile;
        public float PeakAlpha = 0.78f;
        public bool SoftPulse;
        public float FadeInFraction = 0.12f;
        public float FadeOutFraction = 0.26f;
        public Color BrightBackdropLift = new(1f, 0.91f, 0.72f, 1f);
        public float BrightBackdropLiftStrength = 0.34f;
        public float EdgeSoftness = 0.038f;
        public float AlphaCutoff;
        public float AlphaSoftness = 0.001f;
        public bool BlackMatte;
        public float Desaturate;
        public float PaintContrast;
        // HDR 베이크의 밝은 코어는 RGB 비율보다 보존한 광량 alpha로 분리한다.
        public float CoverageCoreStrength;
        public float PaletteWhiteMix = .65f;
        public float AlphaGain=1f;
        // 국소 접점의 먹선 전용. 0은 기존 모든 시트 합성과 같다.
        public float InkOutlineWidth;

        public Color GlowColor = new(1f, 0.82f, 0.40f, 1f);
        public float GlowAlpha = 0.66f;
        public float GlowThreshold = 0.70f;
        public float GlowSoftness = 0.16f;
        public float GlowGain = 1.28f;

        public float StartScale = 0.88f;
        public float CrestScale = 1.04f;
        public float EndScale = 1.10f;
        // 콜백 없는 장식만 정점을 앞당기고 짧게 회수한다. 기본값은 기존 시트와 같다.
        public float CrestAtFraction = .52f;
        public float ReleaseEndFraction = 1f;
        public Vector2 StartStretch = Vector2.one;
        public Vector2 CrestStretch = Vector2.one;
        public Vector2 EndStretch = Vector2.one;
        public float RotationDegrees;
        public float StartRotationDegrees;
        public bool AllowHighSpeedSkipping;
        // 콜백 없는 장식의 과장 셀만 최소 한 렌더 프레임 보장한다.
        public int RequiredVisibleFrame=-1;
        public Vector2 StartOffsetInCards;
        public Vector2 EndOffsetInCards;

        public Color GroundLightColor = new(1f, 0.72f, 0.28f, 0.22f);
        public Vector2 GroundLightSizeInCards = new(0.92f, 0.22f);
        public Color ContactShadowColor = new(0.20f, 0.30f, 0.38f, 0.22f);
        public Vector2 ContactShadowSizeInCards = new(0.82f, 0.16f);
        public float GroundOffsetInCards = -0.39f;

        public string PeakResourcePath;
        public float PeakStart = 0.32f;
        public float PeakHoldEnd = 0.66f;
        public float PeakEnd = 0.82f;
        public float PeakLayerAlpha = 0.74f;
        public float PeakLayerScale = 1f;

        public Color SceneLightColor = new(1f, 0.78f, 0.42f, 1f);
        public float SceneLightStrength = 0.16f;
    }

    /// <summary>
    /// 한 장의 행×열 시트를 프레임 애니메이션으로 재생한다.
    /// 몸체 알파와 휘도 임계 가산 코어를 분리하고 인접 프레임을 교차 합성한다. 큰 연출은
    /// 고해상도 정점 한 장을 짧게 겹쳐, 작은 시트 한 칸을 화면 전체로 늘릴 때 생기는 뭉개짐을 보완한다.
    /// </summary>
    public sealed class FlipbookFx : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite[]> Frames = new();
        private static readonly Dictionary<string, Sprite> Singles = new();
        private static readonly int BackdropLuminance = Shader.PropertyToID("_BackdropLuminance");
        private static readonly int PaletteLift = Shader.PropertyToID("_PaletteLift");
        private static readonly int LiftStrength = Shader.PropertyToID("_LiftStrength");
        private static readonly int EdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        private static readonly int AlphaCutoff = Shader.PropertyToID("_AlphaCutoff");
        private static readonly int AlphaSoftness = Shader.PropertyToID("_AlphaSoftness");
        private static readonly int Desaturate = Shader.PropertyToID("_Desaturate");
        private static readonly int UvRect = Shader.PropertyToID("_UvRect");
        private static readonly int GlowThreshold = Shader.PropertyToID("_GlowThreshold");
        private static readonly int GlowSoftness = Shader.PropertyToID("_GlowSoftness");
        private static readonly int GlowGain = Shader.PropertyToID("_GlowGain");

        private SpriteRenderer _renderer;
        private SpriteRenderer _nextRenderer;
        private SpriteRenderer _glowRenderer;
        private SpriteRenderer _nextGlowRenderer;
        private SpriteRenderer _peakRenderer;
        private SpriteRenderer _peakGlowRenderer;
        private SpriteRenderer _groundLight;
        private SpriteRenderer _contactShadow;
        private Sprite[] _frames;
        private float[] _delays;
        private Vector2 _size;
        private Color _baseColor;
        private int _frame;
        private float _frameAge;
        private float _completedDuration;
        private float _totalDuration;
        private float _coreEndAge;
        private bool _loop;
        private bool _finished;
        private Action<int> _onFrame;
        private Action _onComplete;
        private Func<Vector3> _follow;
        private Vector3 _followOffset;
        private Vector3 _anchor;
        private Vector3 _presentationOffset;
        private bool _lateFollow;
        private int _requiredVisibleFrame=-1;
        private bool _requiredFramePresented;
        private bool _requiredFrameRendered;
        private float _requiredHoldAge;
        internal int RequiredVisibleFrame => _requiredVisibleFrame;
        internal int DisplayedFrame => _frame;
        // 스타일의 코어 셀 끝을 따른다. 샘플 수가 다른 구매 시트도 장식 간 위상만 연결한다.
        internal bool ImpactCoreReleased => _finished || !_useComposite || _style==null || !_style.ContinuousBodyBlend || _frame>_style.CoreLastFrame;
        internal bool ContactLocked => _lateFollow;
        private FlipbookCompositeStyle _style;
        private bool _useComposite;
        private bool _usePeak;
        private float _bodyBlend,_coreRelease;
        private MaterialPropertyBlock _propertyBlock;
        private Entities.Unit _recipient;
        internal Entities.Unit Recipient => _recipient;

        /// <summary>실제 수신자를 기록한다. 전투 VFX는 카드 앞에서 원본 형상을 온전히 그린다.</summary>
        public FlipbookFx BindRecipient(Entities.Unit target)
        {
            _recipient = target;
            RefreshMaterialProperties();
            return this;
        }
        public FlipbookFx ScaleEnvelope(float start,float crest,float end)
        {
            if (_style != null) { _style.StartScale=start; _style.CrestScale=crest; _style.EndScale=end; }
            return this;
        }
        /// <summary>곡선 비행의 접선 방향을 합성 업데이트와 같은 회전에 반영한다.</summary>
        public FlipbookFx AimDegrees(float angle)
        {
            if(_style!=null)_style.StartRotationDegrees=angle;
            transform.localRotation=Quaternion.Euler(0f,0f,angle);
            return this;
        }
        public FlipbookFx LimitCoreSteps(int cells)
        {
            if(_style!=null)_style.MaxCoreCellsPerUpdate=Mathf.Max(0,cells);
            return this;
        }

        /// <summary>씨앗 명중의 짧고 작은 핵만 선택한다. 셀/콜백/총 수명은 바꾸지 않는다.</summary>
        public FlipbookFx UseSeedImpact()
        {
            if(_style!=null)_style.SeedImpactProfile=true;
            RefreshMaterialProperties();
            return this;
        }

        /// <summary>구매 시트의 형태를 유지하며 짧은 정점의 방향 강세를 조절한다.</summary>
        public FlipbookFx StretchEnvelope(Vector2 start,Vector2 crest,Vector2 end)
        {
            if(_style!=null){_style.StartStretch=start;_style.CrestStretch=crest;_style.EndStretch=end;}
            UpdatePresentation();
            return this;
        }
        private void RefreshMaterialProperties()
        {
            ApplyMaterialProperties(_renderer); ApplyMaterialProperties(_nextRenderer);
            ApplyMaterialProperties(_glowRenderer,true); ApplyMaterialProperties(_nextGlowRenderer,true);
            ApplyMaterialProperties(_peakRenderer); ApplyMaterialProperties(_peakGlowRenderer,true);
        }

        public bool IsFinished => _finished;
        public Transform FxTransform => transform;

        public static FlipbookFx Play(string resourcePath, int columns, int rows, Vector2 pivot,
            Vector3 position, Vector2 size, Color color, bool additive, bool loop, bool flipX,
            float[] frameDelays, int sortingLayer, int order, Action<int> onFrame = null,
            Action onComplete = null, FlipbookCompositeStyle style = null,
            SkillFxPlaybackMode playbackMode = SkillFxPlaybackMode.Final, bool reverseFrames = false, int startFrame = 0,
            int frameCount = 0)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            Sprite[] frames = FramesFor(resourcePath, texture, columns, rows, pivot);
            if (frames == null || frames.Length == 0) return null;
            // 장식 전용 셀 범위. 기본값은 기존 콜백·프레임 계약 전체를 그대로 재생한다.
            if (frameCount > 0 && frameCount < frames.Length)
            {
                var subset = new Sprite[frameCount];
                Array.Copy(frames, subset, frameCount);
                frames = subset;
            }
            if (reverseFrames)
            {
                // 캐시 배열은 다른 재생과 공유하므로 복사본만 뒤집어 정방향 부품에 영향을 주지 않는다.
                frames = (Sprite[])frames.Clone();
                Array.Reverse(frames);
            }

            var root = new GameObject("FlipbookFx_" + texture.name);
            root.transform.position = position;
            var fx = root.AddComponent<FlipbookFx>();
            fx._frames = frames;
            fx._delays = NormalizeDelays(frameDelays, frames.Length);
            fx._totalDuration = Sum(fx._delays);
            if(style?.ContinuousBodyBlend==true)
                for(int i=0;i<=Mathf.Clamp(style.CoreLastFrame,0,frames.Length-1);i++)fx._coreEndAge+=fx._delays[i];
            fx._size = size;
            fx._baseColor = color;
            fx._loop = loop;
            fx._onFrame = onFrame;
            fx._onComplete = onComplete;
            fx._anchor = position;
            fx._style = style;
            fx._useComposite = style != null && style.Enabled && playbackMode != SkillFxPlaybackMode.Legacy16;
            fx._requiredVisibleFrame=fx._useComposite&&onFrame==null&&onComplete==null
                ?Mathf.Clamp(style.RequiredVisibleFrame,-1,frames.Length-1):-1;
            if(fx._requiredVisibleFrame>=0)
            {
                Camera.onPostRender+=fx.ObserveRequiredFrame;
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering+=fx.ObserveRequiredFrameSrp;
            }
            fx._usePeak = fx._useComposite && playbackMode != SkillFxPlaybackMode.Blended16 &&
                          !string.IsNullOrWhiteSpace(style.PeakResourcePath);
            fx._propertyBlock = new MaterialPropertyBlock();

            fx._renderer = VfxSpace.NewSprite(root.transform, "Body", frames[0], color,
                sortingLayer, order, additive && !fx._useComposite);
            fx._renderer.flipX = flipX;
            if (fx._useComposite)
            {
                fx._renderer.sharedMaterial = style.ContinuousBodyBlend?VfxSpace.ContinuousImpact:VfxSpace.CompositeAlpha;
                fx._nextRenderer = VfxSpace.NewSprite(root.transform, "BodyBlend", frames[0], Color.clear,
                    sortingLayer, order + 1);
                fx._nextRenderer.sharedMaterial = VfxSpace.CompositeAlpha;
                fx._nextRenderer.flipX = flipX;
                if(style.ContinuousBodyBlend)fx._nextRenderer.enabled=false;

                fx._glowRenderer = VfxSpace.NewSprite(root.transform, "LuminanceCore", frames[0], Color.clear,
                    sortingLayer, order + 2);
                fx._glowRenderer.sharedMaterial = VfxSpace.LuminanceGlow;
                fx._glowRenderer.flipX = flipX;
                fx._nextGlowRenderer = VfxSpace.NewSprite(root.transform, "LuminanceCoreBlend", frames[0], Color.clear,
                    sortingLayer, order + 3);
                fx._nextGlowRenderer.sharedMaterial = VfxSpace.LuminanceGlow;
                fx._nextGlowRenderer.flipX = flipX;
                fx.BuildGrounding(sortingLayer, order);
                fx.BuildPeak(sortingLayer, order, flipX);
            }
            startFrame = Mathf.Clamp(startFrame, 0, frames.Length - 1);
            for (int i = 0; i < startFrame; i++) fx._completedDuration += fx._delays[i];
            fx.EnterFrame(startFrame);
            fx.UpdatePresentation();
            return fx;
        }

        /// <summary>움직이는 카드나 착탄점을 따라가야 하는 노드에 동적 앵커를 붙인다.</summary>
        public FlipbookFx Follow(Func<Vector3> anchor, Vector3 offset, bool lateFollow=false)
        {
            _follow = anchor;
            _followOffset = offset;
            _lateFollow = lateFollow;
            return this;
        }

        private void LateUpdate()
        {
            if(_finished||!_lateFollow||_follow==null||Time.timeScale<=0f)return;
            // 반동 코루틴의 회전·스쿼시가 끝난 실제 원화 UV를 렌더 직전에 다시 따른다.
            _anchor=_follow()+_followOffset;
            transform.position=_anchor+_presentationOffset;
            if(_recipient!=null)RefreshMaterialProperties();
        }

        private void Update()
        {
            if (_finished || Time.timeScale<=0f) return;
            if (_follow != null) _anchor = _follow() + _followOffset;

            float delta = Time.deltaTime;
            bool paceCore=_useComposite&&_style.ContinuousBodyBlend&&_style.MaxCoreCellsPerUpdate>0&&
                _onFrame==null&&_onComplete==null&&Effects.HitStop.PlaybackScale<=2.01f;
            int coreSteps=0;
            _frameAge += delta;
            if(_frame==_requiredVisibleFrame&&!_requiredFrameRendered&&_requiredHoldAge<.10f)
            {
                _requiredFramePresented=true;
                if(paceCore)_frameAge=Mathf.Min(_frameAge,DelayFor(_frame));
                _requiredHoldAge+=Time.unscaledDeltaTime;
                UpdatePresentation();
                return;
            }
            // 높은 배속에서도 호출 프레임을 건너뛰지 않는다. EnterFrame을 순서대로 거쳐
            // 피해 콜백은 지정한 칸에서 정확히 한 번 발생한다.
            int guard = _frames.Length + 1;
            while (!_finished && guard-- > 0 && _frameAge >= DelayFor(_frame))
            {
                _completedDuration += _delays[_frame];
                _frameAge -= DelayFor(_frame);
                int next = _frame + 1;
                if (next >= _frames.Length)
                {
                    if (_loop)
                    {
                        next = 0;
                        _completedDuration = 0f;
                    }
                    else
                    {
                        Complete();
                        break;
                    }
                }
                EnterFrame(next);
                if(!_requiredFramePresented&&_frame==_requiredVisibleFrame)
                {
                    // 긴 프레임에서도 과장 셀을 렌더 전에 지나치지 않는다. 남은 시간은 다음 Update로 넘긴다.
                    _requiredFramePresented=true;
                    if(paceCore)_frameAge=Mathf.Min(_frameAge,DelayFor(_frame));
                    break;
                }
                if(paceCore&&(_requiredFramePresented||_requiredVisibleFrame<0)&&_frame<=_style.CoreLastFrame&&
                    ++coreSteps>=_style.MaxCoreCellsPerUpdate)
                {
                    // 정점에서 밀린 시간을 다음 해체 셀에 한꺼번에 갚지 않는다. 게임 시계는 그대로다.
                    _frameAge=Mathf.Min(_frameAge,DelayFor(_frame));
                    break;
                }
            }
            if (!_finished) UpdatePresentation();
        }

        private void ObserveRequiredFrame(Camera camera)
        {
            if(camera==Camera.main&&_frame==_requiredVisibleFrame)_requiredFrameRendered=true;
        }
        private void ObserveRequiredFrameSrp(UnityEngine.Rendering.ScriptableRenderContext context,Camera camera)
            =>ObserveRequiredFrame(camera);

        private void EnterFrame(int frame)
        {
            _frame = Mathf.Clamp(frame, 0, _frames.Length - 1);
            _renderer.sprite = _frames[_frame];
            int next = _loop ? (_frame + 1) % _frames.Length : Mathf.Min(_frame + 1, _frames.Length - 1);
            if (_nextRenderer != null) _nextRenderer.sprite = _frames[next];
            if (_glowRenderer != null) _glowRenderer.sprite = _frames[_frame];
            if (_nextGlowRenderer != null) _nextGlowRenderer.sprite = _frames[next];
            ApplySize(_renderer, _renderer.sprite, _size);
            ApplySize(_nextRenderer, _nextRenderer != null ? _nextRenderer.sprite : null, _size);
            ApplySize(_glowRenderer, _glowRenderer != null ? _glowRenderer.sprite : null, _size);
            ApplySize(_nextGlowRenderer, _nextGlowRenderer != null ? _nextGlowRenderer.sprite : null, _size);
            ApplyMaterialProperties(_renderer);
            ApplyMaterialProperties(_nextRenderer);
            ApplyMaterialProperties(_glowRenderer, true);
            ApplyMaterialProperties(_nextGlowRenderer, true);
            _onFrame?.Invoke(_frame);
        }

        private void UpdatePresentation()
        {
            if (!_useComposite)
            {
                transform.position = _anchor;
                return;
            }

            // 고배속의 최소 표시 시간도 현재 프레임에 반영한다. 원래 길이만으로 나누면
            // 아직 남은 프레임이 있는데 진행률이 1이 되어 뒤쪽 그림이 사라진다.
            float authoredAge = _completedDuration + _delays[_frame] * Mathf.Clamp01(_frameAge / DelayFor(_frame));
            float progress = _totalDuration > 0.0001f ? Mathf.Clamp01(authoredAge / _totalDuration) : 1f;
            float alpha = EvaluateAlpha(progress) * _style.PeakAlpha;
            float backdrop = BattlefieldBackdrop.CurrentBackgroundLuminance;
            float backgroundRange = Smooth01(Mathf.InverseLerp(0.08f, 0.65f, backdrop));
            // 어두운 판은 이미 명도 대비가 충분하다. 몸체와 가산 코어를 함께 낮춰 흰 면이 뜨지 않게 한다.
            float bodyAdaptation = Mathf.Lerp(0.90f, 1f, backgroundRange);
            float glowAdaptation = Mathf.Lerp(0.76f, 1f, backgroundRange);
            float bodyAlpha = alpha * bodyAdaptation;
            float blend = 0f;
            if (_style.BlendFrames && _frame < _frames.Length - 1)
            {
                float inFrame = Mathf.Clamp01(_frameAge / DelayFor(_frame));
                float start = 1f - Mathf.Clamp(_style.BlendFraction, 0.05f, 0.95f);
                blend = Smooth01(Mathf.InverseLerp(start, 1f, inFrame));
            }

            _bodyBlend=blend;
            // 코어 마지막 셀은 release .62다. 완전 소멸(.8) 전에 실제 파편과 섞인다.
            float releaseEndAge=_style.ContinuousBodyBlend?.05f+Mathf.Max(.001f,_coreEndAge-.05f)/.62f:.35f;
            _coreRelease=Mathf.InverseLerp(.05f,releaseEndAge,authoredAge);
            SetRendererColor(_renderer, _baseColor, bodyAlpha * (_style.ContinuousBodyBlend?1f:1f-blend));
            SetRendererColor(_nextRenderer, _baseColor, _style.ContinuousBodyBlend?0f:bodyAlpha*blend);
            SetRendererColor(_glowRenderer, _style.GlowColor,
                alpha * glowAdaptation * _style.GlowAlpha * (1f - blend));
            SetRendererColor(_nextGlowRenderer, _style.GlowColor,
                alpha * glowAdaptation * _style.GlowAlpha * blend);

            float crestAt=Mathf.Clamp(_style.CrestAtFraction,.001f,.999f);
            float releaseEnd=Mathf.Clamp(_style.ReleaseEndFraction,crestAt+.001f,1f);
            float crest = Smooth01(Mathf.InverseLerp(0f, crestAt, progress));
            float release = Smooth01(Mathf.InverseLerp(crestAt, releaseEnd, progress));
            float scale = progress < crestAt
                ? Mathf.Lerp(_style.StartScale, _style.CrestScale, crest)
                : Mathf.Lerp(_style.CrestScale, _style.EndScale, release);
            Vector2 stretch=progress<crestAt?Vector2.Lerp(_style.StartStretch,_style.CrestStretch,crest)
                :Vector2.Lerp(_style.CrestStretch,_style.EndStretch,release);
            transform.localScale = new Vector3(Mathf.Max(.01f,scale*stretch.x),Mathf.Max(.01f,scale*stretch.y),Mathf.Max(.01f,scale));
            transform.localRotation = Quaternion.Euler(0f, 0f, _style.StartRotationDegrees + _style.RotationDegrees * progress);
            Vector2 offsetCards = Vector2.Lerp(_style.StartOffsetInCards, _style.EndOffsetInCards,
                Smooth01(progress));
            _presentationOffset=(Vector3)(offsetCards * VfxSpace.CardSize);
            transform.position = _anchor + _presentationOffset;

            float pulse = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            SetRendererColor(_groundLight, _style.GroundLightColor,
                _style.GroundLightColor.a * pulse * alpha * glowAdaptation);
            SetRendererColor(_contactShadow, _style.ContactShadowColor,
                _style.ContactShadowColor.a * pulse * bodyAlpha);
            UpdatePeak(progress, bodyAlpha, glowAdaptation);
            if (_recipient != null) RefreshMaterialProperties();
        }

        private void BuildGrounding(int layer, int order)
        {
            if (_style.GroundLightSizeInCards.sqrMagnitude > 0.0001f)
            {
                _groundLight = VfxSpace.NewSprite(transform, "GroundLightPool", VfxSpace.Glow,
                    Color.clear, layer, order - 5, true);
                _groundLight.transform.localPosition = Vector3.up * (_style.GroundOffsetInCards * VfxSpace.CardSize);
                _groundLight.transform.localScale = new Vector3(
                    _style.GroundLightSizeInCards.x * VfxSpace.CardSize,
                    _style.GroundLightSizeInCards.y * VfxSpace.CardSize, 1f);
            }
            if (_style.ContactShadowSizeInCards.sqrMagnitude > 0.0001f)
            {
                _contactShadow = VfxSpace.NewSprite(transform, "ContactShadow", VfxSpace.SoftDot,
                    Color.clear, layer, order - 6);
                _contactShadow.transform.localPosition = Vector3.up * (_style.GroundOffsetInCards * VfxSpace.CardSize);
                _contactShadow.transform.localScale = new Vector3(
                    _style.ContactShadowSizeInCards.x * VfxSpace.CardSize,
                    _style.ContactShadowSizeInCards.y * VfxSpace.CardSize, 1f);
            }
        }

        private void BuildPeak(int layer, int order, bool flipX)
        {
            if (!_usePeak) return;
            Sprite peak = Single(_style.PeakResourcePath);
            if (peak == null) return;
            Vector2 peakSize = _size * Mathf.Max(0.01f, _style.PeakLayerScale);
            _peakRenderer = VfxSpace.NewSprite(transform, "HighResolutionPeak", peak, Color.clear,
                layer, order + 4);
            _peakRenderer.sharedMaterial = VfxSpace.CompositeAlpha;
            _peakRenderer.flipX = flipX;
            _peakGlowRenderer = VfxSpace.NewSprite(transform, "HighResolutionPeakCore", peak, Color.clear,
                layer, order + 5);
            _peakGlowRenderer.sharedMaterial = VfxSpace.LuminanceGlow;
            _peakGlowRenderer.flipX = flipX;
            ApplySize(_peakRenderer, peak, peakSize);
            ApplySize(_peakGlowRenderer, peak, peakSize);
            ApplyMaterialProperties(_peakRenderer);
            ApplyMaterialProperties(_peakGlowRenderer, true);
        }

        private void UpdatePeak(float progress, float bodyAlpha, float glowAdaptation)
        {
            if (_peakRenderer == null) return;
            float fadeIn = Smooth01(Mathf.InverseLerp(_style.PeakStart,
                Mathf.Min(_style.PeakHoldEnd, _style.PeakStart + 0.12f), progress));
            float fadeOut = 1f - Smooth01(Mathf.InverseLerp(_style.PeakHoldEnd, _style.PeakEnd, progress));
            float weight = Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut));
            float peakAlpha = bodyAlpha * _style.PeakLayerAlpha * weight;
            SetRendererColor(_peakRenderer, _baseColor, peakAlpha);
            SetRendererColor(_peakGlowRenderer, _style.GlowColor,
                peakAlpha * glowAdaptation * _style.GlowAlpha);

            float breathe = Mathf.Lerp(0.94f, 1.035f,
                Smooth01(Mathf.InverseLerp(_style.PeakStart, _style.PeakEnd, progress)));
            _peakRenderer.transform.localScale = PeakScale(_peakRenderer, breathe);
            _peakGlowRenderer.transform.localScale = PeakScale(_peakGlowRenderer, breathe);
        }

        private Vector3 PeakScale(SpriteRenderer renderer, float multiplier)
        {
            if (renderer == null || renderer.sprite == null) return Vector3.one;
            Vector2 target = _size * Mathf.Max(0.01f, _style.PeakLayerScale);
            Vector2 bounds = renderer.sprite.bounds.size;
            return new Vector3(target.x / Mathf.Max(0.0001f, bounds.x) * multiplier,
                target.y / Mathf.Max(0.0001f, bounds.y) * multiplier, 1f);
        }

        private float EvaluateAlpha(float progress)
        {
            // 선택한 연출만 한 번 부드럽게 오르내린다. 판정 프레임과 재생 시간은 보존한다.
            if (_style.SoftPulse)
            {
                // 정점 근처의 형태를 유지한 뒤 잔광을 천천히 푼다. 재생/판정 프레임은 바꾸지 않는다.
                float attack = Smooth01(progress / Mathf.Max(.08f, _style.FadeInFraction));
                float release = 1f - Smooth01(Mathf.InverseLerp(.58f, 1f, progress));
                return Mathf.Min(attack, release);
            }
            float fadeIn = _style.FadeInFraction <= 0f
                ? 1f
                : Smooth01(progress / Mathf.Max(0.0001f, _style.FadeInFraction));
            float fadeOutStart = 1f - Mathf.Clamp01(_style.FadeOutFraction);
            float fadeOut = progress <= fadeOutStart
                ? 1f
                : 1f - Smooth01(Mathf.InverseLerp(fadeOutStart, 1f, progress));
            return Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut));
        }

        private void ApplyMaterialProperties(SpriteRenderer renderer, bool glow = false)
        {
            if (renderer == null || renderer.sprite == null || _style == null) return;
            renderer.GetPropertyBlock(_propertyBlock);
            Rect textureRect = renderer.sprite.textureRect;
            Texture2D texture = renderer.sprite.texture;
            _propertyBlock.SetVector(UvRect, new Vector4(
                textureRect.x / texture.width, textureRect.y / texture.height,
                textureRect.width / texture.width, textureRect.height / texture.height));
            _propertyBlock.SetFloat(EdgeSoftness, _style.EdgeSoftness);
            _propertyBlock.SetFloat(AlphaCutoff, _style.AlphaCutoff);
            _propertyBlock.SetFloat(AlphaSoftness, _style.AlphaSoftness);
            _propertyBlock.SetFloat("_BlackMatte",_style.BlackMatte?1f:0f);
            _propertyBlock.SetFloat(Desaturate, Mathf.Clamp01(_style.Desaturate));
            _propertyBlock.SetFloat("_PaintContrast",Mathf.Clamp01(_style.PaintContrast));
            _propertyBlock.SetFloat("_CoverageCoreStrength",Mathf.Clamp01(_style.CoverageCoreStrength));
            _propertyBlock.SetFloat("_AlphaGain",Mathf.Max(1f,_style.AlphaGain));
            if(_style.ContinuousBodyBlend&&renderer==_renderer&&!glow)
            {
                var next=_frames[Mathf.Min(_frame+1,_frames.Length-1)].textureRect;
                _propertyBlock.SetVector("_NextUvRect",new Vector4(next.x/texture.width,next.y/texture.height,next.width/texture.width,next.height/texture.height));
                _propertyBlock.SetFloat("_FrameBlend",_bodyBlend);
                _propertyBlock.SetFloat("_SeedProfile",_style.SeedImpactProfile?1f:0f);
                _propertyBlock.SetFloat("_PaletteWhiteMix",Mathf.Clamp01(_style.PaletteWhiteMix));
                _propertyBlock.SetFloat("_CoreRelease",_coreRelease);
                _propertyBlock.SetFloat("_CurrentCore",_frame<=_style.CoreLastFrame?1f:0f);
                _propertyBlock.SetFloat("_NextCore",_frame+1<=_style.CoreLastFrame?1f:0f);
            }
            _propertyBlock.SetFloat("_InkOutlineWidth",Mathf.Clamp(_style.InkOutlineWidth,0f,.08f));
            if (glow)
            {
                _propertyBlock.SetFloat(GlowThreshold, _style.GlowThreshold);
                _propertyBlock.SetFloat(GlowSoftness, _style.GlowSoftness);
                _propertyBlock.SetFloat(GlowGain, _style.GlowGain);
            }
            else
            {
                _propertyBlock.SetFloat(BackdropLuminance, BattlefieldBackdrop.CurrentBackgroundLuminance);
                _propertyBlock.SetColor(PaletteLift, _style.BrightBackdropLift);
                _propertyBlock.SetFloat(LiftStrength, _style.BrightBackdropLiftStrength);
            }
            renderer.SetPropertyBlock(_propertyBlock);
            _propertyBlock.Clear();
        }

        private float DelayFor(int frame)
        {
            float authored = _delays[Mathf.Clamp(frame, 0, _delays.Length - 1)];
            float speed=_style?.AllowHighSpeedSkipping==true?Mathf.Min(2f,Time.timeScale):Time.timeScale;
            return Mathf.Max(authored, speed / Mathf.Clamp(_style?.SampleRate??60f,60f,120f));
        }

        private void Complete()
        {
            if (_finished) return;
            _finished = true;
            Action complete = _onComplete;
            _onComplete = null;
            complete?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Camera.onPostRender-=ObserveRequiredFrame;
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=ObserveRequiredFrameSrp;
            // 씬 정리로 먼저 파괴돼도 대기 중인 SkillFx 코루틴이 영원히 남지 않게 한다.
            if (!_finished)
            {
                _finished = true;
                Action complete = _onComplete;
                _onComplete = null;
                complete?.Invoke();
            }
        }

        private static void SetRendererColor(SpriteRenderer renderer, Color rgb, float alpha)
        {
            if (renderer == null) return;
            renderer.color = new Color(rgb.r, rgb.g, rgb.b, Mathf.Clamp01(alpha));
        }

        private static void ApplySize(SpriteRenderer renderer, Sprite sprite, Vector2 size)
        {
            if (renderer == null || sprite == null) return;
            Vector2 bounds = sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                size.x / Mathf.Max(0.0001f, bounds.x),
                size.y / Mathf.Max(0.0001f, bounds.y), 1f);
        }

        private static Sprite Single(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Singles.TryGetValue(path, out Sprite cached)) return cached;
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture == null)
            {
                Singles[path] = null;
                return null;
            }
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), Mathf.Max(texture.width, texture.height), 0u, SpriteMeshType.FullRect);
            sprite.name = texture.name + "_Peak";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Singles[path] = sprite;
            return sprite;
        }

        private static Sprite[] FramesFor(string path, Texture2D texture, int columns, int rows, Vector2 pivot)
        {
            if (texture == null || columns <= 0 || rows <= 0) return null;
            string key = $"{path}|{columns}|{rows}|{pivot.x:0.###}|{pivot.y:0.###}";
            if (Frames.TryGetValue(key, out Sprite[] cached)) return cached;

            var frames = new Sprite[columns * rows];
            for (int index = 0; index < frames.Length; index++)
            {
                int column = index % columns;
                int rowFromTop = index / columns;
                int rowFromBottom = rows - 1 - rowFromTop;
                int x0 = Mathf.RoundToInt(column * texture.width / (float)columns);
                int x1 = Mathf.RoundToInt((column + 1) * texture.width / (float)columns);
                int y0 = Mathf.RoundToInt(rowFromBottom * texture.height / (float)rows);
                int y1 = Mathf.RoundToInt((rowFromBottom + 1) * texture.height / (float)rows);
                int width = Mathf.Max(1, x1 - x0);
                int height = Mathf.Max(1, y1 - y0);
                frames[index] = Sprite.Create(texture, new Rect(x0, y0, width, height), pivot,
                    Mathf.Max(width, height), 0u, SpriteMeshType.FullRect);
                frames[index].name = $"{texture.name}_{index:00}";
                frames[index].hideFlags = HideFlags.HideAndDontSave;
            }
            Frames[key] = frames;
            return frames;
        }

        private static float[] NormalizeDelays(float[] delays, int count)
        {
            var result = new float[count];
            for (int i = 0; i < count; i++)
            {
                float value = delays != null && delays.Length > 0
                    ? delays[Mathf.Min(i, delays.Length - 1)]
                    : 0.055f;
                result[i] = Mathf.Max(0.005f, value);
            }
            return result;
        }

        private static float Sum(float[] values)
        {
            float total = 0f;
            if (values != null)
                foreach (float value in values) total += value;
            return Mathf.Max(0.001f, total);
        }

        private static float Smooth01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
