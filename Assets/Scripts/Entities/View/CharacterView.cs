using System;
using System.Collections.Generic;
using UnityEngine;

namespace Entities.View
{
    /// <summary>
    /// 페이퍼돌 캐릭터의 논리적 시각 슬롯.
    /// z-순서(뒤→앞)는 실제 SpriteRenderer의 sortingOrder / Sorting Group으로 정한다.
    /// (규격서 §2 참고)
    /// </summary>
    public enum VisualSlot
    {
        HairBack,   // 뒷머리 (캐릭터 고유)
        BackArm,    // 뒤쪽 팔 (베이스 공유)
        Body,       // 몸통+다리 (베이스 공유)
        Armor,      // 몸통 옷/갑옷 (아이템: Armor)
        Head,       // 머리 베이스 (베이스 공유)
        Face,       // 표정 (캐릭터 고유)
        HairFront,  // 앞머리 (캐릭터 고유)
        OffHand,    // 방패 등 (아이템: OffHand)
        FrontArm,   // 앞쪽 팔 (베이스 공유, ATTACK 시 움직임)
        MainHand,   // 무기 (아이템: MainHand)
    }

    /// <summary>재생 가능한 모션. Animator 상태/트리거와 1:1 대응. (규격서 §4)</summary>
    public enum MotionType
    {
        Idle,   // 기본 상태(루프)
        Spawn,  // 등장
        Attack, // 공격
        Hit,    // 피격
        Exit,   // 퇴장
    }

    /// <summary>
    /// 셀 위 유닛의 모듈러 도트 캐릭터 표현(퍼펫 리그).
    /// <para>- 베이스 몸/머리/팔은 공유 리그: Animator가 트랜스폼(뼈)을 애니메이트한다.</para>
    /// <para>- Face/Hair/Armor/MainHand/OffHand 슬롯은 스프라이트 교체로 외형을 바꾼다.</para>
    /// <para>- 모션은 Animator 상태(Idle/Spawn/Attack/Hit/Exit)로 재생한다.</para>
    /// 스킬 이펙트는 이 컴포넌트가 다루지 않는다(기존 이펙트 시스템 유지).
    ///
    /// 패키지 비의존: com.unity.2d.animation 없이 SpriteRenderer 교체만으로 동작한다.
    /// 나중에 Sprite Library/Resolver로 교체하려면 SetSlotSprite 내부만 바꾸면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterView : MonoBehaviour
    {
        [Serializable]
        public class SlotBinding
        {
            public VisualSlot slot;
            public SpriteRenderer renderer;
        }

        [Header("레이어 슬롯 → SpriteRenderer (인스펙터에서 연결)")]
        [SerializeField] private List<SlotBinding> slotBindings = new();

        [Header("모션 (베이스 리그의 Animator)")]
        [SerializeField] private Animator animator;

        [Header("좌우반전 루트 (기본: 오른쪽 바라봄)")]
        [SerializeField] private Transform flipRoot;

        // Animator Controller의 파라미터 이름과 반드시 일치시킬 것.
        private static class AnimParam
        {
            public const string Spawn = "Spawn";
            public const string Attack = "Attack";
            public const string Hit = "Hit";
            public const string Exit = "Exit";
            // Idle은 기본 상태(트리거 불필요)
        }

        // Resources 경로 프리픽스 (규격서 §5)
        private const string BaseRoot = "Sprite/Char/Base/";
        private const string FaceRoot = "Sprite/Char/Face/";
        private const string HairRoot = "Sprite/Char/Hair/";
        private const string ItemRoot = "Sprite/Char/Item/";

        private readonly Dictionary<VisualSlot, SpriteRenderer> _slots = new();
        private bool _initialized;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _slots.Clear();
            foreach (SlotBinding b in slotBindings)
            {
                if (b?.renderer != null)
                {
                    _slots[b.slot] = b.renderer;
                }
            }
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (flipRoot == null) flipRoot = transform;
            _initialized = true;
        }

        // ─────────────────────────────────────────────────────────────
        // 외형
        // ─────────────────────────────────────────────────────────────

