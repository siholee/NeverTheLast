using BaseClasses;
using Entities;
using UnityEngine;

namespace Effects
{
    /// <summary>
    /// 피해가 실제로 들어간 순간의 타격감을 한 곳에서 낸다.
    ///
    /// 공격 코드는 자기 연출(투사체·베기)만 책임지고, <b>맞은 쪽의 반응</b>은 전부 여기로 온다.
    /// 지속피해든 반격이든 즉시 피해든 <see cref="Unit.TakeDamage"/> 하나를 지나므로,
    /// 여기에 걸어 두면 어떤 경로로 들어온 피해든 같은 규칙으로 보인다.
    ///
    /// 세 가지를 함께 친다. 하나만으로는 약하고, 셋이 같은 프레임에 겹쳐야 '맞았다'가 된다.
    ///   · 숫자 — 얼마나 아팠는지
    ///   · 카드 — 흔들리고 뒤로 밀린다
    ///   · 화면 — 큰 한 방에만 아주 짧게 흔든다
    /// </summary>
    public static class CombatFeedback
    {
        /// <summary>이 비율만큼의 최대 체력을 한 번에 깎으면 연출이 최대 세기가 된다.</summary>
        private const float HeavyHitRatio = 0.22f;

        /// <summary>화면을 흔들기 시작하는 세기. 잔매마다 흔들리면 금방 피로해진다.</summary>
        private const float ScreenShakeThreshold = 0.55f;

        /// <summary>치명타도 궁극기도 아닌 공격이 시간을 멈출 수 있는 세기.</summary>
        private const float HitStopThreshold = 0.55f;

        /// <summary>
        /// 피해 한 건이 정산된 직후 부른다.
        /// </summary>
        /// <param name="target">맞은 유닛.</param>
        /// <param name="context">피해 정보. 공격자·치명타·태그를 읽는다.</param>
        /// <param name="hpLost">체력에서 깎인 양.</param>
        /// <param name="shieldLost">방어막이 대신 받은 양.</param>
        public static void PlayDamage(Unit target, DamageContext context, int hpLost, int shieldLost)
        {
            if (target == null) return;

            int total = Mathf.Max(0, hpLost) + Mathf.Max(0, shieldLost);
            if (total <= 0) return;

            AttackVisualForm form = AttackVisuals.Classify(context?.Attacker, context);
            Color accent = AttackVisuals.AccentFor(context?.Attacker, context, form);
            bool physical = AttackVisuals.IsPhysical(form);
            bool crit = context?.IsCrit == true;

            // 세기는 '이 한 방이 최대 체력의 몇 할인가'로 잰다. 절대값은 레벨에 따라 뜻이 달라진다.
            float weight = target.HpMax > 0
                ? Mathf.Clamp01(total / (target.HpMax * HeavyHitRatio))
                : 0.5f;

            CombatNumberKind kind = hpLost > 0 ? CombatNumberKind.Damage : CombatNumberKind.Shielded;
            DamagePopup.Show(target, total, crit, accent, kind, weight);

            // 지속피해는 때린 사람이 없다. 카드까지 흔들면 매 턴 전장이 들썩인다.
            bool overTime = context?.CodeType == BaseEnums.CodeType.Effect;
            float strength = Mathf.Lerp(0.45f, 1f, weight) * (crit ? 1.25f : 1f) * (overTime ? 0.35f : 1f);
            PlayCardHit(target, strength, physical && !overTime, crit);

            if (!overTime && strength >= ScreenShakeThreshold)
            {
                CameraShake.Shake(Mathf.Lerp(0.06f, 0.22f, weight) * (crit ? 1.3f : 1f));
            }

            // 무게감은 흔들림보다 '멈춤'에서 온다. 다만 멈출 자격은 아주 좁게 준다 —
            // 잔매마다 서면 전투가 끊기는 것으로만 보인다.
            bool ultimate = context?.CodeType == BaseEnums.CodeType.Ultimate ||
                            context?.DamageTags?.Contains(DamageTag.UltAttack) == true;
            if (overTime || (!crit && !ultimate && weight < HitStopThreshold)) return;

            float freeze = 0.030f + (crit ? 0.008f : 0f) + (ultimate ? 0.012f : 0f) + weight * 0.010f;
            HitStop.Play(freeze);
        }

