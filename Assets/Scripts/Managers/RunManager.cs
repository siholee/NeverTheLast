using System.Collections.Generic;
using System.Linq;
using BaseClasses;
using Core;
using Entities;
using UnityEngine;
using static BaseClasses.BaseEnums;

namespace Managers
{
    /// <summary>
    /// 로그라이트 런 관리자.
    /// 스테이지/인카운터 진행, HP 영속성, 세이브/로드 담당.
    /// DontDestroyOnLoad 싱글톤.
    /// </summary>
    public class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        public const int StagesPerRun       = 3;
        public const int EncountersPerStage = 3; // 인덱스 4 = 보스
        public const int BossEncounter      = 4;

        public int  CurrentStage     { get; private set; } = 1;
        public int  CurrentEncounter { get; private set; } = 1;
        public int  TotalBattles     { get; private set; } = 0;
        public bool IsBossEncounter  => CurrentEncounter == BossEncounter;
        public bool RunActive        { get; private set; } = false;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // ── 런 시작 / 로드 ────────────────────────────────────────────────────────

        /// <summary>
        /// 새 런 시작: 상태 초기화 + SP 리셋.<br/>
        /// 아군 소환은 CharacterSelectionManager가 담당하므로 여기서는 하지 않음.
        /// </summary>
        public void StartRun()
        {
            CurrentStage     = 1;
            CurrentEncounter = 1;
            TotalBattles     = 0;
            RunActive        = true;

            SaveSystem.DeleteSave();
            GameManager.Instance.spManager?.ResetForEncounter();

            Debug.Log($"[RunManager] 새 런 시작 대기 — CharacterSelection으로 진행");
        }

        /// <summary>
        /// 저장된 런 복원. 세이브 데이터에서 팀 구성을 읽고 아군을 다시 소환한 뒤 HP 복원.
        /// </summary>
        public void LoadSavedRun()
        {
            RunSaveData save = SaveSystem.LoadRun();
            if (save == null)
            {
                Debug.LogWarning("[RunManager] 저장 데이터 없음 — 새 런 시작");
                StartRun();
                return;
            }

            CurrentStage     = save.currentStage;
            CurrentEncounter = save.currentEncounter;
            TotalBattles     = save.totalBattles;
            RunActive        = true;

            GameManager.Instance.spManager?.ResetForEncounter();

            // 저장된 팀 구성으로 아군 소환
            RestoreHeroSpawns(save.heroTeam);
            // 적 큐 로드
            GameManager.Instance.roundManager.LoadRound(GetRoundIndex());
            // HP 복원
            RestoreHeroStats(save.heroTeam);

            Debug.Log($"[RunManager] 런 복원 — Stage {CurrentStage}, Encounter {CurrentEncounter}");
        }

        /// <summary>
        /// CharacterSelectionManager 확인 후 호출. 적 큐만 로드 (아군은 이미 소환됨).
        /// </summary>
        public void LoadCurrentRound()
        {
            GameManager.Instance.spManager?.ResetForEncounter();
            GameManager.Instance.roundManager.LoadRound(GetRoundIndex());
            Debug.Log($"[RunManager] 라운드 로드 완료 — Stage {CurrentStage}, Encounter {CurrentEncounter}");
        }

        // ── 인카운터 진행 ─────────────────────────────────────────────────────────

        /// <summary>전투 승리 후 다음 인카운터로 이동</summary>
        public void AdvanceEncounter()
        {
            TotalBattles++;
            GameManager.Instance.spManager?.ResetForEncounter();

            // 인카운터 먼저 증가 (저장보다 앞서야 다음 인카운터를 정확히 저장)
            CurrentEncounter++;
            if (CurrentEncounter > BossEncounter)
            {
                CurrentEncounter = 1;
                CurrentStage++;
                if (CurrentStage > StagesPerRun)
                {
                    // 런 완료 — 세이브 삭제 후 RunComplete 상태로 전환
                    RunActive = false;
                    SaveSystem.DeleteSave();
                    // NextGameState가 RunComplete 케이스를 처리하려면 먼저 상태를 설정해야 함
                    GameManager.Instance.gameState = GameState.RunComplete;
                    GameManager.Instance.NextGameState(false); // RunComplete → MainMenu
                    return;
                }
            }

            // 증가된 다음 인카운터 상태로 저장 (이전 인카운터 위치가 저장되는 버그 수정)
            SaveSystem.SaveRun(BuildSaveData());

            Debug.Log($"[RunManager] 다음 인카운터 — Stage {CurrentStage}, Encounter {CurrentEncounter} (보스: {IsBossEncounter})");
            // 적 큐만 재로드 (아군은 인카운터 간 HP 유지, 소환 상태 그대로)
            GameManager.Instance.roundManager.LoadRound(GetRoundIndex());
        }

        // ── 라운드 인덱스 계산 ────────────────────────────────────────────────────

        /// <summary>(stage-1)*4 + encounter → 70_rounds.yaml roundNumber</summary>
        public int GetRoundIndex()
        {
            return (CurrentStage - 1) * BossEncounter + CurrentEncounter;
        }

        // ── 히어로 HP 비파괴적 유지 ───────────────────────────────────────────────

        /// <summary>현재 히어로 목록에서 RunSaveData 구성 (그리드 라인 포함)</summary>
        private RunSaveData BuildSaveData()
        {
            var heroTeam = new List<UnitSaveData>();
            foreach (var hero in GridManager.Instance.heroList)
            {
                if (!hero.isActive) continue;
                heroTeam.Add(new UnitSaveData
                {
                    unitId       = hero.ID,
                    currentHP    = hero.HpCurr,
                    atkBaseBonus = 0,
                    defBaseBonus = 0,
                    hpBaseBonus  = 0,
                    gridLine     = hero.CurrentLine.ToString(),
                    slotIndex    = hero.currentCell?.slotIndex ?? 0,
                });
            }
            return new RunSaveData
            {
                currentStage     = CurrentStage,
                currentEncounter = CurrentEncounter,
                totalBattles     = TotalBattles,
                heroTeam         = heroTeam,
            };
        }

        /// <summary>저장 데이터로 아군 소환 (그리드 라인/슬롯 복원)</summary>
        private void RestoreHeroSpawns(List<UnitSaveData> savedHeroes)
        {
            foreach (var saved in savedHeroes)
            {
                bool isFrontline = saved.gridLine != "Backline";
                GridManager.Instance.SpawnHero(saved.unitId, isFrontline, saved.slotIndex);
            }
        }

        /// <summary>저장 데이터로 히어로 HP 복원 (소환 이후 호출)</summary>
        private void RestoreHeroStats(List<UnitSaveData> savedHeroes)
        {
            foreach (var hero in GridManager.Instance.heroList)
            {
                if (!hero.isActive) continue;
                var saved = savedHeroes.FirstOrDefault(s => s.unitId == hero.ID);
                if (saved == null) continue;
                hero.ModifyHp(saved.currentHP);
            }
        }
    }
}
