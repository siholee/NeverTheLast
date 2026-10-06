using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Effects.Negative;

namespace Managers.UI.Core
{
    /// <summary>플레이어용 설명. Rework Detail 01/02/03/05/16/17과 함께 갱신한다.</summary>
    public static class GameGuideCatalog
    {
        public sealed class Article
        {
            public string Id, Title, Chapter, Summary, Detail, Example;
            public string[] Related;
            public Article(string id, string title, string chapter, string summary, string detail,
                string example = null, params string[] related)
            { Id = id; Title = title; Chapter = chapter; Summary = summary; Detail = detail; Example = example; Related = related; }
        }

        public static readonly IReadOnlyList<Article> Guides = new[]
        {
            new Article("start", "첫 여정의 준비", "01 · 시작하기", "승부는 전투 전에 결정됩니다.",
                "해금한 캐릭터 1~5명으로 파티를 편성하세요. 모두 동등한 파티원이며 각자의 특성·일반행동·초월기를 가지고 있습니다.\n\n준비 단계에서 배치를 정하고, 상점과 휴식으로 다음 전투를 준비합니다. Tab에서 파티원의 스탯·스킬·보상 코드를 살펴볼 수 있습니다. 전투를 시작하면 행동과 초월기는 자동으로 진행됩니다.",
                "전열에 버틸 수 있는 지원가를 배치하고, 후열의 딜러에게 공격을 강화하는 코드를 모아 보세요.", "placement", "rewardcodes", "rest"),
            new Article("combat", "차례와 자동 전투", "02 · 전투 읽기", "행동서열을 보면 다음 차례를 알 수 있습니다.",
                "속도가 높으면 일반행동까지 필요한 행동치가 줄어듭니다. 일반행동은 자신의 턴을 진행하지만, 추가행동·특수행동·초월기는 일반행동의 차례를 소모하지 않습니다.\n\n일반행동으로 마나 20, 직접 피격으로 5, 처치로 10을 얻는 것이 기본입니다. 지속피해와 덧붙는 피해는 피격 마나를 주지 않습니다. 마나가 충분해지면 초월기가 자동 예약됩니다. 별도 자원이나 특수 발동 조건을 가진 캐릭터는 해당 스킬 설명을 확인하세요.",
                "속도만으로 모든 행동 순서를 예측할 수는 없습니다. 조건으로 예약된 추가행동이나 초월기가 먼저 끼어들 수 있습니다.", "av", "additional", "special", "concentration"),
            new Article("elements", "원소를 조합하는 법", "03 · 원소와 상태", "캐릭터의 원소와 실제 원소 부착은 다릅니다.",
                "불·물·풀·전기·얼음·바람·바위의 일곱 원소를 사용합니다. 스킬에 원소 부착이 적혀 있어야 대상에게 원소를 남깁니다. 두 부착이 만나면 반응하며 같은 원소끼리도 반응할 수 있습니다.\n\n한 번에 성립하는 반응이 여러 개라면 하나만 선택됩니다. 증폭 → 확산 → 제어 → 즉발 → 지속피해 → 디버프 → 버프·보호막 순서가 기본입니다. 반응을 일으킨 유닛의 특수공격력이 위력을 결정합니다. 적을 강화하는 반응도 있으므로 부착 순서를 미리 설계하세요.",
                "바루나의 물 부착 뒤 아그니의 불 공격은 증발을 노릴 수 있습니다. 바람의 확산은 반응한 대상과 같은 진영 전체로 원소를 퍼뜨립니다.", "attachment", "vaporize", "spread", "dot"),
            new Article("build", "코드로 파티 완성하기", "04 · 성장과 빌드", "활성 코드와 보관 코드를 구분하세요.",
                "경험치로 레벨이 오르면 캐릭터의 기초 스탯과 레벨당 성장치가 반영됩니다. 보상 코드는 스탯과 전투 방식을 바꾸는 별도의 빌드 수단입니다.\n\n캐릭터마다 보상 코드 활성 칸 7개와 추가 보관함이 있습니다. 보관 중인 코드는 효과를 주지 않습니다. 준비 단계에서 Tab을 열어 드래그하면 활성·보관 전환, 활성 칸 교환, 아군 간 이동을 무료로 할 수 있습니다. 전투 중에는 편집할 수 없습니다.\n\n상위 코드를 얻으면 대응하는 하위 코드가 있던 위치에서 바뀝니다. 활성 칸이 모두 찬 뒤 새 코드를 얻으면 개인 보관함으로 들어갑니다. 캐릭터의 고유 스킬은 이 7칸을 사용하지 않습니다.",
                "방어 코드를 전열 지원가에게, 반응 코드를 원소 반응을 유발하는 캐릭터에게 옮겨 보세요.", "rewardcodes", "stats", "specialattack"),
            new Article("supply", "상점과 야영", "05 · 여정 관리", "지금의 강화와 다음 휴식을 함께 계산하세요.",
                "상점에서는 보상 코드와 보급품을 구매합니다. 가격은 현재 스테이지에 비례하며, 준비 단계마다 구매는 2회까지입니다. 코드 구매와 보급품 구매가 이 횟수를 함께 사용합니다.\n\n식량은 긴 휴식에 쓰이고, 전투 비약은 준비 단계에서 사용하여 다음 전투 1회 파티 전체를 강화합니다. 공용 보급은 파티가 함께 사용하는 물품 보관소입니다. 코드의 개인 보관함과는 별개입니다.",
                "쓰러진 아군을 되살려야 한다면 짧은 휴식보다 식량을 모아 긴 휴식을 선택하세요.", "rest", "rewardcodes"),
            new Article("defense", "버티는 파티 만들기", "06 · 피해와 생존", "일반 피해와 지속피해를 서로 다른 방어로 막습니다.",
                "일반 공격 피해는 주로 방어력으로, 원소 반응과 지속피해는 특수방어력으로 경감합니다. 특수공격력이 높다고 모든 스킬의 직접 피해가 강해지는 것은 아닙니다. 스킬에 적힌 기준 스탯을 확인하세요.\n\n보호막은 여러 번 받으면 누적됩니다. 일반적인 보호막 스킬은 방어력과 보호막 보너스를, 치유 스킬은 최대 체력과 치유 보너스를 사용합니다. 원소 반응 등 예외는 각 효과 설명을 따릅니다. 지속피해는 보호막을 우회하므로 보호막만으로 안전해지지는 않습니다.",
                "집중으로 파티를 지원하는 캐릭터는 제어에 걸리면 집중이 풀립니다. 해당 시전자의 생존과 행동 가능 상태도 지켜 주세요.", "shield", "dot", "concentration", "specialdefense"),
        };