        /// <summary>회피했을 때. 숫자 대신 '회피'를 띄우고 카드를 가볍게 물린다.</summary>
        public static void PlayEvade(Unit target)
        {
            if (target == null) return;
            DamagePopup.Show(target, 0, false, Color.white, CombatNumberKind.Miss);
        }

        /// <summary>횟수제 무적이 타격을 지웠다. 회피와 달리 맞기는 맞았으므로 카드는 가볍게 흔든다.</summary>
        public static void PlayNullified(Unit target)
        {
            if (target == null) return;
            DamagePopup.Show(target, 0, false, Color.white, CombatNumberKind.Nullified);
            PlayCardHit(target, 0.15f, false, false);
        }

        /// <summary>회복량을 띄운다.</summary>
        public static void PlayHeal(Unit target, int amount)
        {
            if (target == null || amount <= 0) return;
            DamagePopup.Show(target, amount, false, Color.white, CombatNumberKind.Heal,
                target.HpMax > 0 ? Mathf.Clamp01(amount / (target.HpMax * HeavyHitRatio)) : 0.4f);
        }

        /// <summary>맞은 카드를 흔든다. 칸에 선 유닛과 소환수 카드를 함께 본다.</summary>
        private static void PlayCardHit(Unit target, float strength, bool physical, bool crit)
        {
            if (target.currentCell != null)
            {
                target.currentCell.PlayHitReaction(strength, physical, crit);
                return;
            }

            target.SummonView?.PlayHitReaction(strength, physical, crit);
        }
    }


    /// <summary>
    /// 큰 한 방에서 시간을 아주 잠깐 세운다.
    ///
    /// 흔들림은 '무슨 일이 났다'를 알리고, <b>멈춤은 그 한 방이 무거웠다</b>를 알린다.
    /// 30~50ms면 눈에는 '툭 걸렸다'로만 읽히고 전투 흐름은 끊기지 않는다.
    ///
    /// 배속은 <c>Time.timeScale</c>로 걸려 있다(<see cref="Managers.UI.HUD.BattleHud"/>).
    /// 그래서 멈춤은 배율을 <b>기억했다가 되돌리는</b> 방식이어야 하고, 되돌릴 때는
    /// 우리가 넣어 둔 값이 그대로 남아 있을 때만 손댄다 — 그 사이 플레이어가 배속을
    /// 바꿨다면 그쪽이 주인이다. 실시간(<c>unscaledDeltaTime</c>)으로 재므로 8배속에서도
    /// 멈추는 길이는 같다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitStop : MonoBehaviour
    {
        /// <summary>멈춘 동안의 시간 배율. 0으로 두면 코루틴이 아예 서 버려 복구가 늦는다.</summary>
        private const float FreezeScale = 0.04f;

        /// <summary>멈춤 사이의 최소 간격(실시간). 연타에서 화면이 계속 걸리는 것을 막는다.</summary>
        private const float MinInterval = 0.12f;

        private const float MaxDuration = 0.05f;

        private static HitStop _instance;

        private float _remaining;
        private float _baseScale = 1f;
        private float _frozenScale;
        private float _lastStop = -10f;

        public static void Play(float seconds)
        {
            if (_instance == null)
            {
                var go = new GameObject("HitStop") { hideFlags = HideFlags.HideAndDontSave };
                // 씬이 바뀌는 도중에 멈춰 있으면 배율을 되돌릴 주인이 사라진다.
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<HitStop>();
            }

            _instance.Begin(Mathf.Clamp(seconds, 0.01f, MaxDuration));
        }

        private void Begin(float seconds)
        {
            if (Time.unscaledTime - _lastStop < MinInterval) return;
            _lastStop = Time.unscaledTime;

            // 이미 멈춘 중이면 배율을 또 기억하지 않는다. 기억해 두면 0.04가 기준이 되어
            // 전투가 그대로 느려진 채 남는다.
            if (_remaining <= 0f) _baseScale = Time.timeScale;

            _remaining = Mathf.Max(_remaining, seconds);
            _frozenScale = FreezeScale;
            Time.timeScale = FreezeScale;
        }

        private void Update()
        {
            if (_remaining <= 0f) return;

            _remaining -= Time.unscaledDeltaTime;
            if (_remaining > 0f) return;
            Restore();
        }

        private void Restore()
        {
            _remaining = 0f;
            // 그 사이 배속이 바뀌었다면 그쪽 값이 맞다. 우리가 넣은 값일 때만 되돌린다.
            if (Mathf.Approximately(Time.timeScale, _frozenScale)) Time.timeScale = _baseScale;
        }

        private void OnDisable() => Restore();
    }

