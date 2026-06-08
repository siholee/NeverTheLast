using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Managers;
using UnityEngine;

/// <summary>
/// 전투 전 캐릭터 선택 단계 관리자.<br/>
/// • 사용 가능한 전체 영웅 목록을 unitDataList에서 읽음<br/>
/// • 플레이어가 최대 5명 선택, 전열/후열 배치 지정<br/>
/// • ConfirmSelection() → 아군 소환 → 라운드 로드 → 전투 시작
/// </summary>
public class CharacterSelectionManager : MonoBehaviour
{
    public static CharacterSelectionManager Instance { get; private set; }

    // 선택된 편성 항목
    public class LineupEntry
    {
        public int                   UnitId;
        public BaseEnums.GridLine    Line;
        public int                   SlotIndex;
    }

    private readonly List<LineupEntry> _lineup = new();

    /// <summary>현재 선택된 편성 (읽기 전용)</summary>
    public IReadOnlyList<LineupEntry> Lineup => _lineup;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }
    }

    // ── 편성 API (UI에서 호출) ────────────────────────────────────────────────

    /// <summary>영웅 추가. 최대 5명. 전열/후열 자동 배치 (전열 먼저 채움).</summary>
    public bool AddHero(int unitId, BaseEnums.GridLine preferredLine = BaseEnums.GridLine.Frontline)
    {
        if (_lineup.Count >= GridManager.MaxHeroesTotal)
        {
            Debug.Log("[CharSel] 최대 편성 인원(5명) 초과");
            return false;
        }

        // 슬롯 인덱스 결정 (같은 라인의 이미 배치된 수)
        int slotIndex = _lineup.Count(e => e.Line == preferredLine);
        if (slotIndex >= GridManager.SlotsPerLine)
        {
            // 원하는 라인 꽉 참 → 반대 라인으로
            preferredLine = preferredLine == BaseEnums.GridLine.Frontline
                ? BaseEnums.GridLine.Backline
                : BaseEnums.GridLine.Frontline;
            slotIndex = _lineup.Count(e => e.Line == preferredLine);
        }

        _lineup.Add(new LineupEntry { UnitId = unitId, Line = preferredLine, SlotIndex = slotIndex });
        Debug.Log($"[CharSel] {unitId} 추가 → {preferredLine} 슬롯 {slotIndex} (총 {_lineup.Count}명)");
        return true;
    }

    /// <summary>유닛 제거</summary>
    public void RemoveHero(int unitId)
    {
        int idx = _lineup.FindIndex(e => e.UnitId == unitId);
        if (idx < 0) return;
        _lineup.RemoveAt(idx);
        RebuildSlotIndices();
        Debug.Log($"[CharSel] {unitId} 제거 → 남은 {_lineup.Count}명");
    }

    /// <summary>전체 편성 초기화</summary>
    public void ClearLineup() { _lineup.Clear(); }

    // ── 확정 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 편성 확정: 아군 소환 → 라운드 로드 → CharacterSelection → Preparation → RoundInProgress.
    /// </summary>
    public void ConfirmSelection()
    {
        if (_lineup.Count == 0)
        {
            Debug.LogWarning("[CharSel] 선택된 영웅 없음 — 기본 팀으로 시작");
            UseDefaultLineup();
        }

        // 아군 소환
        foreach (var entry in _lineup)
        {
            bool isFrontline = entry.Line == BaseEnums.GridLine.Frontline;
            GridManager.Instance.SpawnHero(entry.UnitId, isFrontline, entry.SlotIndex);
        }

        // 적 큐 로드
        RunManager.Instance?.LoadCurrentRound();

        // 시너지 + 보상 패널 숨기기
        GameManager.Instance.uiManager?.HideRewardPanel();

        // CharacterSelection → Preparation → RoundInProgress
        GameManager.Instance.NextGameState(false); // CharacterSelection → Preparation
        GameManager.Instance.NextGameState(false); // Preparation → RoundInProgress
    }

    // ── 에디터 직접 플레이 기본 팀 ───────────────────────────────────────────

    /// <summary>DirectStart 모드(에디터)용 기본 편성. unitDataList 첫 유닛을 전열에 배치.</summary>
    public void UseDefaultLineup()
    {
        _lineup.Clear();
        var units = GameManager.Instance.unitDataList?.units;
        if (units == null || units.Count == 0)
        {
            Debug.LogError("[CharSel] unitDataList 비어있음 — 기본 팀 구성 불가");
            return;
        }

        // 플레이어 영웅만 선택 (id < 100: 플레이어, id >= 100: 적 전용 유닛)
        var playerUnits = units.FindAll(u => u.id < 100);
        int count = Mathf.Min(playerUnits.Count, 3);
        for (int i = 0; i < count; i++)
        {
            _lineup.Add(new LineupEntry
            {
                UnitId    = playerUnits[i].id,
                Line      = BaseEnums.GridLine.Frontline,
                SlotIndex = i
            });
        }
        Debug.Log($"[CharSel] 기본 팀 구성: {string.Join(", ", _lineup.Select(e => e.UnitId))}");
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────────────

    private void RebuildSlotIndices()
    {
        int frontIdx = 0, backIdx = 0;
        foreach (var entry in _lineup)
        {
            if (entry.Line == BaseEnums.GridLine.Frontline)
                entry.SlotIndex = frontIdx++;
            else
                entry.SlotIndex = backIdx++;
        }
    }
}