        public static readonly IReadOnlyList<Article> Terms = BuildTerms();
        private static List<Article> BuildTerms()
        {
            var terms = new List<Article>
            {
                new("concentration", "집중", "전투 규칙", "시전자의 턴과 행동 가능 상태에 효과의 지속을 묶는 규칙입니다.", "남은 집중 시간은 시전자의 턴이 끝날 때 줄어듭니다. 시전자가 행동불능에 걸리거나 쓰러지면 집중 효과가 해제됩니다. 같은 시전자가 같은 집중을 다시 걸면 이전 효과를 끝내고 새로 시작합니다.\n\n효과를 받은 아군의 피격이나 제어가 아니라 시전자 자신의 상태를 확인합니다.", "집중 버프를 받은 딜러가 맞아도 유지되지만, 시전자가 빙결되면 풀립니다.", "freeze", "control"),
                new("dot", "지속피해", "전투 규칙", "남아 있는 상태가 정해진 시점마다 피해를 주는 효과입니다.", "상태별 발동 시점과 지속시간을 확인하세요. 지속피해는 특수방어력으로 경감하며 보호막을 우회합니다. 야마의 지속피해 즉시 발동은 대상의 모든 지속피해를 활용하고, 상태와 남은 지속시간을 소모하지 않습니다.", "풍화는 턴 시작만이 아니라 대상의 행동 시작에 발동합니다.", "burn", "shock", "weathering"),
                new("shield", "보호막", "전투 규칙", "피해를 체력보다 먼저 흡수하며 여러 보호막은 누적됩니다.", "일반적인 보호막 생성 스킬은 방어력과 보호막 보너스를 사용합니다. 지속피해와 보호막 관통 피해는 보호막을 우회합니다. 원소 반응 경화처럼 다른 스탯을 사용하는 예외는 개별 설명을 따릅니다.", null, "dot", "crystallize"),
                new("av", "행동치", "전투 규칙", "다음 일반행동까지 필요한 차례 간격입니다. 속도가 높으면 짧아집니다.", "기본 행동치는 10000 ÷ 속도입니다. 스케줄러는 추월 제한과 행동 앞당김·지연도 반영합니다. 추가행동과 초월기는 일반행동의 행동치를 초기화하지 않습니다.", null, "additional", "special"),
                new("additional", "추가행동", "전투 규칙", "일반행동의 턴을 소모하지 않고 조건에 따라 예약되는 행동입니다.", "우선 추가행동과 일반 추가행동이 있습니다. 특수행동과 협동행동은 별도 종류이므로 ‘추가행동 시’ 효과에 포함되지 않습니다. 추가행동의 피해 판정과 발동 조건은 개별 코드를 확인하세요.", null, "special", "av"),
                new("special", "특수행동", "전투 규칙", "인드라가 로카팔라에게 여는 별도의 행동 슬롯입니다.", "로카팔라 파티는 인드라의 초월기로 열린 특수행동을 활용합니다. 특수행동은 일반행동의 턴과 행동치를 소모하지 않으며 추가행동으로 세지 않습니다. 각 캐릭터의 특수행동 설명에서 공격·지원 효과를 확인하세요.", null, "additional"),
                new("placement", "전열", "편성과 성장", "적에게 우선적으로 노출되는 앞쪽 열입니다.", "각 진영은 전열과 후열에 각각 4칸을 가집니다. 전열은 기본 우선도가 높아 공격 대상이 되기 쉽지만 모든 공격을 대신 맞는 것은 아닙니다. 대기석은 전장 밖이며 전투에 참여하지 않습니다.", null, "shield"),
                new("rewardcodes", "보상 코드", "편성과 성장", "캐릭터마다 7개까지 활성화하고 추가로 보관할 수 있는 빌드 효과입니다.", "보상 선택이나 상점에서 얻습니다. 활성 코드만 전투·스탯·정산 효과를 주며 보관 코드는 효과가 없습니다. 준비 단계의 Tab에서 드래그하여 무료로 활성·보관 전환과 아군 간 교환을 할 수 있습니다. 같은 캐릭터가 같은 코드를 중복 보유할 수는 없습니다.", null, "stats"),
                new("stats", "스탯", "편성과 성장", "체력·공격·특공·방어·특방·속도·치명타 확률·치명타 피해의 8가지 능력치입니다.", "기초값 + 레벨당 성장치 × (레벨 − 1)로 계산하고 코드와 상태 효과를 더합니다. 훈련이나 중량은 사용하지 않습니다. 공격 스킬마다 기준 스탯이 다르므로 설명의 피해 계수를 확인하세요.", null, "specialattack", "specialdefense"),
                new("specialattack", "특수공격력", "편성과 성장", "원소 반응의 위력과 해로운 효과의 명중에 관여하는 능력치입니다.", "일반적인 스킬 직접 피해의 공통 계수는 아닙니다. 반응을 일으킨 캐릭터의 최종 특수공격력을 사용합니다. 직접 피해가 특수공격력을 사용하는 스킬은 개별 계수에 명시됩니다.", null, "attachment", "effecthit"),
                new("specialdefense", "특수방어력", "편성과 성장", "원소 반응·지속피해를 경감하고 해로운 효과에 저항하는 능력치입니다.", "일반 피해의 방어력과 구분됩니다. 공격자 특수공격력과의 관계는 효과 명중과 일부 제어 지속시간에도 관여합니다.", null, "dot", "control"),
                new("attachment", "원소 부착", "원소 반응", "대상에게 원소를 남겨 다른 부착과 반응시키는 효과입니다.", "타고난 원소와 부착은 별개입니다. 스킬에 부착 효과가 있어야 원소가 남습니다. 같은 원소끼리도 반응하며, 반응 우선순위와 내부 발동 제한 때문에 모든 조합이 동시에 터지지는 않습니다. 해로운 반응은 아군에게 적용하지 않는 보호 규칙이 있지만 이로운 반응은 적에게도 생길 수 있습니다.", null, "spread", "vaporize"),
                new("control", "제어", "전투 규칙", "행동을 막거나 차례를 늦추는 효과입니다.", "행동불능 제어를 받으면 시전 중인 집중이 해제됩니다. 제어 종류마다 막는 행동과 지속 조건이 다릅니다. 빙결·기절처럼 행동을 막는 효과와 착근처럼 차례만 늦추는 효과를 구분하세요.", null, "concentration", "freeze", "root"),
                new("effecthit", "효과 명중", "전투 규칙", "특수공격력과 상대 특수방어력으로 원소 부착·지속피해·디버프의 성공을 정합니다.", "적에게 거는 원소 부착·지속피해·디버프·제어·표식에 적용합니다. 통상 명중률은 75% × 2 × 특수공격력 / (특수공격력 + 대상 특수방어력), 최대 100%입니다. 같은 스탯이면 75%, 특공이 특방의 두 배면 100%입니다. 아군과 자기 자신에게는 저항 없이 적용합니다. 부착에 실패해도 공격 피해는 들어가며 기존 부착과 지속시간은 보존합니다. 성공한 부착에서 생기는 반응 상태와 이미 붙은 지속피해의 발동은 다시 명중을 굴리지 않습니다. 코드의 추가 상태 저항은 별도로 적용될 수 있습니다.", null, "specialattack", "specialdefense"),
                new("rest", "긴 휴식", "여정 관리", "식량 40으로 파티를 부활시키고 체력·마나를 완전히 회복합니다.", "짧은 휴식은 남은 횟수 1을 사용해 살아 있는 아군의 최대 체력 50%를 회복합니다. 부활과 마나 회복은 없습니다. 긴 휴식은 전원 부활·완전 회복과 짧은 휴식 횟수 2회 복원을 제공합니다. 준비 단계마다 휴식은 한 번만 선택합니다.", null, "shield"),
            };
            Add(terms, "burn", "화상", "불 + 불. 대상 턴 시작에 피해를 주는 지속피해입니다.", "원소 반응 화상은 2턴 동안 대상 최대 체력의 5%를 기준으로 피해를 줍니다. 불 부착의 효과 명중 성공을 상속합니다. 가시 등 반응이 아닌 화상은 개별 명중률을 따릅니다. 같은 화상은 피해가 겹쳐 쌓이는 대신 지속시간이 늘어납니다. 코드로 거는 화상은 해당 코드의 피해·지속시간을 따릅니다.");
            Add(terms, "shock", "감전", "물 + 전기. 대상 턴 시작에 피해를 주는 지속피해입니다.", "반응 유발자의 특수공격력 60%를 기준으로 2턴 동안 발동합니다. 출처별로 별개의 감전이 붙습니다. 야마가 있으면 감전된 적에게 죽음의 선고가 파생되어 파티 전체의 피해를 돕습니다.");
            Add(terms, "weathering", "풍화", "물 + 바위. 대상의 행동 시작에 피해를 주는 지속피해입니다.", "반응 유발자의 특수공격력 60%를 기준으로 2턴 지속합니다. 일반행동뿐 아니라 초월기·추가행동 시작에도 피해를 받으므로 행동이 잦은 적에게 유효합니다.");
            Add(terms, "vaporize", "증발", "불 + 물. 방금 들어간 공격을 강화하는 증폭 반응입니다.", "같은 대상에게 방금 가한 직접 피해를 되짚어 추가 피해로 환산합니다. 반응 유발자의 특수공격력이 증폭을 결정합니다. 피해 없는 부착만으로는 증폭이 성립하지 않습니다.");
            Add(terms, "burning", "연소", "불 + 풀. 방금 공격의 피해를 증폭합니다.", "증발과 같은 증폭 규칙을 사용합니다. 이름과 달리 지속피해 화상과는 다른 반응입니다.");
            Add(terms, "melt", "융해", "불 + 얼음. 방금 공격의 피해를 증폭합니다.", "증발과 같은 증폭 규칙을 사용합니다. 반응 유발자의 특수공격력이 증폭 위력을 결정합니다.");
            Add(terms, "spread", "확산", "바람이 만난 원소를 대상과 같은 진영 전체에 퍼뜨립니다.", "바람 + 다른 원소는 그 원소를 대상과 같은 진영 전체에 퍼뜨립니다. 새 수신자마다 효과 명중을 판정하며, 퍼진 부착은 다시 반응하지 않습니다. 바람 + 바람은 같은 진영 전체에 특수공격력 70% 피해를 주는 즉발 반응입니다.");
            Add(terms, "freeze", "빙결", "물 + 얼음. 대상을 행동불능 상태로 만듭니다.", "반응 제어는 유발자의 특수공격력과 대상 특수방어력의 비율에 따라 1~3턴 지속합니다. 집중 시전자에게 걸리면 집중을 해제합니다.");
            Add(terms, "vibration", "진동", "바위 + 바위. 대상에게 기절을 겁니다.", "유발자의 특수공격력과 대상 특수방어력의 비율에 따라 1~2턴 지속합니다. 집중 시전자를 기절시키면 집중이 해제됩니다.");
            Add(terms, "root", "착근", "풀 + 풀. 다음 일반행동의 차례를 뒤로 늦춥니다.", "한 행동에 필요한 행동치의 50%만큼 지연합니다. 행동불능이 아니라 지연이므로 빙결·진동과 다른 제어입니다.");
            Add(terms, "overload", "과부하", "불 + 전기. 즉시 반응 피해를 줍니다.", "유발자의 특수공격력 120%를 기준으로 피해를 줍니다. 지속피해를 남기는 반응은 아닙니다.");
            Add(terms, "quicken", "활성", "풀 + 전기. 특수방어력으로 경감하는 피해를 더 받게 합니다.", "원소 반응과 지속피해를 돕는 디버프입니다. 방어력으로 경감하는 일반 피해에는 초전도를 사용합니다.");
            Add(terms, "superconduct", "초전도", "전기 + 얼음. 방어력으로 경감하는 피해를 더 받게 합니다.", "일반적인 직접 피해를 돕는 디버프입니다. 특수방어력으로 경감하는 원소 반응·지속피해에는 활성을 사용합니다.");
            Add(terms, "slow", "둔화", "얼음 + 얼음. 속도를 낮춥니다.", "반응 둔화는 3턴 동안 속도를 낮춥니다. 효과의 세기는 반응 유발자의 특수공격력을 따릅니다.");
            Add(terms, "growth", "성장", "풀 + 바위. 방어력을 올리는 반응입니다.", "3턴 지속하는 이로운 반응입니다. 적에게도 생길 수 있으므로 원소 부착 순서에 주의하세요.");
            Add(terms, "forge", "단조", "불 + 바위. 속도를 올리는 반응입니다.", "3턴 지속하는 이로운 반응입니다. 상대를 강화할 수도 있습니다.");
            Add(terms, "bloom", "개화", "물 + 풀. 최대 체력을 올리는 반응입니다.", "3턴 지속하는 이로운 반응입니다. 원소 반응은 항상 적에게 불리한 효과만 만들지는 않습니다.");
            Add(terms, "essence", "정수", "물 + 물. 특수공격력을 올리는 반응입니다.", "3턴 지속하는 이로운 반응입니다. 반응과 효과 명중을 돕습니다.");
            Add(terms, "charge", "축전", "전기 + 전기. 공격력을 올리는 반응입니다.", "3턴 지속하는 이로운 반응입니다. 이로운 반응은 적에게도 적용될 수 있습니다.");
            Add(terms, "crystallize", "경화", "얼음 + 바위. 보호막을 부여하는 반응입니다.", "유발자의 특수공격력 150%를 기준으로 보호막을 만듭니다. 일반적인 방어력 기반 보호막 스킬과 구분되는 원소 반응 예외입니다.");
            Add(terms, "ground", "접지", "전기 + 바위. 두 원소가 소멸합니다.", "피해나 강화를 만드는 대신 반응에 참여한 두 원소를 없앱니다.");
            Add(terms, "dormant", "휴면", "풀 + 얼음. 두 원소가 소멸합니다.", "피해나 강화를 만드는 대신 반응에 참여한 두 원소를 없앱니다.");
            Unique(terms, "verdict", "죽음의 선고", "야마가 감전된 적에게 내려 파티 전체의 피해를 돕는 상태입니다.", Codes.Base.CodeCatalog.Slot.Passive, 261, "shock");
            Unique(terms, "genesis", "창세의 결정", "보호막을 두른 아군의 피격으로 세이가 쌓는 고유 중첩입니다.", Codes.Base.CodeCatalog.Slot.Passive, 201, "shield");
            Unique(terms, "soul", "영혼", "유닛의 사망으로 아누비스가 영구히 모으는 성장 중첩입니다.", Codes.Base.CodeCatalog.Slot.Passive, 321, "stats");
            Unique(terms, "wildfire", "맹화", "히미코의 표식입니다. 세 중첩이 모이면 추가 효과를 일으키고 지워집니다.", Codes.Base.CodeCatalog.Slot.Passive, 244, "concentration");
            Unique(terms, "wanted", "현상수배", "수리야의 공격과 응답 사격이 집중되는 적 표식입니다.", Codes.Base.CodeCatalog.Slot.Passive, 267, "additional");
            Unique(terms, "bullets", "탄환", "수리야가 아군의 공격에 응답 사격을 하기 위해 소비하는 자원입니다.", Codes.Base.CodeCatalog.Slot.Passive, 267, "wanted");
            Unique(terms, "stakes", "판돈", "아군 초월기를 강화하고 아그니의 특성 공격에도 궁극기 판정을 주는 효과입니다.", Codes.Base.CodeCatalog.Slot.Ultimate, 62, "additional");
            Unique(terms, "goldpower", "금력", "쿠베라의 골드 러쉬 위력을 보유 골드와 스테이지로 결정하는 배율입니다.", Codes.Base.CodeCatalog.Slot.Special, 65, "special");
            Unique(terms, "venom", "맹독", "피그말리온이 접촉 공격자에게 남기는 지속피해입니다.", Codes.Base.CodeCatalog.Slot.Ultimate, 20, "dot");
            return terms;
        }
        private static void Unique(List<Article> terms, string id, string title, string summary,
            Codes.Base.CodeCatalog.Slot slot, int codeId, params string[] related)
        {
            var code = Codes.Base.CodeCatalog.Find(slot, codeId);
            if (code == null) return;
            terms.Add(new(id, title, "캐릭터 메커니즘", summary,
                Codes.Base.CodeCatalog.Summary(code.description), null, related));
        }
        private static void Add(List<Article> terms, string id, string title, string summary, string detail)
            => terms.Add(new(id, title, "원소 반응", summary, detail, null, "attachment", "specialattack"));

