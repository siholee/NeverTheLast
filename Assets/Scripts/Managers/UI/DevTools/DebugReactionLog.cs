#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Entities;
using UnityEngine;

namespace Managers.UI.DevTools
{
    /// <summary>
    /// 일어난 원소 반응을 순서대로 적어 둔다.
    ///
    /// 반응은 28쌍이고 한 부착이 여러 쌍을 동시에 성립시킬 수 있다. 콘솔 로그는
    /// 다른 로그에 섞여 흘러가 버려 <b>"왜 이 반응이 나왔는가"를 되짚을 수가 없다.</b>
    /// 여기에 모아 두면 우선순위·내부 쿨다운이 의도대로 골랐는지 사후에 확인할 수 있다.
    ///
    /// 자동 검증 스위트도 이 기록을 읽어 "무엇이 터졌는가"를 판정한다.
    /// </summary>
    public static class DebugReactionLog
    {
        public readonly struct Entry
        {
            public readonly float Time;
            public readonly int Turn;
            public readonly string Source;
            public readonly string Target;
            public readonly string Reaction;

            public Entry(Unit source, Unit target, string reaction)
            {
                Time = UnityEngine.Time.time;
                Turn = target != null ? target.TurnCount : 0;
                Source = source != null ? source.UnitName : "?";
                Target = target != null ? target.UnitName : "?";
                Reaction = reaction;
            }

            public override string ToString()
                => $"{Time,8:F2}s  T{Turn,-3} {Reaction,-8} {Source} → {Target}";
        }

        private static readonly List<Entry> Entries = new();
        private static bool _hooked;

        /// <summary>
        /// 지금 보고 있는 창의 시작 위치. <see cref="Restart"/>가 여기만 옮기고 기록은 지우지 않는다.
        ///
        /// 검사 하나하나는 "직전 부착으로 무엇이 터졌는가"만 보면 되지만,
        /// 저장 파일은 <b>전체 흐름</b>이 남아야 쓸모가 있다. 예전에는 Restart가 목록을 비워
        /// 마지막 검사 몇 건만 저장되고 나머지가 사라졌다.
        /// </summary>
        private static int _windowStart;

        /// <summary>기록 중인가.</summary>
        public static bool Recording { get; private set; }

        /// <summary>현재 창에 쌓인 건수.</summary>
        public static int Count => Entries.Count - _windowStart;

        /// <summary>전체 기록 건수.</summary>
        public static int TotalCount => Entries.Count;

        /// <summary>현재 창의 가장 최근 반응 이름. 없으면 빈 문자열이다.</summary>
        public static string Last => Count > 0 ? Entries[^1].Reaction : "";

        public static void Start()
        {
            if (!_hooked)
            {
                Unit.AnyElementalReaction += Record;
                _hooked = true;
            }
            Recording = true;
        }

        public static void Stop() => Recording = false;

        public static void Clear()
        {
            Entries.Clear();
            _windowStart = 0;
        }

        /// <summary>새 창을 연다. <b>기록은 지우지 않는다</b> — 저장 파일에는 전체가 남는다.</summary>
        public static void Restart()
        {
            _windowStart = Entries.Count;
            Start();
        }

        /// <summary>현재 창에 일어난 반응 이름을 순서대로.</summary>
        public static List<string> Names()
            => Entries.Skip(_windowStart).Select(entry => entry.Reaction).ToList();

        /// <summary>대상별로 걸러 본다.</summary>
        public static List<string> NamesFor(Unit target)
            => Entries.Where(entry => entry.Target == (target != null ? target.UnitName : "?"))
                      .Select(entry => entry.Reaction).ToList();

        private static void Record(Unit source, Unit target, string reaction)
        {
            if (!Recording) return;
            Entries.Add(new Entry(source, target, reaction));
        }

        /// <summary><c>Logs/ReactionLog.txt</c>에 떨군다. 저장한 경로를 돌려준다.</summary>
        public static string Dump()
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "ReactionLog.txt");

            var text = new StringBuilder();
            text.AppendLine($"# 원소 반응 기록  {DateTime.Now:yyyy-MM-dd HH:mm:ss}  (전체 {Entries.Count}건)");
            foreach (Entry entry in Entries) text.AppendLine(entry.ToString());

            var counts = Entries.GroupBy(entry => entry.Reaction)
                                .OrderByDescending(group => group.Count());
            text.AppendLine();
            text.AppendLine("# 반응별 횟수");
            foreach (var group in counts) text.AppendLine($"  {group.Key,-8} {group.Count()}");

            File.WriteAllText(path, text.ToString());
            Debug.Log($"[디버그] 반응 기록 {Entries.Count}건을 저장했다 — {path}");
            return path;
        }
    }
}
#endif
