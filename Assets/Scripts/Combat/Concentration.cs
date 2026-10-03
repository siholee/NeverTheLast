using System;
using System.Collections.Generic;
using BaseClasses;
using Entities;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// 집중 — 효과가 <b>시전 유닛을 기준으로</b> 지속되고, 시전 유닛이 행동불능이 되면 풀린다.
    ///
    ///   · 지속 턴은 효과를 받은 쪽이 아니라 <b>시전자의 턴</b>으로 센다(시전자의 턴이 끝날 때 1씩 준다).
    ///   · 시전자가 행동불능(기절 · 빙결 · 에어본 · 속박)에 걸리거나 쓰러지면 즉시 풀린다.
    ///   · 라운드가 끝나도 풀린다.
    ///   · 같은 시전자가 같은 키로 다시 집중하면 앞의 것을 풀고 새로 건다.
    ///
    /// 효과 자체(버프 상태 등)는 호출한 쪽이 걸고, 풀릴 때 할 일을 <c>onEnd</c>로 넘긴다.
    /// 이 클래스는 <b>언제 풀리는가</b>만 책임진다. 규칙의 원본은 Detail_03 §4.5-D다.
    /// </summary>
    public static class Concentration
    {
        /// <summary>풀린 이유. 연출이 '끊겼다'와 '다 됐다'를 다르게 보여 줄 수 있게 넘긴다.</summary>
        public enum EndReason
        {
            Expired,
            Interrupted,
            Replaced,
            RoundEnd,
        }

        private sealed class Session
        {
            public Unit Caster;
            public string Key;
            public int TurnsLeft;
            public Action<EndReason> OnEnd;
            public Action<EventContext> TurnEndHandler;
            public Action<EventContext> InterruptHandler;
            public Action<EventContext> RoundEndHandler;
        }

        private static readonly List<Session> Sessions = new();

        /// <summary>집중을 시작한다. <paramref name="turns"/>는 시전자의 턴 수다.</summary>
        public static void Begin(Unit caster, string key, int turns, Action<EndReason> onEnd)
        {
            if (caster == null || string.IsNullOrEmpty(key)) return;
            End(caster, key, EndReason.Replaced);

            var session = new Session { Caster = caster, Key = key, TurnsLeft = Mathf.Max(1, turns), OnEnd = onEnd };
            session.TurnEndHandler = _ =>
            {
                if (--session.TurnsLeft <= 0) Finish(session, EndReason.Expired);
            };
            session.InterruptHandler = _ => Finish(session, EndReason.Interrupted);
            session.RoundEndHandler = _ => Finish(session, EndReason.RoundEnd);

            caster.AddListener(BaseEnums.UnitEventType.OnTurnEnd, session.TurnEndHandler);
            caster.AddListener(BaseEnums.UnitEventType.OnControlStarts, session.InterruptHandler);
            caster.AddListener(BaseEnums.UnitEventType.OnDeath, session.InterruptHandler);
            caster.AddListener(BaseEnums.UnitEventType.OnRoundEnd, session.RoundEndHandler);
            Sessions.Add(session);
            Debug.Log($"[집중] {caster.UnitName} — {key} {session.TurnsLeft}턴");
        }

        public static bool IsActive(Unit caster, string key) => Find(caster, key) != null;

        /// <summary>남은 턴(시전자 기준). 집중 중이 아니면 0.</summary>
        public static int TurnsLeft(Unit caster, string key) => Find(caster, key)?.TurnsLeft ?? 0;

        public static void End(Unit caster, string key, EndReason reason)
        {
            Session session = Find(caster, key);
            if (session != null) Finish(session, reason);
        }

        private static Session Find(Unit caster, string key)
        {
            for (int i = 0; i < Sessions.Count; i++)
            {
                if (ReferenceEquals(Sessions[i].Caster, caster) && Sessions[i].Key == key) return Sessions[i];
            }
            return null;
        }

        private static void Finish(Session session, EndReason reason)
        {
            if (!Sessions.Remove(session)) return;

            Unit caster = session.Caster;
            if (caster != null)
            {
                caster.RemoveListener(BaseEnums.UnitEventType.OnTurnEnd, session.TurnEndHandler);
                caster.RemoveListener(BaseEnums.UnitEventType.OnControlStarts, session.InterruptHandler);
                caster.RemoveListener(BaseEnums.UnitEventType.OnDeath, session.InterruptHandler);
                caster.RemoveListener(BaseEnums.UnitEventType.OnRoundEnd, session.RoundEndHandler);
            }

            Debug.Log($"[집중] {caster?.UnitName} — {session.Key} 종료 ({reason})");
            session.OnEnd?.Invoke(reason);
        }
    }
}
