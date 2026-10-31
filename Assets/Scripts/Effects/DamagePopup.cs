using System.Collections.Generic;
using Entities;
using System.Linq;
using Managers.UI.Core;
using Managers.UI.Theme;
using TMPro;
using UnityEngine;

namespace Effects
{
    public enum CombatNumberKind { Damage, Shielded, Heal, Miss, Nullified, EffectResisted, HealthCost, Poison }

    /// <summary>화면 크기를 기준으로 찍히는 전투 숫자. 배속과 무관하게 머물며 짧게 튀고 떠오른다.</summary>
    [DisallowMultipleComponent]
    public sealed class DamagePopup : MonoBehaviour
    {
        private const int MaxConcurrent = 24;
        private const float Lifetime = 1.12f;
        private static readonly List<DamagePopup> Live = new();
        private static Material _numberMaterial;
        private static Material _stageNumberMaterial, _stageCriticalMaterial;
        private bool _stageNumber;
        private Unit _target;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _lastAnchor, _offset;
        private Vector2 _placementHalf=new(100,55);
        private float _age, _scale;
        private float _rise=32f, _popStrength=.18f;
        private int _slot;
        private int _actionId=-1, _amount, _hitCount=1;
        private bool _critical, _counter;
        private CombatNumberKind _kind;
        private TextMeshProUGUI _label, _caption;
        private string _captionPrefix;
        internal Unit Recipient => _target;
        internal int ActionId => _actionId;
        internal int DisplayedAmount => _amount;
        internal int HitCount => _hitCount;
        internal Rect OccupiedRect => new(_rect.anchoredPosition-_placementHalf,_placementHalf*2f);

        internal static void ShowPoison(Unit target, bool refresh)
        {
            if(target==null)return;
            Live.RemoveAll(item=>item==null);
            var popup=Live.Find(item=>item._target==target && item._kind==CombatNumberKind.Poison);
            if(popup==null)
            {
                Show(target,0,false,Vfx.ElementPalette.ColorFor(BaseClasses.BaseEnums.UnitElement.Dendro),CombatNumberKind.Poison);
                popup=Live.FindLast(item=>item._target==target && item._kind==CombatNumberKind.Poison);
            }
            if(popup==null)return;
            popup._age=0f;popup._label.text=refresh?"맹독 · 갱신":"맹독";
            popup._placementHalf.x=Mathf.Max(44f,popup._label.GetPreferredValues(popup._label.text).x*.5f+8f);
            popup._offset=popup.FindClearOffset(new Vector2(0,Vfx.CardMotion.ViewOf(target)?.StageProxy != null ? 0f : 28f),popup._placementHalf);
            popup.Frame();
        }

        private Vector2 Anchor()
        {
            if (Vfx.CardMotion.ViewOf(_target)?.StageProxy != null)
                return CombatFeedbackSurface.Position(_target, _kind == CombatNumberKind.Poison);
            if(_kind!=CombatNumberKind.Poison)return CombatFeedbackSurface.Position(_target,true);
            Camera camera=Camera.main;
            if(camera==null)return _lastAnchor;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(CombatFeedbackSurface.Root,
                camera.WorldToScreenPoint(Vfx.VfxSpace.ContactPoint(_target)),null,out Vector2 local);
            return local;
        }

