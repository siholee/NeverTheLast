using System.Collections.Generic;
using System.Linq;
using Core;

namespace Managers
{
    /// <summary>
    /// `30_synergies.yaml`을 한 번 읽어 두고 질문에 답한다.
    ///
    /// 자료실이 유닛 하나를 보여 줄 때 묻는 것은 둘 중 하나다.
    ///   · 이 캐릭터를 <b>메인</b>으로 쓰면 어떤 조합인가          → <see cref="RecommendationFor"/>
    ///   · 이 캐릭터는 <b>서포트</b>로 어떤 메인에게 추천되는가     → <see cref="RecommendedFor"/>
    ///
    /// 데이터 파일이 없어도 화면이 죽지 않도록 전부 빈 값을 돌려준다.
    /// </summary>
    public static class SynergyCatalog
    {
        private static SynergyDataList _data;
        private static Dictionary<int, SynergyUnitData> _units;
        private static Dictionary<int, SynergyRecommendationData> _recommendations;
        private static Dictionary<string, SynergyRoleData> _roles;
        private static Dictionary<string, SynergyArchetypeData> _archetypes;

        private static void EnsureLoaded()
        {
            if (_data != null) return;

            _data = GameManager.Instance?.dataManager?.FetchSynergyDataList() ?? new SynergyDataList();
            _units = (_data.units ?? new List<SynergyUnitData>())
                .Where(unit => unit != null).ToDictionary(unit => unit.id);
            _recommendations = (_data.recommendations ?? new List<SynergyRecommendationData>())
                .Where(entry => entry != null).ToDictionary(entry => entry.mainId);
            _roles = (_data.roles ?? new List<SynergyRoleData>())
                .Where(role => role?.id != null).ToDictionary(role => role.id);
            _archetypes = (_data.archetypes ?? new List<SynergyArchetypeData>())
                .Where(entry => entry?.id != null).ToDictionary(entry => entry.id);
        }

        /// <summary>런을 새로 시작하거나 데이터를 다시 읽어야 할 때.</summary>
        public static void Invalidate() => _data = null;

        /// <summary>
        /// 표를 실제로 읽어 냈는가. 화면이 비었을 때
        /// <b>"파일을 못 읽었다"</b>와 <b>"이 캐릭터만 표에 없다"</b>를 갈라 말하는 데 쓴다.
        /// </summary>
        public static bool IsLoaded
        {
            get
            {
                EnsureLoaded();
                return _units.Count > 0;
            }
        }

        public static SynergyUnitData UnitOf(int unitId)
        {
            EnsureLoaded();
            return _units.GetValueOrDefault(unitId);
        }

        /// <summary>이 유닛을 메인으로 세울 때의 추천. 서포트 전용 캐릭터면 null.</summary>
        public static SynergyRecommendationData RecommendationFor(int unitId)
        {
            EnsureLoaded();
            return _recommendations.GetValueOrDefault(unitId);
        }

        /// <summary>역할 ID를 화면에 쓸 한국어 이름으로.</summary>
        public static string RoleName(string roleId)
        {
            EnsureLoaded();
            return _roles.TryGetValue(roleId ?? string.Empty, out SynergyRoleData role) && role.name != null
                ? role.name
                : roleId;
        }

        public static string ArchetypeName(string archetypeId)
        {
            EnsureLoaded();
            return _archetypes.TryGetValue(archetypeId ?? string.Empty, out SynergyArchetypeData entry) &&
                   entry.name != null
                ? entry.name
                : archetypeId;
        }

        /// <summary>이 유닛이 서포트로 뽑히는 메인들. (메인 이름, 고점인가) 목록.</summary>
        public static List<(string MainName, bool IsBest)> RecommendedFor(int unitId)
        {
            EnsureLoaded();

            var found = new List<(string, bool)>();
            foreach (SynergyRecommendationData entry in _recommendations.Values)
            {
                bool best = entry.best?.members?.Contains(unitId) == true;
                bool basic = entry.basic?.members?.Contains(unitId) == true;
                if (best || basic) found.Add((entry.mainName, best));
            }

            // 고점에 뽑히는 쪽을 먼저, 그 다음 이름순. 화면 순서가 실행마다 흔들리지 않게 한다.
            return found.OrderByDescending(pair => pair.Item2).ThenBy(pair => pair.Item1).ToList();
        }

        /// <summary>
        /// 지금 이 캐릭터를 실제로 편성할 수 있는가. 추천 목록에 회색으로 표시할지 판단한다.
        /// Support 유형은 언제나 서포트 슬롯에 오를 수 있다.
        /// </summary>
        public static bool IsAvailable(int unitId)
        {
            SynergyUnitData unit = UnitOf(unitId);
            if (unit == null) return false;
            if (unit.type == "Support") return true;
            return SaveSystem.IsStarterUnlocked(unitId) || SaveSystem.IsCharacterTrained(unitId);
        }

        public static string NameOf(int unitId) => UnitOf(unitId)?.name ?? $"#{unitId}";

        /// <summary>역할군 정의 전체. 자료실의 편성 용어 항목이 읽는다. 파일 순서 그대로.</summary>
        public static IReadOnlyList<SynergyRoleData> Roles
        {
            get
            {
                EnsureLoaded();
                return _data.roles ?? new List<SynergyRoleData>();
            }
        }

        /// <summary>조합 유형 정의 전체. 파일 순서 그대로.</summary>
        public static IReadOnlyList<SynergyArchetypeData> Archetypes
        {
            get
            {
                EnsureLoaded();
                return _data.archetypes ?? new List<SynergyArchetypeData>();
            }
        }
    }
}