        /// <summary>캐릭터 고유 외형(얼굴/헤어) 지정. 보통 스폰 시 1회.</summary>
        /// <param name="faceKey">Sprite/Char/Face/{faceKey} (예: "SPARTACUS_face")</param>
        /// <param name="hairKey">Sprite/Char/Hair/{hairKey} (예: "SPARTACUS_hair")</param>
        /// <param name="hairBackKey">뒷머리 키(선택). 없으면 뒷머리 슬롯 비활성.</param>
        public void SetAppearance(string faceKey, string hairKey, string hairBackKey = null)
        {
            EnsureInitialized();
            SetSlotSprite(VisualSlot.Face, LoadSprite(FaceRoot, faceKey));
            SetSlotSprite(VisualSlot.HairFront, LoadSprite(HairRoot, hairKey));
            SetSlotSprite(VisualSlot.HairBack, LoadSprite(HairRoot, hairBackKey));
        }

        /// <summary>
        /// 게임 장비 슬롯의 외형을 아이템 id로 반영.
        /// </summary>
        /// <param name="gameSlot">"MainHand" | "OffHand" | "Armor" (BaseClasses.EquipmentSlot 이름)</param>
        /// <param name="itemId">아이템 id. 0 이하이면 슬롯을 비운다.</param>
        public void EquipVisual(string gameSlot, int itemId)
        {
            EnsureInitialized();
            if (!TryMapEquipmentSlot(gameSlot, out VisualSlot vslot)) return;

            if (itemId <= 0)
            {
                SetSlotSprite(vslot, null);
                return;
            }
            // 규격서 §5: ITEM_{id}_{slot}.png (slot 소문자)
            string key = $"ITEM_{itemId}_{gameSlot.Trim().ToLowerInvariant()}";
            SetSlotSprite(vslot, LoadSprite(ItemRoot, key));
        }

        /// <summary>장비 슬롯 외형 제거.</summary>
        public void ClearVisual(string gameSlot)
        {
            EnsureInitialized();
            if (TryMapEquipmentSlot(gameSlot, out VisualSlot vslot))
            {
                SetSlotSprite(vslot, null);
            }
        }

        /// <summary>좌우 방향. faceLeft=true면 왼쪽을 바라보도록 flipRoot를 반전.</summary>
        public void SetFacing(bool faceLeft)
        {
            EnsureInitialized();
            if (flipRoot == null) return;
            Vector3 s = flipRoot.localScale;
            s.x = Mathf.Abs(s.x) * (faceLeft ? -1f : 1f);
            flipRoot.localScale = s;
        }

        // ─────────────────────────────────────────────────────────────
        // 모션
        // ─────────────────────────────────────────────────────────────

        public void PlayMotion(MotionType motion)
        {
            EnsureInitialized();
            if (animator == null) return;
            switch (motion)
            {
                case MotionType.Spawn:  animator.SetTrigger(AnimParam.Spawn); break;
                case MotionType.Attack: animator.SetTrigger(AnimParam.Attack); break;
                case MotionType.Hit:    animator.SetTrigger(AnimParam.Hit); break;
                case MotionType.Exit:   animator.SetTrigger(AnimParam.Exit); break;
                case MotionType.Idle:   /* 기본 상태로 자동 복귀 */ break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 내부
        // ─────────────────────────────────────────────────────────────

        private void SetSlotSprite(VisualSlot slot, Sprite sprite)
        {
            if (!_slots.TryGetValue(slot, out SpriteRenderer r) || r == null) return;
            r.sprite = sprite;
            r.enabled = sprite != null; // 비어 있으면 렌더러 끔
        }

        private static Sprite LoadSprite(string root, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            Sprite sp = Resources.Load<Sprite>(root + key);
            if (sp == null)
            {
                Debug.LogWarning($"[CharacterView] 스프라이트를 찾을 수 없음: {root}{key}");
            }
            return sp;
        }

        /// <summary>게임 장비 슬롯 문자열 → 시각 슬롯.</summary>
        private static bool TryMapEquipmentSlot(string gameSlot, out VisualSlot vslot)
        {
            switch ((gameSlot ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "mainhand": vslot = VisualSlot.MainHand; return true;
                case "offhand":  vslot = VisualSlot.OffHand;  return true;
                case "armor":    vslot = VisualSlot.Armor;    return true;
                default:         vslot = VisualSlot.Body;     return false;
            }
        }
    }
}
