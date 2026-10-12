using System;
using System.Linq;
using Codes.Base;
using Entities;
using Managers;

namespace Combat
{
    /// <summary>행동서열 위 발동 배너가 구분해 표시하는 전투 사건.</summary>
    public enum ActionAnnouncementKind
    {
        Normal,
        Substitute,
        Additional,
        Coordinated,
        Ultimate,
        Special,
        Passive,
    }

    /// <summary>전투 로직과 UI 사이에 전달하는 한 줄 발동 공지.</summary>
    public readonly struct ActionAnnouncement
    {
        public readonly Unit Unit;
        public readonly ActionAnnouncementKind Kind;
        public readonly string UnitName;
        public readonly string KindLabel;
        public readonly string ActionName;
        public readonly bool IsEnemy;
        /// <summary>예약된 실제 행동의 종료까지 확장 슬롯을 유지한다.</summary>
        public readonly bool FollowsAction;

        public ActionAnnouncement(Unit unit, ActionAnnouncementKind kind, string actionName, bool followsAction = false)
        {
            Unit = unit;
            FollowsAction = followsAction;
            Kind = kind;
            UnitName = unit != null ? unit.UnitName : "?";
            KindLabel = LabelFor(kind);
            ActionName = string.IsNullOrWhiteSpace(actionName) ? "행동" : actionName.Trim();
            IsEnemy = unit != null && unit.IsEnemy;
        }

        public static string LabelFor(ActionAnnouncementKind kind) => kind switch
        {
            ActionAnnouncementKind.Normal => "일반행동",
            ActionAnnouncementKind.Substitute => "대체행동",
            ActionAnnouncementKind.Additional => "추가행동",
            ActionAnnouncementKind.Coordinated => "협동행동",
            ActionAnnouncementKind.Ultimate => "궁극기(초월기)",
            ActionAnnouncementKind.Special => "특수행동",
            ActionAnnouncementKind.Passive => "패시브(특성)",
            _ => "행동",
        };
    }

    /// <summary>
    /// 실제로 성립한 행동·특성만 좌상단 발동 배너에 전달하는 공용 입구.
    /// 조건을 살피는 단계에서는 호출하지 않고, 자원·상태·피해·예약 행동이 확정된 순간에만 부른다.
    /// </summary>
    public static class ActionAnnouncer
    {
        public static event Action<ActionAnnouncement> Announced;

        public static void Announce(Unit unit, ActionAnnouncementKind kind, string actionName, bool followsAction = false)
        {
            if (unit == null || string.IsNullOrWhiteSpace(actionName)) return;
            Announced?.Invoke(new ActionAnnouncement(unit, kind, actionName, followsAction));
        }

        public static void Announce(Unit unit, ActionAnnouncementKind kind, Code code, string fallback = null)
            => Announce(unit, kind, DisplayName(code, fallback));

        /// <summary>스케줄러가 막 실행할 행동의 슬롯과 20_codes 표시명을 함께 해석한다.</summary>
        public static void AnnounceScheduled(Unit unit, ActionScheduler.ActionKind kind, string fallback)
        {
            if (unit == null) return;

            ActionAnnouncementKind announcementKind;
            Code code = null;
            string name = fallback;

            switch (kind)
            {
                case ActionScheduler.ActionKind.Normal:
                    NormalCode normal = unit.ActiveNormalCode;
                    bool substitute = normal?.IsSubstituteAction == true;
                    announcementKind = substitute
                        ? ActionAnnouncementKind.Substitute
                        : ActionAnnouncementKind.Normal;
                    code = normal;
                    name = substitute ? normal.AnnouncementName : fallback;
                    break;
                case ActionScheduler.ActionKind.Ultimate:
                    announcementKind = ActionAnnouncementKind.Ultimate;
                    code = unit.ActiveUltimateCode;
                    break;
                case ActionScheduler.ActionKind.Special:
                    announcementKind = ActionAnnouncementKind.Special;
                    code = unit.ActiveSpecialCode;
                    break;
                case ActionScheduler.ActionKind.Coordinated:
                    announcementKind = ActionAnnouncementKind.Coordinated;
                    break;
                default:
                    UltimateCode persistentUltimate = unit.ActiveUltimateCode;
                    bool isPersistentUltimate = persistentUltimate?.IsPersistent == true &&
                        string.Equals(persistentUltimate.CodeName, fallback, StringComparison.Ordinal);
                    announcementKind = isPersistentUltimate
                        ? ActionAnnouncementKind.Ultimate
                        : ActionAnnouncementKind.Additional;
                    code = isPersistentUltimate
                        ? persistentUltimate
                        : unit.ActivePassiveCodes.FirstOrDefault(passive =>
                            passive != null && string.Equals(passive.CodeName, fallback, StringComparison.Ordinal));
                    break;
            }

            Announce(unit, announcementKind, DisplayName(code, name), followsAction: true);
        }

        public static void AnnouncePassive(Unit unit, PassiveCode passive)
            => Announce(unit, ActionAnnouncementKind.Passive, passive);

        private static string DisplayName(Code code, string fallback)
        {
            string verbal = code != null ? CodeCatalog.Find(code)?.verbalName : null;
            return !string.IsNullOrWhiteSpace(verbal)
                ? verbal
                : (string.IsNullOrWhiteSpace(fallback) ? code?.CodeName : fallback);
        }
    }
}
