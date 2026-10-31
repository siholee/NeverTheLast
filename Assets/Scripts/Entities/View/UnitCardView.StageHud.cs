using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Effects.Vfx;
using Managers;
using Managers;
using Managers.Stage;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Entities.View
{
    public sealed partial class UnitCardView
    {
        private bool _stageHudActive;
        private static StageFigureDataList _summonBadgeProfiles;
        private static string SummonBadge(string kind, int count)
        {
            var catalog = _summonBadgeProfiles ??= DataManager.LoadStageFigureDataList();
            string text = catalog.figures.FirstOrDefault(p => p.sprite == kind)?.summon?.badge ?? catalog.swarm.badge ?? "";
            return text.Replace("{n}",count.ToString());
        }
        private readonly List<Action> _restoreStageHud = new();
        private Image _stageBacking, _stagePortrait, _stageBorder, _stageOrbWell, _stageRowPlate, _stageSummonPlate, _stageGlow;
        private static Sprite _stageGlowSprite;
        private TextMeshPro _stageRow, _stageSummons;
        private static readonly Dictionary<Sprite, Sprite> StagePortraits = new();
        private static readonly Dictionary<string, Sprite> StageRings = new();
        public bool StageHudFocused => _stageHudActive && StageUnitFocus.IsFocused(_unit);
        public int StageHudOrder => _canvas != null ? _canvas.sortingOrder : 0;
        public Rect StageHudHitRect(Camera camera)
        {
            if (!_stageHudActive || !StageBattleHud.Visible || camera == null) return default;
            var corners = new Vector3[4]; ((RectTransform)_canvas.transform).GetWorldCorners(corners);
            Vector3 low = camera.WorldToScreenPoint(corners[0]), high = camera.WorldToScreenPoint(corners[2]);
            return Rect.MinMaxRect(low.x, low.y, high.x, high.y);
        }
        public Rect StageHudOrbScreenRect(Camera camera)
        {
            if(!_stageHudActive||!StageBattleHud.Visible||camera==null)return default;
            var corners=new Vector3[4];_ultRoot.GetWorldCorners(corners);
            Vector2 low=camera.WorldToScreenPoint(corners[0]),high=camera.WorldToScreenPoint(corners[2]);
            return Rect.MinMaxRect(low.x,low.y,high.x,high.y);
        }

        // Stage를 떠날 때 원래 부모·앵커·서체·스프라이트를 되돌린 뒤 Card 레이아웃을 적용한다.
        private void RememberStageHud()
        {
            var rects = new HashSet<RectTransform>(_canvas.GetComponentsInChildren<RectTransform>(true));
            rects.UnionWith(_orbCanvas.GetComponentsInChildren<RectTransform>(true));
            foreach (TextMeshPro text in new[] { _hpLabel, _resourceBadgeText, _strongShotText }) rects.Add(text.rectTransform);
            foreach (RectTransform rect in rects)
            {
                Transform parent = rect.parent;
                int sibling = rect.GetSiblingIndex();
                Vector2 min = rect.anchorMin, max = rect.anchorMax, pivot = rect.pivot, size = rect.sizeDelta;
                Vector3 pos = rect.localPosition, scale = rect.localScale;
                Quaternion rotation = rect.localRotation;
                bool active = rect.gameObject.activeSelf;
                _restoreStageHud.Add(() =>
                {
                    if (rect == null) return;
                    rect.SetParent(parent, false); rect.SetSiblingIndex(sibling);
                    rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.sizeDelta = size;
                    rect.localPosition = pos; rect.localScale = scale; rect.localRotation = rotation;
                    rect.gameObject.SetActive(active);
                });
            }
            foreach (Image image in _body.GetComponentsInChildren<Image>(true))
            {
                Sprite sprite = image.sprite; Image.Type type = image.type; Color color = image.color;
                bool enabled = image.enabled;
                var method = image.fillMethod; int origin = image.fillOrigin; bool clockwise = image.fillClockwise;
                _restoreStageHud.Add(() =>
                {
                    if (image == null) return;
                    image.sprite = sprite; image.type = type; image.color = color; image.enabled = enabled;
                    image.fillMethod = method; image.fillOrigin = origin; image.fillClockwise = clockwise;
                });
            }
            foreach (TextMeshPro text in new[] { _hpLabel, _resourceBadgeText, _strongShotText })
            {
                bool auto = text.enableAutoSizing, ortho = text.isOrthographic;
                var overflow = text.overflowMode;
                float size = text.fontSize, min = text.fontSizeMin, max = text.fontSizeMax;
                var alignment = text.alignment;
                _restoreStageHud.Add(() => { if (text == null) return; text.enableAutoSizing = auto;
                    text.fontSize = size; text.fontSizeMin = min; text.fontSizeMax = max; text.alignment = alignment;
                    text.isOrthographic = ortho; text.overflowMode = overflow; });
            }
        }

        private void RestoreStageHud()
        {
            if (!_stageHudActive) return;
            _stageHudActive = false;
            foreach (Action restore in _restoreStageHud) restore();
            _restoreStageHud.Clear();
            foreach (Image image in new[] { _stageBacking, _stagePortrait, _stageBorder, _stageOrbWell, _stageRowPlate, _stageSummonPlate, _stageGlow })
                if (image != null) Destroy(image.gameObject);
            if (_stageRow != null) Destroy(_stageRow.gameObject);
            if (_stageSummons != null) Destroy(_stageSummons.gameObject);
            _stageBacking = _stagePortrait = _stageBorder = null;
            _stageOrbWell = null;
            _stageRowPlate = null;
            _stageSummonPlate = null;
            _stageGlow = null;
            _stageRow = _stageSummons = null;
            _glassHeightInitialized = _hudLodInitialized = false;
            ApplyHudLod(_compactHud, true);
            ApplyGlassHeight(_dotStatuses.Count > 0);
            ApplySorting();
        }

        private void ConfigureStageHud()
        {
            if (!IsStage || _unit == null || !_combatHud) return;
            if (!_stageHudActive)
            {
                RememberStageHud();
                _stageHudActive = true;
                _canvas.transform.SetParent(transform, false);
                _orbCanvas.gameObject.SetActive(false);
                _ultRoot.SetParent(_canvas.transform, false);
                _resourceBadge.transform.SetParent(_canvas.transform, false);
                foreach (TextMeshPro label in new[] { _hpLabel, _resourceBadgeText, _strongShotText })
                    label.transform.SetParent(_canvas.transform, false);

                _stageBacking = NewImage(_canvas.transform, "StageHudBacking", Color.white);
                _stageBacking.sprite = UIShapes.RoundedRect(12, Color.white);
                _stageBacking.type = Image.Type.Sliced;
                UIBuild.Stretch(_stageBacking.rectTransform);
                _stageBacking.transform.SetAsFirstSibling();
                _stageGlow=NewImage(_canvas.transform,"StageReadyGlow",Color.clear);
                _stageGlow.sprite=StageCardGlow();
                Place(_stageGlow.rectTransform,-18,-18,154,186);
                _stageGlow.transform.SetAsFirstSibling();
                _stagePortrait = NewImage(_canvas.transform, "StagePortrait", Color.white);
                _stagePortrait.sprite = StagePortrait(Resources.Load<Sprite>(_unit.PortraitPath));
                Place(_stagePortrait.rectTransform, 0f, 0f, 118f, 112f);
                _stagePortrait.transform.SetSiblingIndex(1);
                _stageRowPlate = NewImage(_canvas.transform, "StageRowBadgeBack", new Color(14f/255f,22f/255f,40f/255f,.7f));
                _stageRowPlate.sprite = UIShapes.RoundedRect(4, Color.white);
                _stageRowPlate.type = Image.Type.Sliced;
                Place(_stageRowPlate.rectTransform, _unit.IsEnemy ? 6f : 74f, 6f, 38f, 18f);
                _stageRow = NewLabel("StageRowBadge", FontStyles.Bold, TextAlignmentOptions.Center);
                _stageRow.transform.SetParent(_canvas.transform, false);
                _stageRow.outlineWidth = .25f;
                _stageSummons = NewLabel("StageSummonBadge", FontStyles.Bold, TextAlignmentOptions.Center);
                _stageSummons.transform.SetParent(_canvas.transform, false);
                _stageSummonPlate = NewImage(_canvas.transform,"StageSummonBadgeBack",new Color(.004f,.009f,.022f,.97f));
                _stageSummonPlate.sprite = UIShapes.RoundedRect(5,Color.white);
                _stageSummonPlate.type = Image.Type.Sliced;
                Place(_stageSummonPlate.rectTransform,0f,-34f,118f,18f);
                _stageBorder = NewImage(_canvas.transform, "StageHudBorder", Color.clear);
                _stageBorder.sprite = UIShapes.RoundedRect(12, Color.clear, Color.white, 1);
                _stageBorder.type = Image.Type.Sliced;
                UIBuild.Stretch(_stageBorder.rectTransform);

                _bottomHudRoot.localScale = Vector3.one;
                Place(_bottomHudRoot, 0f, 0f, 118f, 150f);
                Place((RectTransform)_bottomGlass.transform, 7f, 119f, 104f, 24f);
                _bottomGlass.GetComponent<Image>().enabled = false;
                _glassTint.enabled = false;
                _bottomGlassShadow.SetActive(false);
                _actingOutline.enabled = false;
                _actionTrack.gameObject.SetActive(false);
                Place(_hpTrack.rectTransform, 0f, 17f, 104f, 6f);
                _hpTrack.sprite = UIShapes.RoundedRect(3, new Color(0f, 0f, 0f, .45f));
                _hpDamageTrail.color = new Color(.96f, .97f, .99f, 1f);
                Place(_toughnessTrack.rectTransform, 0f, 25f, 104f, 2f);
                Place(_ultRoot, _unit.IsEnemy ? 86f : -10f, -12f, 42f, 42f);
                _ultRoot.localScale = Vector3.one;
                foreach (Transform child in _ultRoot)
                    if (child.name is not ("Outline" or "Mark" or "ResourceBadge") && !child.name.StartsWith("TreasureBead"))
                        child.gameObject.SetActive(false);
                _ultFill.transform.SetParent(_ultRoot, false);
                _ultFill.gameObject.SetActive(true);
                _ultFill.transform.SetSiblingIndex(_ultOutline.transform.GetSiblingIndex() + 1);
                UIBuild.Stretch(_ultFill.rectTransform);
                _ultFill.type = Image.Type.Filled;
                _ultFill.fillMethod = Image.FillMethod.Radial360;
                _ultFill.fillOrigin = (int)Image.Origin360.Top;
                _ultFill.fillClockwise = true;
                _ultOutline.color = new Color(1f, 1f, 1f, .22f);
                _ultMark.gameObject.SetActive(true);
                Place(_ultMark.rectTransform,14f,14f,14f,14f);
                _ultOutline.gameObject.SetActive(true);
                _stageOrbWell = NewImage(_ultRoot, "StageOrbWell", new Color(.025f,.04f,.075f,.98f));
                _stageOrbWell.sprite = UIShapes.Disc(64, Color.white);
                Place(_stageOrbWell.rectTransform, 6f, 6f, 30f, 30f);
                _stageOrbWell.transform.SetAsFirstSibling();
                Place(_resourceBadge.rectTransform, 0f, 0f, 42f, 14f);
                _visibleDotSlots = 3;
                for (int i = 0; i < MaxDots; i++)
                {
                    Place(_dotBacks[i].rectTransform, 60f + i * 15f, 0f, 14f, 14f);
                    UIBuild.Stretch(_dots[i].rectTransform, 2f, 2f);
                }
                for(int i=0;i<_elementBadges.Length;i++)
                {
                    _elementBadges[i].gameObject.SetActive(true);
                    Place(_elementBadges[i].rectTransform,7f+i*18f,75f,14f,14f);
                }
            }
            UpdateStageHud();
            ApplySorting();
        }

        private void ApplyStageHudSorting(int order)
        {
            _stageFigure?.ApplyDepth(order, _layerId);
            // 고정 화면 HUD는 모든 전장 VFX보다 앞, ScreenSpaceOverlay 크롬·컷인보다 뒤다.
            int layer = SortingLayer.NameToID("CombatVfx");
            _canvas.sortingLayerID = layer; _canvas.sortingOrder = 30000;
            foreach (TextMeshPro label in new[] { _hpLabel, _resourceBadgeText, _strongShotText, _stageRow, _stageSummons })
            {
                var renderer = label.GetComponent<MeshRenderer>();
                renderer.sortingLayerID = layer; renderer.sortingOrder = 30001;
            }
        }

        private void UpdateStageHud()
        {
            if (!_stageHudActive) return;
            bool visible = StageBattleHud.Visible;
            _canvas.gameObject.SetActive(visible);
            if (!visible) return;
            // Grid 재배치의 Card용 정보판 축소가 들어와도 화면 카드의 체력·상태 행은 고정한다.
            _bottomHudRoot.localScale = Vector3.one;
            Place(_bottomHudRoot,0f,0f,118f,150f);
            float px = StageLayout.WorldHeight / StageLayout.HeightPx;
            bool focus = StageUnitFocus.IsFocused(_unit);
            bool acting = ReferenceEquals(GameManager.Instance?.ActionScheduler?.ActingUnit, _unit) || _unit.isCasting;
            _resourceBadgeText.fontSize = 160f;
            _resourceBadgeText.isOrthographic = false;
            var rect = (RectTransform)_canvas.transform;
            rect.pivot = new Vector2(.5f, 1f); rect.sizeDelta = new Vector2(118f, 150f);
            Vector2 pixel = StageBattleHud.Current.Position(_unit);
            pixel.y -= acting ? 14f : _ultReady ? 8f : 0f;
            StageBattleHud.Current.Track(_unit, _canvas, acting ? 14f : _ultReady ? 8f : 0f);
            rect.position = StageLayout.PixelToWorld(pixel, Camera.main);
            // SD 피격·돌진·hover 모션에서 독립시켜 카드 크기·화면 위치를 고정한다.
            rect.rotation = Quaternion.identity;
            Vector3 parent = rect.parent.lossyScale;
            rect.localScale = new Vector3(px / parent.x, px * StageBattleHud.Current.HeightScale(_unit) / parent.y, px / parent.z);
            Color backing = _unit.IsEnemy ? new Color(48f/255f,18f/255f,30f/255f,.84f)
                : new Color(14f/255f,22f/255f,40f/255f,.82f);
            _stageBacking.color = backing;
            _stageGlow.color=acting||focus?Color.white:_ultReady?ElementPalette.ColorFor(_unit.Element):Color.clear;
            _stageBorder.sprite = UIShapes.RoundedRect(12,Color.clear,Color.white,focus||acting||_ultReady?2:1);
            _stageBorder.color = focus || acting ? Color.white : _ultReady ? ElementPalette.ColorFor(_unit.Element) : new Color(1f, 1f, 1f, .14f);
            StageLabel(_stageRow, _unit.IsEnemy ? 6f : 74f, 6f, 38f, 18f, 110f);
            _stageRow.color = new Color(230f/255f,236f/255f,245f/255f);
            _stageRow.isOrthographic = false;
            _stageRow.text = StageBattleHud.Current.Column(_unit) == 0 ? "전열" : "후열";
            var summons = GridManager.Instance.summonList.Where(s => s != null && s.isActive && s.SummonOwner == _unit).ToArray();
            StageLabel(_stageSummons, 0f, -34f, 118f, 18f, 110f);
            _stageSummons.isOrthographic = false;
            _stageSummons.text = summons.Length == 0 ? "" : SummonBadge(
                summons[0].GetComponent<SummonCardView>()?.StageVisual?.Kind ?? summons[0].SummonKind,summons.Length);
            _stageSummonPlate.enabled = summons.Length > 0;
            if (_unit.GetCombatResourceMaximum(Codes.Passive.OrionSirius.ResourceId) > 0)
            {
                int charges = _unit.GetCombatResource(Codes.Passive.OrionSirius.ResourceId);
                _stageSummons.text = SummonBadge("sirius",1) + " " + string.Concat(Enumerable.Range(0, 5)
                    .Select(i => i < charges ? "<color=#FFC836>◆</color>" : "<color=#526078>◆</color>"));
                _stageSummonPlate.enabled = true;
            }
            _hpLabel.rectTransform.localRotation = _resourceBadgeText.rectTransform.localRotation = Quaternion.identity;
            // 큰 체력 수도 원문을 유지한다. 숫자가 길면 상태를 한 줄 위로 옮겨 104px를 확보한다.
            bool wideHp = _hpLabel.GetPreferredValues(_hpLabel.text).x > 58f;
            StageLabel(_hpLabel, 7f, 117f, wideHp ? 104f : 58f, 18f, 16f);
            if(wideHp) { _hpLabel.enableAutoSizing=true;_hpLabel.fontSizeMin=14f; }
            for(int i=0;i<MaxDots;i++) Place(_dotBacks[i].rectTransform,60f+i*15f,wideHp?-20f:0f,14f,14f);
            for(int i=0;i<_elementBadges.Length;i++) Place(_elementBadges[i].rectTransform,7f+i*18f,wideHp?55f:75f,14f,14f);
            _hpLabel.transform.localScale = Vector3.one;
            _shieldFill.color = new Color(.94f, .79f, .42f, 1f);
            _shieldFill.rectTransform.anchorMin = new Vector2(0f, 1f);
            _shieldFill.rectTransform.anchorMax = new Vector2(_shieldFill.rectTransform.anchorMax.x, 1f);
            _shieldFill.rectTransform.pivot = new Vector2(0f, 0f);
            _shieldFill.rectTransform.offsetMin = new Vector2(0f, 2f);
            _shieldFill.rectTransform.offsetMax = new Vector2(0f, 4f);
            _hpFill.color = _unit.IsEnemy ? new Color(.94f, .42f, .47f) : new Color(.22f, .86f, .76f);

            // RefreshResourceBadge가 선택한 문구·값을 카드 내부 한 줄에 그대로 표시한다.
            StageLabel(_resourceBadgeText, 4f, wideHp ? 74f : 94f, 110f, 20f, 160f);
            _resourceBadgeText.isOrthographic = false;
            _resourceBadgeText.alignment = TextAlignmentOptions.Center;
            Place(_resourceBadge.rectTransform, 4f, wideHp ? 74f : 94f, 110f, 20f);
            _resourceBadge.transform.localScale = Vector3.one;
            _suryaResourcePlate.enabled = false;
            if (_resourceBadgeText.enabled) _resourceBadge.enabled = true;
            StageLabel(_strongShotText, 66f, wideHp ? 55f : 75f, 46f, 16f, 110f);
            _strongShotText.isOrthographic = false;
            var kind = _unit.UltimateResourceType;
            int segments = kind == BaseEnums.UltimateResourceType.Passive ? 21 : kind == BaseEnums.UltimateResourceType.Stack ? Mathf.Max(1, _unit.ManaMax) : 1;
            int filled = Mathf.RoundToInt(_ultFillAmount * segments);
            Sprite ring = StageRing(segments, kind == BaseEnums.UltimateResourceType.Passive);
            _ultOutline.sprite = _ultFill.sprite = ring;
            _ultOutline.color = new Color(1f,1f,1f,.22f);
            _ultMark.color = ElementPalette.ColorFor(_unit.Element);
            _ultMark.sprite = StatusIcons.Element(ElementPalette.Parse(_unit.Element));
            _ultFill.color = ElementPalette.ColorFor(_unit.Element);
            _ultFill.fillAmount = kind == BaseEnums.UltimateResourceType.Passive ? 1f : kind == BaseEnums.UltimateResourceType.Stack ? (float)filled / segments : _ultFillAmount;
            _group.alpha = 1f;
        }

        private static void StageLabel(TextMeshPro label, float x, float y, float width, float height, float font)
        {
            Place(label.rectTransform, x, y, width, height);
            Vector3 position = label.rectTransform.localPosition; position.z = -.02f;
            label.rectTransform.localPosition = position;
            label.isOrthographic = true;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = false; label.fontSizeMin = font - 2f; label.fontSizeMax = font;
            label.fontSize = font; label.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private static Sprite StagePortrait(Sprite source)
        {
            if (source == null) return null;
            if (StagePortraits.TryGetValue(source, out var cached)) return cached;
            var rt = RenderTexture.GetTemporary(source.texture.width, source.texture.height, 0);
            var active = RenderTexture.active;
            Graphics.Blit(source.texture, rt); RenderTexture.active = rt;
            var read = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); read.Apply();
            RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt);
            var texture = new Texture2D(118, 112, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            Rect area = source.textureRect;
            float scale = Mathf.Max(118f / area.width, 112f / area.height);
            float cropX = (area.width - 118f / scale) * .5f, cropY = (area.height - 112f / scale) * .14f;
            for (int y = 0; y < 112; y++) for (int x = 0; x < 118; x++)
            {
                Color color = read.GetPixelBilinear((area.x + cropX + x / scale) / read.width,
                    (area.yMax - cropY - (111f - y) / scale) / read.height);
                color.a *= Mathf.Clamp01(y / (112f * .3f));
                if (y > 100 && (x < 12 || x > 105))
                    color.a *= Mathf.Clamp01(12.5f - Vector2.Distance(new Vector2(x, y), new Vector2(x < 12 ? 12 : 105, 100)));
                texture.SetPixel(x, y, color);
            }
            Destroy(read); texture.Apply();
            return StagePortraits[source] = Sprite.Create(texture, new Rect(0, 0, 118, 112), new Vector2(.5f, .5f), 100f);
        }

        private static Sprite StageRing(int count, bool dotted)
        {
            string key = $"{count}:{dotted}";
            if (StageRings.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                Vector2 p = new(x - 63.5f, y - 63.5f);
                float angle = Mathf.Repeat(Mathf.Atan2(p.x, p.y) / (2f * Mathf.PI), 1f);
                float r = p.magnitude;
                // SVG 42px의 r18·선3(상시는1.8)과 둘레상의 간격3px/점선2:3.5.
                float alpha = Mathf.Clamp01((dotted ? 1.8f : 3f) * 128f / 84f + .5f - Mathf.Abs(r - 18f * 128f / 42f));
                float on = dotted ? 2f / 5.5f : 1f - 3f * count / (2f * Mathf.PI * 18f);
                if (count > 1 && Mathf.Repeat(angle * count, 1f) > Mathf.Clamp01(on)) alpha = 0f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0f, 0f, 128f, 128f), new Vector2(.5f, .5f), 128f);
            StageRings[key] = sprite;
            return sprite;
        }

        private static Sprite StageCardGlow()
        {
            if(_stageGlowSprite!=null)return _stageGlowSprite;
            var texture=new Texture2D(154,186,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
            for(int y=0;y<186;y++)for(int x=0;x<154;x++)
            {
                Vector2 q=new(Mathf.Abs(x-76.5f)-47f,Mathf.Abs(y-92.5f)-63f);
                float distance=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-12f;
                float alpha=distance>0?Mathf.Exp(-distance*distance/50f)*.5f:0;
                texture.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            texture.Apply();return _stageGlowSprite=Sprite.Create(texture,new Rect(0,0,154,186),new Vector2(.5f,.5f),100);
        }
    }
}
