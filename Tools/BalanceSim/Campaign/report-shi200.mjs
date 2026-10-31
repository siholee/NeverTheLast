import fs from 'node:fs';import path from 'node:path';
const root=path.resolve(process.argv[2]),read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const d=read(root+'/analysis/results.json'),dd=read(root+'/analysis/shi200.json');
const op=read(root+'/operations/analysis/results.json'),od=read(root+'/operations/analysis/shi200.json');
const combined=read(root+'/combined/analysis/results.json'),cd=read(root+'/combined/analysis/shi200.json');
const fixed=read(root+'/preparation-fix/analysis/results.json'),fd=read(root+'/preparation-fix/analysis/shi200.json');
if(!d.complete||!op.complete||!dd.complete||!od.complete||!combined.complete||!cd.complete||!fixed.complete||!fd.complete)throw Error('모든 최종 런 완료 후 작성합니다.');
const cat=read(root+'/catalog.json'),plan=read(root+'/plan.json');
const base=dd.profiles.find(x=>x.team===plan.teams[0].key),ops=od.profiles[0];
const f=(x,n=1)=>x==null?'—':x.toLocaleString('ko-KR',{minimumFractionDigits:n,maximumFractionDigits:n});
const table=(heads,rows)=>['| '+heads.join(' | ')+' |','| '+heads.map(()=>'---').join(' | ')+' |',...rows.map(r=>'| '+r.join(' | ')+' |')].join('\n');
const unitName=id=>cat.units.find(x=>x.id===Number(id))?.name??String(id);
const enemyName=ids=>ids.split(',').map(id=>cat.enemies.find(x=>x.id===Number(id))?.name??id).join('·');
const group=rows=>Object.entries(rows.reduce((s,x)=>(s[x]=(s[x]??0)+1,s),{})).sort((a,b)=>b[1]-a[1]);
const baselineLosses=dd.failures.filter(x=>x.team===base.team);
const zeroShield=baselineLosses.filter(x=>x.shielding===0).length;
const manaShort=baselineLosses.filter(x=>x.heroes.find(u=>u.id===1)?.mana<120).length;
const growthReached=dd.profiles.filter(x=>x.team!==base.team&&x.entered200>0);
const observed=growthReached.length?growthReached.map(x=>x.name+': 진입 '+x.entered200+'/10·승리 '+x.cleared200+'/10').join(', '):'가상 성장 변경을 포함한 다섯 조건 모두200층 진입이 관측되지 않았다';
const labCases=[['30층 중보스',root+'/laboratory/corrected-6101602/summary.json'],['40층 중보스',root+'/laboratory/corrected-6101603/summary.json'],['42층 오딘',root+'/laboratory/result-6101606/summary.json']];
const lab=labCases.flatMap(([fixture,p])=>read(p).map(x=>({...x,fixture})));
const high=read(root+'/laboratory/high126/summary.json'),target=read(root+'/laboratory/target200/summary.json');
const codeCount=(data,team,unit,id)=>data.unitCodes.find(x=>x.team===team&&x.unit===unit&&x.code===id)?.activeRuns??0;
let restRows=[];
function walk(p){for(const e of fs.readdirSync(p,{withFileTypes:true})){const q=path.join(p,e.name);if(e.isDirectory())walk(q);else if(e.name==='resource_rests.jsonl')restRows.push(...fs.readFileSync(q,'utf8').trim().split(/\n/).filter(Boolean).map(JSON.parse))}}
walk(root+'/operations/campaign');
const text=`# 시 화력 기준 파티 — 200층 도달 진단

기준은 **시·세이·히미코·찬드라·오르페우스 / 시 화력 우선**이다. 전열 세이·오르페우스, 후열 시·히미코·찬드라를 유지했다. 현재 파수거신·군세장과 이후 군세장 반복을 유지하며 신규 고정보스는 추가하지 않았다.

**현재 수치·기준 운영의 200층 진입은 ${base.entered200}/10회, 200층 승리는 ${base.cleared200}/10회다.** 평균 ${f(base.meanReached)}층, 최고 ${base.maxReached}층이다. 실제 비용을 지불하는 운영 대조(구매 전 휴식 시도, 수정 전 정책)는 평균 ${f(ops.meanReached)}층·최고 ${ops.maxReached}층, 200층 승리 ${ops.cleared200}/10회다. 목표 달성 여부는 표본에서 관측된 결과이며 보장 확률을 뜻하지 않는다.

성장 진단의 200층 관측: **${observed}**. 원래 게임 수치의 기준과 가상 성장 변경 조건을 구분한다. 목표는 최고 도달층 200이며, 200층 전투 진입과 승리를 둘 다 보고했다.

권고 순서는 (1) 현재 자동 운영에서 사용하지 않는 승급 인장·비약·넥타르와 코드의 실제 스탯 효율을 반영하고, (2) 보스 진입 마나·식량 및 전열 유지 여부를 확인한 다음, (3) 목표층의 아군 EXP/레벨과 적 추가 전투 예산을 함께 조정하는 것이다. 성장 배율의 실제 채택이나 다른 로스터 전체에 대한 일괄 적용은 이번 비교만으로 확정하지 않는다.

## 자연 진행: 현재 수치와 성장 진단

각 조건 10회, 시작 시드 6101601~6101610, 관찰 상한 200층으로 전멸 또는 상한까지 진행했다. 레벨·코드·자원은 실제 신규 런의 EXP·보상·상점·휴식 API로 얻었다. 아래 성장 배율은 전용 실험 프로세스의 DTO에만 적용했으며 **실제 게임 YAML에는 반영하지 않았다**.

${table(['조건','시 공격 성장','5인 체력 성장','평균층 ± SD','최소~최고층','200층 진입/승리','평균 누적 피해'],dd.profiles.map(x=>{const p=plan.teams.find(t=>t.key===x.team);return [x.name,p.shiAttackGrowthScale+'배',p.partyHpGrowthScale+'배',f(x.meanReached)+' ± '+f(x.sdReached),x.minReached+'~'+x.maxReached,x.entered200+'/10 · '+x.cleared200+'/10',f(x.meanDamage,0)]}))}

시는 공격 성장 8.5/Lv를 12.75 또는 17로 비교했다. 체력 성장은 세이95·시88·히미코82·찬드라160·오르페우스140/Lv에 해당 배율을 곱했다. 기초 스탯·방어·특방·속도 성장·EXP 식·적·보상 확률·코드 성능은 원본이다. 파티 체력 증가는 생존뿐 아니라 찬드라 일반공격·소마 공격 버프·달빛 순환 및 체력 계수 보상에도 작용하므로 순수 생존 효과로 분리해서 해석하지 않는다.

이번 자료에서 상한 생존 런은 ${d.runs.filter(r=>r.capped).length}회다. 도달층은 전투에 진입한 누적 위치이고 사건·휴식도 위치에 포함된다. 누적 피해는 생존 길이와 만난 적의 영향을 받는다. 같은 시작 시드라도 선택·행동·처치 순서가 달라지면 이후 적과 보상도 달라지므로 조건 간 차이를 동일 경로의 단순 전후 효과로 해석하지 않는다.

## 부족한 부분 1: 적 피해·속도 예산의 추가 증가

EnemyCombatBudget은 유닛 YAML의 선형 성장 외에 누적 위치 16 이후 추가 배율을 적용한다. 아래는 CLI가 실제 공통 코드의 함수를 호출해 출력한 값이다.

${table(['층','일반 기본 피해','엘리트 기본 피해','중보스·보스 기본 피해','일반 속도','중보스·보스 속도'],cat.combatBudget.map(x=>[x.stage,f(x.normalDamage,2)+'배',f(x.eliteDamage,2)+'배',f(x.bossDamage,2)+'배',f(x.normalSpeed,3)+'배',f(x.bossSpeed,3)+'배']))}

이 표는 최종 피해 배율이 아니다. 일반전에는 개전 완충·첫 행동 전 보호·초기 적 수 보정이, 중보스·보스에는 장기전 압박이 추가되고 공통 방어·피해 증감도 적용된다. 그렇더라도 적의 공격 스탯과 추가 피해 배율이 함께 층수에 비례해 커져, 비슷한 레벨 비율에서는 피해가 대략 층수의 제곱으로 증가한다. 아군 체력은 선형 성장이고 기본 속도는 성장하지 않아 고층의 성장 배율·행동 속도 차이도 함께 커진다.

따라서 캐릭터 기본 성장만으로 200층을 맞추기 전에 이 전투 예산을 목표층에 맞춰 재검토할 필요가 있다. 이번 실험에서는 적 예산 수치를 낮추지 않았다. 구체적인 하향값은 별도 실측 후 결정해야 한다. 소스는 Assets/Scripts/Combat/EnemyCombatBudget.cs다.

## 부족한 부분 2: 아군 레벨 격차와 전열 유지

현재 기준의 실패 시 평균 아군 레벨/층수 비율은 ${f(base.meanLevelRatio*100)}%다. 아군 EXP 필요량은 100+28×(레벨−1)이고, 적 레벨은 누적 위치다. 성장 배율과 EXP 공급을 함께 봐야 하며 시작 레벨을 강제로 맞춘 고정 전투가 실제 런의 레벨 획득을 보장하지 않는다.

${table(['조건','실패 시 평균 아군 레벨','평균 레벨/층수','첫 아군 사망'],[...dd.profiles,ops].map(x=>[x.name,f(x.meanTerminalLevel),f(x.meanLevelRatio*100)+'%',x.firstDeaths.map(([id,n])=>unitName(id)+' '+n+'회').join(', ')]))}

오르페우스가 먼저 쓰러지면 강인도 생성·격파 연계가 끊겨 시의 처치 연계와 격파 지원 코드도 영향을 받는다. 파티 구성을 바꾸는 대신 전열의 초월기 준비·보호막·제어 대응과 EXP 곡선을 먼저 함께 확인해야 한다.

## 부족한 부분 3: 전투 진입 마나와 코드 운영

현재 기준의 최종 패배 ${baselineLosses.length}회 중 보호막 부여량 0은 ${zeroShield}회, 세이 입장 마나가120 미만인 것은 ${manaShort}회다. 보호막 0이 모두 같은 원인이라는 뜻은 아니며 입장 자원·시전 전 사망·제어·중단을 구분해야 한다.

추가 운영은 보스·엘리트 준비에서 세이 마나가 부족하면 실제 긴 휴식을 시도하고, 찬드라 서풍(634) 선택 점수+50·신에게 바치는 공물(823)+30을 적용했다. 시의 재행동은 Special이므로 연환격(724)의 기존 +22 선호를 상쇄했다. 코드 지급·가격·구매 횟수·식량·준비당 휴식 한도는 우회하지 않았다. 세 변경을 묶은 운영 대조이므로 각각의 효과를 분해한 결과는 아니다.

긴 휴식 성공 ${restRows.filter(x=>x.success).length}회, 실패 ${restRows.filter(x=>!x.success).length}회, 이 추가 시도에서 소비한 식량 합계 ${restRows.reduce((s,x)=>s+Math.max(0,x.foodBefore-x.foodAfter),0)}다. 실패는 자원·현재 준비 상태 제한을 포함한다. 무료 고정보스 전 휴식과는 별개인 추가 운영 시도의 기록이다.

${table(['코드·대상','현재 기준 활성 런','운영 대조 활성 런'],[[60,634],[60,823],[2,724],[1,761],[2,761]].map(([u,id])=>[unitName(u)+' · '+cat.rewards.find(c=>c.id===id).verbalName+' ('+id+')',codeCount(d,base.team,u,id)+'/10',codeCount(op,ops.team,u,id)+'/10']))}

서풍은 보유자가 실제로 치유한 대상의 제어를 정화한다. 공물은 처치·강인도 격파 때 살아 있는 아군을 보유자 최대 체력의4%만큼 치유한다. 찬드라가 두 코드를 함께 쓰면 치유를 통한 정화 경로가 생기지만, 행동·격파가 발생하지 않는 상황에서는 그 자체로 회복을 시작할 수 없다. 연환격·정밀·등대 등 실제 추가행동 전용 코드를 시의 Special 재행동에 적용되는 화력 코드로 취급하지 않는다.

## 분리 전투 대조: 코드와 자원

실제 종료 조건에서 추출한 같은 파티·레벨·활성 코드·충전량·넥타르·영구 중첩·배치·체력/자원을 입력하고, 각 변경 조건을 새 전투 시드20개로 비교했다. 기존 캠페인의 난수 소비 전체를 재현한 리플레이가 아니며 코드 강제 교환과 레벨 일치는 획득 비용을 검증하지 않는 가상 조건이다. 자연 진행 결과와 합산하지 않는다. 중보스는 Elite가 아니라 실제 stageKind=MidBoss로 입력한다.

${table(['적 조건','변경','승/20','평균 AV 주기'],lab.map(x=>[x.fixture,x.team,x.wins+'/20',f(x.meanCycles,3)]))}

## 고층 코드 배분과 자동 운영의 범위

현재 기준 시드6101605의126층 진입 시 시는93레벨·공격력922였고, 전사의 투지(501, 공격력+12)를 계속 활성화했다. 원래 선형 공격력910에 +12는 약1.3%다. 같은 런에서 영웅의 자질(906, 주요5스탯+12%)은89층에 히미코에게 지급됐다. 현행 선택 점수는 코드 ID·역할·티어 중심이며 현재 스탯에 따른 한계 효율을 계산하지 않는다.

다만 높은 티어를 시에게 이동시키면 항상 좋아진다는 뜻은 아니다. 같은126층 조건에서501과906을 교환해도 마나가 부족한 원본 상태에서는20회 모두 패배했다. 별도200층 일반전에서는 마나 최대 원본7/20승에 비해906 교환은3/20승이었다. 히미코의 체력·특공을 잃는 비용도 함께 생긴다. 단일 캐릭터의 공격력 증가만으로 코드 배분을 판단하면 안 된다.

${table(['126층 불 공허룡 조건','승/20','평균 AV 주기'],high.map(x=>[x.team,x.wins+'/20',f(x.meanCycles,3)]))}

이126층 입력의 보관함에는 청동·은·금·별빛 승급 인장과 신속의 비약, 속도·치명타 피해 넥타르가 남아 있었다. 기본 자동 운영과 이번 운영 대조는 **인장·비약·넥타르 사용을 자동화하지 않는다**. 인장이 있어도501→601 같은 승급을 수행하지 않고, 리롤도 사용하지 않는다. 따라서 이번 수치는 해당 자동 운영의 성능이며 최적 플레이에서 가능한 최고층의 증명은 아니다. 캐릭터를 일괄 강화하기 전에 이 합법적인 성장·코드 도구 사용과 실제 스탯 효율 평가부터 보완할 필요가 있다.

## 200레벨 적에 대한 별도 부하 대조

126층 최고 기록의 실제 코드·충전량·넥타르·중첩을 유지하고, 적만200레벨로 설정했다. 일반전은 네크로맨서4기를 명시적으로2전열·2후열에 놓은 가상 조건이며 실제200층의 테마·추첨·연속 전투를 재현하지 않는다.140/200레벨 아군은 강제로 지정한 가상 대조로, 자연 EXP나 획득 경로를 검증한 결과가 아니다.

${table(['200층 가정 적','변경','승/20','평균 AV 주기'],target.map(x=>[x.encounter,x.team,x.wins+'/20',f(x.meanCycles,3)]))}

이 분리 대조와 자연 진행의 성장 실험을 함께 보되, 단일 전투 승리를200층까지의 생존으로 바꾸어 집계하지 않는다.

## 성장·운영 통합 추적 2회

시드6101604(체력50% 조건에서170층 도달)와6101605(현재 기준 최고126층)를 골라, 시 공격·5인 체력 성장50%와 운영 대조의 마나 준비·지원 코드 선호를 함께 적용했다. 선택한2개 시드의 추적 확인이며 무작위10회 비교와 같은 표본으로 취급하지 않는다. 기초값·적·EXP·코드 성능은 그대로이고, 실제 게임 YAML은 변경하지 않았다.

${table(['시드','진입층','승리한 최고 위치','200층 진입','200층 승리','평균 아군 레벨(종료 전)'],combined.runs.map(r=>{const failure=cd.failures.find(x=>x.run===r.id);return [r.seed,r.reached,r.cleared,r.reached>=200?'예':'아니오',r.cleared>=200?'예':'아니오',failure?f(failure.meanLevel):'관찰 상한 생존']}))}

시드별 조건·원자료·실행 검증은 combined/에 따로 보존했다. 이2회를 기존 각10회 통계의 평균에 섞지 않았다.

## 발견·수정한 운영 순서 문제와 재검증

위 운영 대조와 최초 통합 추적은 상점 구매 전에 자원용 긴 휴식을 한 번 시도하는 정책이었다. 통합 시드6101605의110층에서 실제로 식량10이라 휴식이 실패했고, 이후 상점에서7700골드를 지불해 야영 보급품40을 구매했다. 식량50과금화13520이 남았지만, 휴식을 다시 시도하지 않아 세이마나45·히미코0·찬드라0으로 전투에 들어갔다. 원본 게임의 식량 부족만으로 설명할 수 없는 자동 운영의 순서 누락이었다.

이를 수정해 어려운 전투에서 마나가 필요하면 구매 전 짧은 휴식으로 준비당 사용 기회를 쓰지 않고, 상점 뒤 긴 휴식을 다시 시도하게 했다. 여전히 식량·준비당1회·실제 게임API를 지키고, 긴 휴식이 불가능하면 기존 체력 회복 수단을 적용한다. 같은 성장50%·코드 선호·시드4/5로 다시 실행했다. 이 수정은 studyResourceRest 실험 정책에만 적용하고 기본 게임 규칙과 데이터는 바꾸지 않았다.

${table(['시드','수정 전 진입층','구매 후 휴식 수정본 진입층','수정본 승리 위치','수정본200 진입/승리'],fixed.runs.map(r=>{const before=combined.runs.find(x=>x.seed===r.seed);return [r.seed,before.reached,r.reached,r.cleared,(r.reached>=200?'예':'아니오')+' / '+(r.cleared>=200?'예':'아니오')]}))}

각2개 시드의 추적이며10회 성적에 합산하지 않는다. 수정 전 operations/와 combined/ 결과는 해당 정책의 결과로 보존하고, 현행 수정본은 preparation-fix/에 따로 보존했다. 구매 후 성공 기록은 resource_rests.jsonl의 phase=after_shop으로 확인할 수 있다.

## 현재 기준 10회의 실패 상세

${table(['시드','진입층','적','평균 아군 레벨','개시 적 잔여 HP','첫 아군 사망','세이 입장 마나','보호막 부여'],baselineLosses.sort((a,b)=>a.seed-b.seed).map(x=>[x.seed,x.stage,enemyName(x.enemies),f(x.meanLevel),f(x.enemyHpFraction*100)+'%',unitName(x.firstAlliedDeath),x.heroes.find(u=>u.id===1)?.mana,f(x.shielding,0)]))}

잔여 HP는 전투 종료로 필드가 정리되기 전 마지막 피해/사망 신호에서 수집한 개시 적의 값이다. 새 증원이 있다면 개시 적 기준과 전체 적의 잔량을 구분해야 한다.

## 검증·자료

최종 자연 진행은 각10회 비교 ${d.completed+op.completed}회와 통합 추적 ${combined.completed}회·준비 순서 수정 재검증 ${fixed.completed}회다. 누락 ${d.expected+op.expected+combined.expected+fixed.expected-d.completed-op.completed-combined.completed-fixed.completed}회, 집계 오류 ${d.errors.length+op.errors.length+combined.errors.length+fixed.errors.length}건, 무효 ${d.invalid.length+op.invalid.length+combined.invalid.length+fixed.invalid.length}회다. 각 campaign/의 실행 요약에서 실제 성공·예외를 별도로 확인했다. 독립 전투 회귀는 457/457 통과했다. 이번 변경은 QA 계측·운영 실험과 CLI 카탈로그 출력이며 게임 전투 규칙·YAML 수치를변경하지 않았다.

자연 진행은 실제 QA Player, 분리 전투는 독립 .NET 코어를 사용했다. QA 복사본의 렌더링만 단순화했으며 이전 기준과 최초4전투의 적·피해·AV·행동·회복·보호막 일치를 확인했다. 미술·화면 검증용 결과가 아니다. 조건·소스 버전·성장/코드 계획·개별 런과 전투·코드 제시/수령/활성·휴식 결과는 이 폴더 및 operations/에 보존했다. 초기 준비 실패와 잘못 분류한 중보스 입력의 결과는 pilots/로 분리했다. 감독 전환 실수로 중단한20개 런은 같은 조건으로 신규 실행했고, 중단 기록을 실패 자료에 보존했으며 정상 패배나0층으로 집계하지 않았다.
실험은 시작 시 고정한 복사본과 그룹별 source-manifest.json의 소스를 사용했다. 작업 중 원본의 무대 연출·검증 관련 파일이 별도로 변경되어 최종 원본과의 해시 차이는 final-source-audit.json에 기록했다. 해당 사용자 변경은 보존했으며, 이 결과를 변경 이후 전체 프로젝트의 재검증으로 확대하지 않는다.
`;
fs.writeFileSync(root+'/Report.md',text);console.log('200층 진단 보고서 저장');