        public static Article Find(string id) => Terms.Concat(Guides).FirstOrDefault(a => a.Id == id);
        private static readonly Dictionary<string, string> Aliases = BuildAliases();
        private static Dictionary<string, string> BuildAliases()
        {
            var aliases = Terms.ToDictionary(t => t.Title, t => t.Id);
            // 빌드의 ‘활성 코드’, 캐릭터의 ‘성장치’를 원소 반응으로 잘못 설명하지 않는다.
            aliases.Remove("활성"); aliases.Remove("성장");
            aliases["활성 반응"] = "quicken"; aliases["성장 반응"] = "growth";
            aliases["효과 저항"] = "effecthit";
            return aliases;
        }
        private static readonly Regex Words = new(string.Join("|", Aliases.Keys.OrderByDescending(k => k.Length).Select(Regex.Escape)), RegexOptions.Compiled);
        private static readonly Regex Tokens = new("(<[^>]*>)", RegexOptions.Compiled);

        /// <summary>TMP 서식은 보존하고 텍스트 부분만 링크화한다. 기존 link 내부는 다시 감싸지 않는다.</summary>
        public static string LinkTerms(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var sb = new StringBuilder(); int linkDepth = 0;
            foreach (string part in Tokens.Split(text))
            {
                if (part.StartsWith("<"))
                {
                    if (part == "<noparse>" || part == "</noparse>") continue;
                    if (part.StartsWith("<link")) linkDepth++;
                    if (part.StartsWith("</link")) linkDepth = Math.Max(0, linkDepth - 1);
                    sb.Append(part);
                }
                else sb.Append(linkDepth > 0 ? part : Words.Replace(part, m =>
                    m.Value == "집중" && part.Substring(m.Index + m.Length).StartsWith(" 포격") ? m.Value :
                    $"<link=\"term:{Aliases[m.Value]}\"><color=#177DA5><u>{m.Value}</u></color></link>"));
            }
            return sb.ToString();
        }
        public static string ArticleLink(string id, string label = null)
            => $"<link=\"guide:{id}\"><color=#177DA5><u>{label ?? Find(id)?.Title ?? id}</u></color></link>";
    }
}