        public static void Show(Unit target, int amount, bool isCrit, Color accent,
            CombatNumberKind kind = CombatNumberKind.Damage, float weight = 0f, bool counter = false, int actionId = -1)
        {
            if (target == null) return;
            if (amount <= 0 && kind is CombatNumberKind.Damage or CombatNumberKind.Heal or CombatNumberKind.Shielded) return;
            bool critical = isCrit && kind == CombatNumberKind.Damage;
            if (critical) Managers.AudioManager.PlayCue("common.crit");
            Live.RemoveAll(item => item == null);
            // 한 행동의 연타는 표적별 누계로 갱신한다. 실제 피해/판정/타격 효과는 합치지 않는다.
            // 행동 ID가 없는 직접 미리보기와 DOT는 각 숫자를 그대로 보존한다.
            if(actionId>=0 && kind is CombatNumberKind.Damage or CombatNumberKind.Shielded)
            {
                DamagePopup burst=Live.Find(item=>item._target==target&&item._actionId==actionId&&
                    item._kind==kind&&item._critical==critical&&item._counter==counter);
                if(burst!=null){burst.Append(amount);return;}
            }
            // 맹독 글자가 기존 피해 숫자의 표시 한도를 차지해 숫자를 밀어내지 않는다.
            bool poison=kind==CombatNumberKind.Poison;
            bool SameBudget(DamagePopup item)=>(item._kind==CombatNumberKind.Poison)==poison;
            while (Live.Count(SameBudget) >= MaxConcurrent)
            { var first = Live.Find(SameBudget); Live.Remove(first); if(first != null) Destroy(first.gameObject); }
            var used = new HashSet<int>();
            foreach (DamagePopup other in Live) if (other._target == target && SameBudget(other)) used.Add(other._slot);
            if (used.Count >= 4)
            {
                DamagePopup oldest = Live.Find(item => item._target == target && SameBudget(item));
                if (oldest != null)
                { used.Remove(oldest._slot); Live.Remove(oldest); oldest.gameObject.SetActive(false); Destroy(oldest.gameObject); }
            }
            int stack = 0; while (used.Contains(stack)) stack++;
            var rect = UIBuild.Container("DamagePopup", CombatFeedbackSurface.Root);
            var popup = rect.gameObject.AddComponent<DamagePopup>();
            popup._actionId=actionId;popup._amount=amount;popup._kind=kind;popup._critical=critical;popup._counter=counter;
            popup.Build(target, amount, critical, accent, kind, Mathf.Clamp01(weight), stack % 4,counter);
            Live.Add(popup);
        }
        private void Build(Unit target, int amount, bool critical, Color accent, CombatNumberKind kind, float weight, int stack,bool counter)
        {
            _target = target; _slot = stack;
            _stageNumber=Managers.Stage.VisualModeState.Current==Managers.Stage.VisualMode.Stage;
            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(.5f,.5f);
            _rect.sizeDelta = new Vector2(186,82);
            _group = gameObject.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false;
            _lastAnchor = Anchor();
            // 같은 표적의 두 타격은 세로로 먼저 쌓아 이전 값과 새 값이 한 숫자처럼 이어지지 않게 한다.
            float lift = Mathf.Min(96f, Mathf.Max(0f, CombatFeedbackSurface.Root.rect.yMax - 198f - _lastAnchor.y));
            _offset = new Vector2((stack / 2 == 0 ? -1 : 1) * 52f, 34f + (stack % 2) * lift);
            if (lift < 96f) _offset.y = -36f - (stack % 2) * 72f;
            // 아랫행 적의 숫자가 윗행 HP판에 붙지 않도록 표적 자신의 상단 안쪽으로 두 행을 내린다.
            if(target.IsEnemy&&CombatFeedbackSurface.DamageHasHudAbove(target))
                _offset.y=-36f-(stack%2)*72f;
            // 아군 쪽은 기존 검수된 오른쪽 머리 여백을 유지한다. 위로 밀면 앞줄 HUD에 겹친다.
            if(!target.IsEnemy)
            {
                float allyLift=Mathf.Min(68f,Mathf.Max(0f,CombatFeedbackSurface.Root.rect.yMax-162f-_lastAnchor.y));
                _offset=new Vector2(42+(stack/2)*102,(stack%2)*allyLift);
                if (allyLift < 50f) _offset.y = -(stack % 2) * 68f;
            }
            _scale = Mathf.Lerp(.95f, 1.10f, weight);
            if(kind==CombatNumberKind.Heal){_rise=8f;_popStrength=.08f;}
            if(kind==CombatNumberKind.Poison){_rise=14f;_popStrength=.10f;_scale=1f;_offset=new Vector2(0,28f);}
            if (Vfx.CardMotion.ViewOf(target)?.StageProxy != null)
                _offset = new Vector2(0f, stack * 96f);
            bool numeric = kind is CombatNumberKind.Damage or CombatNumberKind.Heal or CombatNumberKind.Shielded or CombatNumberKind.HealthCost;
            string text = kind switch
            {
                CombatNumberKind.Heal => "+" + amount.ToString("N0"),
                CombatNumberKind.HealthCost => "−" + amount.ToString("N0"),
                CombatNumberKind.Miss => "회피", CombatNumberKind.Nullified => "무효",
                CombatNumberKind.EffectResisted => "효과 저항", CombatNumberKind.Poison => "맹독", _ => amount.ToString("N0")
            };
            Color healInk=BattlefieldBackdrop.CurrentBackgroundLuminance>=.50f
                ? new Color(.20f,.58f,.46f) : new Color(.56f,1,.84f);
            Color ink = kind==CombatNumberKind.Poison ? accent : critical ? new Color(1,.86f,.44f) : kind == CombatNumberKind.Heal ? healInk :
                kind == CombatNumberKind.HealthCost ? new Color(1,.70f,.64f) :
                kind == CombatNumberKind.Shielded ? new Color(.70f,.88f,1) : new Color(.98f,.99f,1);
            TextMeshProUGUI label = UIBuild.Text("Number", transform, text, numeric ? critical ? 54 : counter ? 52 : kind==CombatNumberKind.Heal?32:42 : 28,
                ink, TextAlignmentOptions.Center);
            if(_stageNumber)
            {
                label.enableAutoSizing=false;
                label.fontSize=numeric?critical?60:kind is CombatNumberKind.Shielded or CombatNumberKind.Heal?32:46:22;
                label.color=critical?new Color(1,230f/255f,160f/255f):kind==CombatNumberKind.Shielded?new Color(240f/255f,201f/255f,106f/255f):ink;
                if(numeric)label.text="<i angle=8>"+text+"</i>";
            }
            label.fontStyle = FontStyles.Bold;
            _label=label;
            UIBuild.Anchor(label.rectTransform, new Vector2(0,.22f), new Vector2(1,.88f));
            if (_numberMaterial == null)
            {
                _numberMaterial = new Material(label.fontSharedMaterial) { name="Combat Number SDF", hideFlags=HideFlags.HideAndDontSave };
                _numberMaterial.EnableKeyword("OUTLINE_ON"); _numberMaterial.EnableKeyword("UNDERLAY_ON");
                _numberMaterial.SetFloat("_FaceDilate", .08f);
                _numberMaterial.SetFloat("_OutlineWidth", .28f);
                _numberMaterial.SetColor("_OutlineColor", new Color(.045f,.075f,.12f,1f));
                _numberMaterial.SetColor("_UnderlayColor", new Color(.025f,.045f,.075f,.90f));
                _numberMaterial.SetFloat("_UnderlayDilate", .18f);
                _numberMaterial.SetFloat("_UnderlayOffsetX", .5f); _numberMaterial.SetFloat("_UnderlayOffsetY", -.65f);
                _numberMaterial.SetFloat("_UnderlaySoftness", .05f);
            }
            label.fontSharedMaterial = _numberMaterial; label.UpdateMeshPadding();
            if(_stageNumber)
            {
                Material material=critical?_stageCriticalMaterial:_stageNumberMaterial;
                if(material==null)
                {
                    material=new Material(_numberMaterial){hideFlags=HideFlags.HideAndDontSave};
                    material.SetColor("_OutlineColor",critical?new Color(58f/255f,36f/255f,8f/255f):new Color(20f/255f,32f/255f,58f/255f));
                    if(critical)_stageCriticalMaterial=material;else _stageNumberMaterial=material;
                }
                label.fontSharedMaterial=material;label.UpdateMeshPadding();
            }
            if (numeric)
            {
                string caption = counter ? critical ? "치명타 · 반격" : "반격" : critical ? "치명타" : kind == CombatNumberKind.Shielded ? "보호막" : kind == CombatNumberKind.HealthCost ? "체력 소모" : "";
                _captionPrefix=caption;
                if (caption.Length > 0 || kind is CombatNumberKind.Damage or CombatNumberKind.Shielded)
                {
                    Color captionInk = counter && BattlefieldBackdrop.CurrentBackgroundLuminance >= .50f
                        ? new Color(.13f,.23f,.36f) : ink;
                    TextMeshProUGUI tag = UIBuild.Text("Kind", transform, caption, 22, captionInk, TextAlignmentOptions.Center);
                    _caption=tag;
                    if(_stageNumber){tag.enableAutoSizing=false;tag.fontSize=critical?15:14;tag.color=label.color;}
                    if (counter) tag.fontStyle = FontStyles.Bold;
                    else tag.fontSharedMaterial = _numberMaterial;
                    UIBuild.Anchor(tag.rectTransform, new Vector2(0,0), new Vector2(1,.27f));
                }
            }
            // 얼굴·체력판·원소 원과 기존 숫자의 상승 경로를 함께 피한다.
            // 같은 열의 세로 두 행을 먼저 찾고, 행이 막혔을 때만 가까운 옆 여백을 쓴다.
            float halfWidth=Mathf.Max(kind==CombatNumberKind.Heal?78f:critical||counter||kind!=CombatNumberKind.Damage?88f:26f,(label.GetPreferredValues(text).x*.5f+8f)*_scale*(1f+_popStrength));
            _placementHalf=new Vector2(halfWidth,kind==CombatNumberKind.Heal?26f:critical||counter||kind!=CombatNumberKind.Damage?55f:30f);
            if(kind==CombatNumberKind.Poison)_placementHalf=new Vector2(Mathf.Max(44f,label.GetPreferredValues(text).x*.5f+8f),23f);
            _offset=FindClearOffset(_offset,_placementHalf);
            Frame();
        }
        private void Append(int amount)
        {
            _amount=(int)System.Math.Min(int.MaxValue,(long)_amount+amount);_hitCount++;_age=0f;
            _label.text=_stageNumber?"<i angle=8>"+_amount.ToString("N0")+"</i>":_amount.ToString("N0");
            if(_caption!=null)_caption.text=(_captionPrefix.Length>0?_captionPrefix+" · ":"")+_hitCount+"타 합계";
            _placementHalf.x=Mathf.Max(_placementHalf.x,(_label.GetPreferredValues(_label.text).x*.5f+8f)*_scale*(1f+_popStrength));
            if(_caption!=null)_placementHalf.x=Mathf.Max(_placementHalf.x,
                (_caption.GetPreferredValues(_caption.text).x*.5f+8f)*_scale*(1f+_popStrength));
            _offset=FindClearOffset(_offset,_placementHalf);Frame();
        }
        private Vector2 FindClearOffset(Vector2 preferred,Vector2 half)
        {
            Camera camera=Camera.main;Vector2 divider=Vector2.zero;
            if(camera!=null)RectTransformUtility.ScreenPointToLocalPointInRectangle(CombatFeedbackSurface.Root,
                camera.WorldToScreenPoint(Vector3.zero),null,out divider);
            bool Fits(Vector2 offset)
            {
                Vector2 from=CombatFeedbackSurface.Clamp(_lastAnchor+offset,half);
                Vector2 to=CombatFeedbackSurface.Clamp(_lastAnchor+offset+Vector2.up*_rise,half);
                if(camera!=null&&(_target.IsEnemy?from.x-half.x<divider.x:from.x+half.x>divider.x))return false;
                if(!CombatFeedbackSurface.BelongsTo(_target,from)||!CombatFeedbackSurface.BelongsTo(_target,to))return false;
                return CombatFeedbackSurface.NoticeHasRoom((from+to)*.5f,half+new Vector2(0,Mathf.Abs(to.y-from.y)*.5f),this);
            }
            DamagePopup previous=Live.Find(item=>item!=null&&item!=this&&item._target==_target&&item._age<Lifetime);
            if(previous!=null)
            {
                float x=previous._rect.anchoredPosition.x-_lastAnchor.x;
                float gap=half.y+previous._placementHalf.y+Mathf.Max(_rise,previous._rise)+12f;
                for(int row=1;row<=1;row++)foreach(float side in new[]{-1f,1f})
                {
                    Vector2 candidate=new(x,previous._rect.anchoredPosition.y-_lastAnchor.y+side*gap*row);
                    Vector2 actual=CombatFeedbackSurface.Clamp(_lastAnchor+candidate,half);
                    if(Mathf.Abs(actual.x-previous._rect.anchoredPosition.x)<.1f&&Fits(candidate))return candidate;
                }
            }
            if(Fits(preferred))return preferred;
            Vector2 reference=preferred;
            var view=Vfx.CardMotion.ViewOf(_target);
            if(view!=null&&camera!=null&&view.StageProxy==null)
            {
                Rect portrait=view.PortraitFocusScreenRect(camera),hud=view.HudScreenRect(camera);
                float bodyTop=Mathf.Lerp(portrait.yMin,portrait.yMax,.55f);
                Vector2 screen=new(hud.center.x,(hud.yMax+bodyTop)*.5f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(CombatFeedbackSurface.Root,screen,null,out Vector2 local);
                Vector2 body=local-_lastAnchor;
                reference=body;
                if(Fits(body))return body;
            }
            Vector2 best=preferred;float bestDistance=float.PositiveInfinity;
            void Consider(Vector2 candidate)
            {
                Vector2 actual=CombatFeedbackSurface.Clamp(_lastAnchor+candidate,half)-_lastAnchor;
                float distance=(actual-reference).sqrMagnitude;
                if(distance>=bestDistance||!Fits(candidate))return;
                best=candidate;bestDistance=distance;
            }
            float[] columns={preferred.x,-64f,64f,-100f,100f,-164f,164f};
            float[] rows={34f+(_slot%2)*96f,50f,98f,130f,226f,-36f,-110f,-150f,-182f,-214f};
            foreach(float x in columns)foreach(float y in rows)
            {
                Vector2 candidate=new(x,y);
                Consider(candidate);
            }
            // 큰 숫자와 원소 원이 동시에 놓인 행은 고정 두 열 사이의 좁은 여백도 검색한다.
            // 표적에 가까운 테두리부터 찾으므로 빈 공간 때문에 반대편 전장으로 먼저 건너가지 않는다.
            const float step=24f;
            for(int ring=1;ring<=16;ring++)
            {
                for(int edge=-ring;edge<=ring;edge++)
                {
                    Vector2 left=reference+new Vector2(-ring*step,edge*step);
                    Vector2 right=reference+new Vector2(ring*step,edge*step);
                    Consider(left);Consider(right);
                }
                for(int edge=-ring+1;edge<ring;edge++)
                {
                    Vector2 below=reference+new Vector2(edge*step,-ring*step);
                    Vector2 above=reference+new Vector2(edge*step,ring*step);
                    Consider(below);Consider(above);
                }
                if(bestDistance<ring*ring*step*step)break;
            }
            return float.IsFinite(bestDistance)?best:preferred;
        }
        private void LateUpdate()
        {
            if(Time.timeScale<=0) return;
            if(_kind==CombatNumberKind.Poison)
            {
                var view=Vfx.CardMotion.ViewOf(_target);
                if(_target==null || !_target.isActive || !_target.IsOnField || view?.PortraitRenderer==null ||
                    view.PortraitRenderer.sprite==null || Managers.GameManager.Instance?.gameState!=BaseClasses.BaseEnums.GameState.RoundInProgress)
                {Destroy(gameObject);return;}
            }
            _age += Vfx.VfxTiming.ReadableDeltaTime;
            var scheduler=Managers.GameManager.Instance?.ActionScheduler;
            if(_actionId>=0&&scheduler?.ExecutingUnit!=null&&scheduler.CurrentActionId==_actionId)
                _age=Mathf.Min(_age,.72f);
            if (_age >= Lifetime) { Destroy(gameObject); return; }
            if (_target != null && _target.isActive) _lastAnchor = Anchor();
            Frame();
        }
        private void Frame()
        {
            _rect.anchoredPosition = CombatFeedbackSurface.Clamp(_lastAnchor + _offset + Vector2.up * (_rise * _age / Lifetime),_placementHalf);
            float pop = 1 + _popStrength * Mathf.Pow(1 - Mathf.Clamp01(_age / .13f),2);
            _rect.localScale = Vector3.one * (_scale * pop);
            _group.alpha = _age < .72f ? 1 : Mathf.Clamp01((Lifetime-_age)/.40f);
        }
        /// <summary>상태 알림은 숫자의 남은 상승 경로까지 비워 두어 다음 프레임에도 겹치지 않는다.</summary>
        internal static bool HasRoom(Vector2 position, Vector2 halfSize,DamagePopup ignore=null)
        {
            Rect box = new(position - halfSize, halfSize * 2);
            foreach (var item in Live)
            {
                if (item == null || item==ignore || !item.gameObject.activeInHierarchy || item._age >= Lifetime) continue;
                float rise = item._rise * Mathf.Clamp01((Lifetime-item._age)/Lifetime);
                Vector2 half = item._placementHalf;
                Vector2 center = item._rect.anchoredPosition;
                Rect occupied = Rect.MinMaxRect(center.x-half.x,center.y-half.y,
                    center.x+half.x,center.y+half.y+rise);
                if (box.Overlaps(occupied)) return false;
            }
            return true;
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static string DescribeRoomCollision(Rect box, DamagePopup ignore)
        {
            var details = new List<string>();
            foreach (var item in Live)
            {
                if (item == null || item == ignore || !item.gameObject.activeInHierarchy || item._age >= Lifetime) continue;
                float rise = item._rise * Mathf.Clamp01((Lifetime-item._age)/Lifetime);
                Vector2 half = item._placementHalf, center = item._rect.anchoredPosition;
                Rect occupied = Rect.MinMaxRect(center.x-half.x, center.y-half.y, center.x+half.x, center.y+half.y+rise);
                if (box.Overlaps(occupied)) details.Add($"popup={item.name}/{item.GetInstanceID()}; renderer={item._label.name}/{item._label.GetInstanceID()}; target={item._target?.ID}; kind={item._kind}; value={item._label.text}; age={item._age:R}; local={occupied}");
            }
            return string.Join("\n", details);
        }
#endif
        private void OnDestroy() => Live.Remove(this);
    }
}
