using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using YamlDotNet.Serialization;

namespace Codes.Base
{
    /// <summary>
    /// `20_codes.yaml`의 표시 데이터(이름·설명)를 한 번 읽어 두고 답한다.
    ///
    /// 코드 클래스는 전투 동작만 들고 있고 "이 코드가 무엇을 하는가"는 이 표에만 있다.
    /// 캐릭터 시트의 코드 목록과 자료실이 같은 문장을 보여 주도록 설명은 여기서만 꺼낸다.
    ///
    /// <b>ID는 슬롯마다 따로다.</b> 패시브 22와 궁극기 22는 무관하므로 반드시 슬롯과 함께 묻는다.
    /// 파일이 없거나 항목이 빠져도 화면이 죽지 않게 전부 빈 값을 돌려준다.
    /// </summary>
    public static class CodeCatalog
    {
        public enum Slot
        {
            Passive,
            Normal,
            Special,
            Ultimate,
        }

        public sealed class Entry
        {
            public int id;
            public string verbalName;
            public string codeName;
            public string description;
        }

        public sealed class CatalogFile
        {
            public Dictionary<string, List<Entry>> codes;
        }

        private static Dictionary<Slot, Dictionary<int, Entry>> _byId;

        /// <summary>팩토리를 거치지 않은 코드를 위한 보조 색인. 클래스 이름이 하나뿐인 것만 담는다.</summary>
        private static Dictionary<Slot, Dictionary<string, Entry>> _byClass;

        private static readonly Regex MarkerPattern = new(@"<[^<>]*>|#\S+", RegexOptions.Compiled);
        private static readonly Regex SpacePattern = new(@"\s{2,}", RegexOptions.Compiled);

        private static void EnsureLoaded()
        {
            if (_byId != null) return;

            _byId = new Dictionary<Slot, Dictionary<int, Entry>>();
            _byClass = new Dictionary<Slot, Dictionary<string, Entry>>();
            foreach (Slot slot in System.Enum.GetValues(typeof(Slot)))
            {
                _byId[slot] = new Dictionary<int, Entry>();
                _byClass[slot] = new Dictionary<string, Entry>();
            }

            TextAsset asset = Resources.Load<TextAsset>("Data/20_codes");
            if (asset == null)
            {
                Debug.LogError("[코드 목록] Data/20_codes.yaml을 찾지 못했다. 코드 설명이 비어 보인다.");
                return;
            }

            CatalogFile file = new DeserializerBuilder().IgnoreUnmatchedProperties().Build()
                .Deserialize<CatalogFile>(asset.text);
            if (file?.codes == null) return;

            foreach (KeyValuePair<string, List<Entry>> pair in file.codes)
            {
                if (!TryParseSlot(pair.Key, out Slot slot) || pair.Value == null) continue;

                var ambiguous = new HashSet<string>();
                foreach (Entry entry in pair.Value.Where(entry => entry != null))
                {
                    // 같은 ID가 두 번 적혔으면 먼저 적힌 쪽을 쓴다. 검증 메뉴가 중복을 따로 잡는다.
                    _byId[slot].TryAdd(entry.id, entry);

                    if (string.IsNullOrWhiteSpace(entry.codeName)) continue;
                    if (!_byClass[slot].TryAdd(entry.codeName, entry)) ambiguous.Add(entry.codeName);
                }

                // 여러 항목이 한 클래스를 나눠 쓰면(마커 패시브 등) 이름만으로는 어느 것인지 모른다.
                foreach (string name in ambiguous) _byClass[slot].Remove(name);
            }
        }

        private static bool TryParseSlot(string key, out Slot slot)
        {
            switch (key?.Trim().ToLowerInvariant())
            {
                case "passive": slot = Slot.Passive; return true;
                case "normal": slot = Slot.Normal; return true;
                case "special": slot = Slot.Special; return true;
                case "ultimate": slot = Slot.Ultimate; return true;
                default: slot = default; return false;
            }
        }

        /// <summary>장비 codeGrants의 slot 문자열. 모르는 값은 패시브로 본다 — 장비가 주는 코드는 거의 패시브다.</summary>
        public static Slot ParseSlot(string key) => TryParseSlot(key, out Slot slot) ? slot : Slot.Passive;

        public static Slot SlotOf(Code code) => code switch
        {
            SpecialCode => Slot.Special,
            UltimateCode => Slot.Ultimate,
            PassiveCode => Slot.Passive,
            _ => Slot.Normal,
        };

        public static Entry Find(Slot slot, int codeId)
        {
            EnsureLoaded();
            return _byId[slot].GetValueOrDefault(codeId);
        }

        /// <summary>살아 있는 코드 인스턴스의 표시 데이터. 팩토리 ID가 없으면 클래스 이름으로 찾는다.</summary>
        public static Entry Find(Code code)
        {
            if (code == null) return null;
            EnsureLoaded();

            Slot slot = SlotOf(code);
            if (code.CatalogId > 0 && _byId[slot].TryGetValue(code.CatalogId, out Entry byId)) return byId;
            return _byClass[slot].GetValueOrDefault(code.GetType().Name);
        }

        /// <summary>한 슬롯의 전 항목. 자료실 목록용이라 ID 순으로 준다.</summary>
        public static IEnumerable<Entry> All(Slot slot)
        {
            EnsureLoaded();
            return _byId[slot].Values.OrderBy(entry => entry.id);
        }

        /// <summary>
        /// 목록 한 줄에 들어갈 설명. <c>&lt;고유 패시브&gt;</c> 같은 꼬리표와 <c>#물리</c> 같은
        /// 피해 태그를 걷어 낸다 — 그것들은 <see cref="Markers"/>로 따로 보여 준다.
        /// </summary>
        public static string Summary(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return string.Empty;
            string stripped = MarkerPattern.Replace(description, " ");
            return SpacePattern.Replace(stripped, " ").Trim();
        }

        /// <summary>설명에 붙은 꼬리표와 피해 태그. 등장 순서 그대로, 중복 없이.</summary>
        public static List<string> Markers(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return new List<string>();
            return MarkerPattern.Matches(description).Cast<Match>()
                .Select(match => match.Value.Trim('<', '>'))
                .Where(value => value.Length > 0)
                .Distinct()
                .ToList();
        }

        /// <summary>데이터를 다시 읽어야 할 때. 표는 런타임에 바뀌지 않으므로 평소엔 쓰지 않는다.</summary>
        public static void Invalidate()
        {
            _byId = null;
            _byClass = null;
        }
    }
}