    /// <summary>
    /// 큰 한 방에만 아주 짧게 화면을 흔든다.
    ///
    /// 카메라 위치는 <see cref="Managers.GridManager"/>의 프레이밍이 주인이다.
    /// 그래서 매 프레임 <b>직전에 넣은 흔들림을 빼고</b> 기준 위치를 다시 읽는다 —
    /// 전투 중 빈 칸이 지워져 프레이밍이 다시 잡혀도 카메라가 옛 자리로 끌려가지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour
    {
        private const float Duration = 0.17f;

        /// <summary>진동 세기의 상한(카드 한 변 대비). 넘으면 화면이 흔들리는 게 아니라 튄다.</summary>
        private const float MaxAmplitude = 0.30f;

        private static CameraShake _instance;

        private Vector3 _appliedOffset;
        private float _amplitude;
        private float _age = float.MaxValue;
        private float _seed;

        /// <summary>세기는 카드 한 변 대비 진폭이다.</summary>
        public static void Shake(float amplitude)
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            if (_instance == null || _instance.gameObject != camera.gameObject)
            {
                if (_instance != null) Destroy(_instance);
                _instance = camera.gameObject.GetComponent<CameraShake>()
                            ?? camera.gameObject.AddComponent<CameraShake>();
            }

            _instance.Begin(Mathf.Min(amplitude, MaxAmplitude));
        }

        private void Begin(float amplitude)
        {
            // 이미 흔들리는 중이면 더 센 쪽을 남긴다. 합치면 연타에서 화면이 날아간다.
            _amplitude = _age < Duration ? Mathf.Max(_amplitude, amplitude) : amplitude;
            _age = 0f;
            _seed = Random.value * 100f;
        }

        private void LateUpdate()
        {
            if (_age >= Duration)
            {
                if (_appliedOffset == Vector3.zero) return;
                transform.position -= _appliedOffset;
                _appliedOffset = Vector3.zero;
                return;
            }

            _age += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(_age / Duration);
            float reach = Cell.CardSize * _amplitude * fade * fade;

            // 두 축의 주파수를 달리해 한 방향으로만 떠는 것처럼 보이지 않게 한다.
            var offset = new Vector3(
                (Mathf.PerlinNoise(_seed, _age * 46f) - 0.5f) * 2f * reach,
                (Mathf.PerlinNoise(_seed + 17f, _age * 39f) - 0.5f) * 2f * reach,
                0f);

            transform.position = transform.position - _appliedOffset + offset;
            _appliedOffset = offset;
        }

        private void OnDisable()
        {
            if (_appliedOffset == Vector3.zero) return;
            transform.position -= _appliedOffset;
            _appliedOffset = Vector3.zero;
        }
    }
}
