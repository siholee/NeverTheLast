using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Managers;
using UnityEngine;

namespace Entities.View
{
    /// <summary>
    /// Unit의 생명주기 이벤트를 <see cref="CharacterView"/> 모션으로 연결하고,
    /// 유닛의 고유 외형(얼굴/헤어)과 장착 아이템 외형을 뷰에 반영한다.
    ///
    /// 스캐폴딩: 스폰/셀 배치 파이프라인에 붙이는 최종 배선은 프리팹/아트가
    /// 준비된 뒤에 진행한다. 지금은 Unit + CharacterView만 있으면 독립 동작한다.
    ///
    /// 이벤트 매핑 (규격서 §7):
    ///   OnSpawn            → Spawn
    ///   OnNormalActivates  → Attack
    ///   OnAfterDamageTaken → Hit
    ///   OnDeath            → Exit
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterMotionBinder : MonoBehaviour
    {
        [SerializeField] private Unit unit;
        [SerializeField] private CharacterView view;

        [Header("외형 키 오버라이드 (비우면 유닛 이름 기반)")]
        [Tooltip("Sprite/Char/Face/{키}. 비우면 \"{UnitName}_face\"")]
        [SerializeField] private string faceKeyOverride;
        [Tooltip("Sprite/Char/Hair/{키}. 비우면 \"{UnitName}_hair\"")]
        [SerializeField] private string hairKeyOverride;
        [Tooltip("뒷머리 키(선택). 비우면 뒷머리 없음")]
        [SerializeField] private string hairBackKeyOverride;

        // 시각으로 반영할 게임 장비 슬롯 (ItemData.slot 값과 일치)
        private static readonly string[] VisualEquipmentSlots = { "MainHand", "OffHand", "Armor" };

        private bool _subscribed;

        private void Awake()
        {
            if (unit == null) unit = GetComponentInParent<Unit>();
            if (view == null) view = GetComponentInChildren<CharacterView>();
        }

        private void OnEnable()
        {
            Subscribe();
            ApplyAll();
        }

        /// <summary>런타임 인스턴스화 시 외부에서 주입하는 진입점.</summary>
        public void Bind(Unit boundUnit, CharacterView boundView)
        {
            Unsubscribe();
            unit = boundUnit;
            view = boundView;
            Subscribe();
            ApplyAll();
        }

        /// <summary>외형(고유 + 장비)을 모두 다시 반영.</summary>
        public void ApplyAll()
        {
            RefreshAppearance();
            RefreshEquipment();
        }

        /// <summary>얼굴/헤어 등 캐릭터 고유 외형 반영.</summary>
        public void RefreshAppearance()
        {
            if (view == null || unit == null) return;

            string faceKey = string.IsNullOrWhiteSpace(faceKeyOverride)
                ? $"{unit.UnitName}_face"
                : faceKeyOverride;
            string hairKey = string.IsNullOrWhiteSpace(hairKeyOverride)
                ? $"{unit.UnitName}_hair"
                : hairKeyOverride;

            view.SetAppearance(faceKey, hairKey, NullIfBlank(hairBackKeyOverride));
        }

        /// <summary>장착 아이템 외형(무기/방패/갑옷) 반영.</summary>
        public void RefreshEquipment()
        {
            if (view == null || unit == null) return;

            // 장착 id들을 슬롯별로 정리
            Dictionary<string, int> bySlot = BuildEquippedSlotMap();

            foreach (string slot in VisualEquipmentSlots)
            {
                if (bySlot.TryGetValue(slot, out int itemId))
                {
                    view.EquipVisual(slot, itemId);
                }
                else
                {
                    view.ClearVisual(slot);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 이벤트 구독
        // ─────────────────────────────────────────────────────────────

        private void Subscribe()
        {
            if (_subscribed || unit == null) return;
            unit.AddListener<EventContext>(BaseEnums.UnitEventType.OnSpawn, OnSpawn);
            unit.AddListener<EventContext>(BaseEnums.UnitEventType.OnNormalActivates, OnAttack);
            unit.AddListener<EventContext>(BaseEnums.UnitEventType.OnAfterDamageTaken, OnHit);
            unit.AddListener<EventContext>(BaseEnums.UnitEventType.OnDeath, OnExit);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            // 현재 Unit에는 RemoveListener API가 없어 재바인딩 시 중복 구독을 피하기 위한 가드만 둔다.
            // (RemoveListener 도입 시 여기서 해제 처리)
            _subscribed = false;
        }

        private void OnSpawn(EventContext _)  => view?.PlayMotion(MotionType.Spawn);
        private void OnAttack(EventContext _) => view?.PlayMotion(MotionType.Attack);
        private void OnHit(EventContext _)    => view?.PlayMotion(MotionType.Hit);
        private void OnExit(EventContext _)   => view?.PlayMotion(MotionType.Exit);

        // ─────────────────────────────────────────────────────────────
        // 내부
        // ─────────────────────────────────────────────────────────────

        /// <summary>유닛이 장착한 아이템 id를 게임 슬롯("MainHand"/"OffHand"/"Armor")별로 매핑.</summary>
        private Dictionary<string, int> BuildEquippedSlotMap()
        {
            var map = new Dictionary<string, int>();
            if (unit == null) return map;

            List<ItemData> catalog = GameManager.Instance?.itemDataList?.items;
            if (catalog == null) return map;

            foreach (int id in unit.EquippedItemIds)
            {
                ItemData data = catalog.FirstOrDefault(i => i.id == id);
                if (data == null || string.IsNullOrWhiteSpace(data.slot)) continue;

                // 마지막 값 우선(같은 슬롯 중복은 없다고 가정)
                map[data.slot] = id;
            }
            return map;
        }

        private static string NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
