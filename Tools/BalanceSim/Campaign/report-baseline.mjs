import fs from 'node:fs';
import path from 'node:path';
const root=path.resolve(process.argv[2]),read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const d=read(path.join(root,'analysis/results.json'));
if(!d.complete)throw Error('완료된 측정만 최종 보고서로 작성합니다.');
const f=(x,n=1)=>x==null?'—':x.toLocaleString('ko-KR',{minimumFractionDigits:n,maximumFractionDigits:n});
const pct=x=>f(x*100,0)+'%';
const table=(heads,rows)=>['| '+heads.join(' | ')+' |','| '+heads.map(()=>'---').join(' | ')+' |',...rows.map(r=>'| '+r.join(' | ')+' |')].join('\n');
const lab=read(path.join(root,'laboratory/analysis.json')),parity=read(path.join(root,'parity-comparison/comparison.json'));
const follow=read(path.join(root,'followup/analysis/results.json'));
if(!follow.complete)throw Error('수정된 기준 설정 10회가 아직 완료되지 않았습니다.');
const avg=a=>a.reduce((s,x)=>s+x,0)/a.length;
const names={1:'세이',2:'시',44:'히미코',60:'찬드라',25:'오르페우스'};
const lossPositions=rr=>{const counts=new Map;for(const r of rr.filter(r=>r.firstLoss>0))counts.set(r.firstLoss,(counts.get(r.firstLoss)??0)+1);return [...counts].sort((a,b)=>b[1]-a[1]||a[0]-b[0]).map(([stage,n])=>stage+'층 '+n+'회').join(', ')};
const text=`# 기준 파티 보상 코드 비교 결과

기준 파티는 **시·세이·히미코·찬드라·오르페우스**다. 기본 전열은 세이·오르페우스, 후열은 시·히미코·찬드라다. 같은 5인으로 5개 정책을 각각 10회 비교한 뒤, 코드 평가 경계를 고친 최신 기준 설정을 10회 추가 실행했다. 총 ${d.completed+follow.completed}회의 자연 진행이다. 최초 비교에서 평균 도달층이 가장 높은 설정은 **${d.summaries[0].name}**(${f(d.summaries[0].meanReached)}층, 최고 ${d.summaries[0].maxReached}층)이다.

히미코 P244 맹화 폭발은 **특수공격력 100%의 고정 피해**로 변경했다. 유효 스탯은 **속도 = 특수공격력 > 방어력**이다. 일반행동 불씨는 공격력 계수다. 보상 정책도 실제 코드의 flatStats/percentStats를 읽어 복합 방어 코드를 차순위로 분류한다.

## 최신 기준 설정: 코드 역할 평가 수정 후 10회

최초 50회 기록에서 스탯이 없는 연격·반격 코드까지 히미코 전용 스탯 평가를 거치는 문제가 확인됐다. 실제 양수 flatStats/percentStats가 있는 코드만 이 경로로 평가하고, 나머지는 기존 역할 평가로 되돌렸다. 연격(615)의 히미코 선택 점수는 18에서 2로 바뀐다. 전투 로직이나 코드 자체 성능은 이 수정으로 바뀌지 않는다. 기본 키트와의 적합성을 낮게 평가하는 것이며 빈 슬롯·다른 코드와의 조합까지 금지하는 규칙은 아니다.

${table(['구분','횟수','평균 도달층 ± SD','최소~최고층','34층 통과','평균 누적 피해','히미코 연격/반격 활성 런'],[...d.summaries.filter(x=>x.policy==='balanced').map(x=>({x,data:d,label:'최초 균형 V2'})),...follow.summaries.map(x=>({x,data:follow,label:'최신 균형 V3'}))].map(({x,data,label})=>[label,x.n,f(x.meanReached)+' ± '+f(x.sdReached),x.minReached+'~'+x.maxReached,pct(x.pass34),f(x.meanDamage,0),[615,616].map(id=>(data.codes.find(c=>c.team===x.team&&c.code===id)?.himikoActiveRuns??0)+'/10').join(' / ')]))}

최신 기준의 원자료·조건·소스 해시는 followup/에 있다. 아래 5개 정책 비교는 수정 전 V2를 동일하게 사용한 별도 묶음이며, V3 결과와 섞어 20회 평균을 만들지 않았다. 이후 기준 설정 재측정은 현재 V3 코드로 실행한다.

## 자연 진행 정책 비교: 동일 V2 50회

레벨 1 신규 런, 시작 시드 6101601~6101610, 관찰 상한 340층이다. 상점·보상·휴식·무료 코드 이동을 실제 게임 API로 실행했고 코드·레벨·자원을 임의 지급하지 않았다. 데이터는 격리 복사 프로젝트에서 만든 전용 QA Player를 사용했다.

${table(['설정','횟수','평균 도달층 ± SD','최소~최고층','34층 통과','68층 통과','평균 누적 피해','코드 보상/구매 평균'],d.summaries.map(x=>[x.name,x.n,f(x.meanReached)+' ± '+f(x.sdReached),x.minReached+'~'+x.maxReached,pct(x.pass34),pct(x.pass68),f(x.meanDamage,0),f(x.meanCodeRewards)+' / '+f(x.meanCodePurchases)]))}

도달층은 전투에 진입한 최고 누적 위치이며 사건·휴식 슬롯도 누적 위치에 포함된다. 통과율은 해당 위치 이상의 전투에서 승리한 런의 비율이다. 상한 생존은 ${d.runs.filter(x=>x.capped).length}회다. 누적 피해는 실제 HP·보호막 차감량으로 생존 길이의 영향도 받는다.

## 설정별 의도

- 균형: 기존 공통 정책에 히미코의 새 스탯 우선순위를 반영한다.
- 시 화력: 시에게 공격·치명·단일 공격 관련 지정 코드의 선택 점수 +22.
- 방호: 세이·오르페우스에게 체력·방어 계열 지정 코드의 선택 점수 +22.
- 회전: 모든 파티원의 속도 코드 선택 점수 +22.
- 히미코 전열: 균형 정책으로 전열 세이·히미코, 후열 시·찬드라·오르페우스를 비교한다.

점수 보너스는 자동 선택 정책의 실험 변수이며 게임 코드의 실제 능력치·가격·출현 확률을 바꾸지 않는다. 세부 코드 ID와 위치는 plan.json 및 도구 소스에 고정했다. 같은 시작 시드에서도 행동·선택에 따른 난수 소비가 달라 이후 적·보상까지 동일하지는 않다. 이번 10회 표본이 최적 운영을 확정하는 것은 아니다.

이전 전체 편성 측정과는 ActionScheduler·GridManager 등 소스와 파티 선택 순서에도 차이가 있다. 과거 시 하이퍼캐리의 평균 52.6층과 이번 수치를 맹화 계수만의 전후 효과로 비교하지 않는다. 소스 차이는 historical-source-differences.json에 보존했다.

## 히미코에게 실제 활성화된 코드

수령은 구매·보상 직후의 대상, 활성은 전투 준비에서 실제 활성 슬롯에 들어간 유닛을 뜻한다. 무료 이동·상위 교환·보관 때문에 두 값은 다를 수 있다. 아래 숫자는 해당 설정의 10개 런 중 히미코가 그 코드를 한 번 이상 활성화한 런 수다.

${table(['설정','특공 505','속도 506','특공 605','속도 606','방어 503','방어 603','복합 방어 510'],d.summaries.map(x=>[x.name,...[505,506,605,606,503,603,510].map(id=>(d.codes.find(c=>c.team===x.team&&c.code===id)?.himikoActiveRuns??0)+'/10')]))}

${d.summaries.map(x=>`### ${x.name}\n\n`+table(['코드','티어','히미코 활성 런','직접 수령 런','전체 파티 활성 런'],d.codes.filter(c=>c.team===x.team&&c.himikoActiveRuns>0).sort((a,b)=>b.himikoActiveRuns-a.himikoActiveRuns||b.tier-a.tier||a.code-b.code).slice(0,12).map(c=>[c.name+' ('+c.code+')',c.tier,c.himikoActiveRuns+'/10',c.himikoTakenRuns+'/10',c.activeRuns+'/10']))).join('\n\n')}

전체 코드별 상점 제시·전투 보상 제시·선택·수령·활성 횟수는 analysis/results.json의 codes에 보존했다. 반복 노출되는 고정 상점 코드의 제시 횟수를 무작위 드랍률로 해석하지 않는다. 고층까지 생존한 런은 코드 획득 기회도 많으므로 획득과 생존의 동시 관측만으로 코드의 인과 효과를 단정하지 않는다.

## 최신 기준 설정의 캐릭터별 코드 사용

아래는 V3 기준 10회에서 각 캐릭터가 전투 중 한 번 이상 활성화한 런 수가 많은 코드다. 강제 추천이나 성능 순위가 아니라 실제 운영 기록이다. 모든 코드의 수령 대상과 활성 대상은 followup/analysis/results.json의 unitCodes에서 확인할 수 있다.

${table(['캐릭터','실제 활성 코드: 활성 런/10'],[2,1,44,60,25].map(id=>[names[id],follow.unitCodes.filter(c=>c.unit===id&&c.activeRuns>0).sort((a,b)=>b.activeRuns-a.activeRuns||b.tier-a.tier||a.code-b.code).slice(0,7).map(c=>c.name+' ('+c.code+'): '+c.activeRuns+'/10').join('<br>')]))}

## 파티 피해 기여와 지원

${table(['설정','시 피해 평균','세이 피해 평균','히미코 피해 평균','찬드라 피해 평균','오르페우스 피해 평균','유효 회복 평균','보호막 부여 평균'],d.summaries.map(x=>{const rr=d.runs.filter(r=>r.team===x.team);return [x.name,...[2,1,44,60,25].map(id=>f(avg(rr.map(r=>r.contributions[id])),0)),f(x.meanHealing,0),f(x.meanShielding,0)]}))}

회복은 초과 회복을 뺀 유효량이며 보호막은 흡수량이 아닌 부여량이다. 피해·회복·보호막 사건은 아군 소환수가 있다면 그 대상 사건도 포함한다. 피해 기여가 낮은 지원 캐릭터를 그 값 하나로 평가하지 않는다.

## 전투 단위 지표와 패배 위치

${table(['묶음','평균 전투 횟수/런','전체 전투당 평균 피해','누적 AV 주기당 피해','첫 패배 위치'],[...d.summaries.map(x=>({name:x.name+' V2',rr:d.runs.filter(r=>r.team===x.team)})),{name:'최신 기준 V3',rr:follow.runs}].map(({name,rr})=>[name,f(avg(rr.map(r=>r.battles))),f(rr.reduce((s,r)=>s+r.damage,0)/rr.reduce((s,r)=>s+r.battles,0),0),f(rr.reduce((s,r)=>s+r.damage,0)/rr.reduce((s,r)=>s+r.cycles,0),0),lossPositions(rr)]))}

AV 주기당 피해는 실제 초당 피해가 아니다. 전투당 피해 역시 만난 적과 생존 구간의 영향을 받으므로 동일 적을 고정한 아래 대조 결과와 함께 해석한다.

## 고정 조건 코드 대조

이 절은 자연 획득 실험과 별개다. 전원 25레벨, 다른 4인 코드를 고정하고 히미코의 추가 코드만 바꿔 각 적 조건에서 20회씩, 총 240회를 실행했다. 공격력은 거인의 완력(601), 특공은 지혜의 샘(605), 속도는 질풍신뢰(606), 방어는 아이기스의 비호(603)다. 특공·속도 조합은 두 슬롯을 쓰므로 단일 코드와 동일 비용 비교가 아니다.

${table(['히미코 설정','적 조건','승리/20','평균 히미코 피해','평균 파티 피해','평균 AV 주기'],lab.map(x=>[x.team,x.encounter,x.wins+'/20',f(x.himikoDamage),f(x.partyDamage),f(x.cycles,3)]))}

일반전에서 파티 피해가 같은 것은 승리 시 실제 차감할 적 HP 총량이 같기 때문이다. 속도에 따른 전투 길이와 히미코 기여 변화도 함께 본다. 고정 조건의 보스전은 모두 패배했으므로 이 스탯 코드 하나만으로 보스 돌파를 보장한다고 해석하지 않는다.

## 검증과 재실행

- 독립 전투 회귀 457/457 통과. 공격력만 올린 맹화 입력 피해는 104로 유지되고, 특공 +200에서는 304로 증가했다. 3표식 소모·취약 유지도 검사했다.
- 실제 Unity Editor Play Mode와 독립 시뮬레이터의 같은 조건 ${parity.total}개 전투 중 ${parity.passed}개 일치, ${parity.failed}개 불일치다.
- 5개 설정의 4층 사전 검사를 통과했다. 기본 스탯 코드 점수는 속도 48 = 특공 48 > 방어 33 = 복합 방어 33이다. 정책 보너스는 이 값에 별도로 적용한다.
- 최종 자연 진행 누락 ${d.expected-d.completed}회, 집계 오류 ${d.errors.length}건, 무효 런 ${d.invalid.length}건이다. 무효·중단은 패배나 0층으로 치환하지 않는다.
- 사용 중인 원본 Unity 에디터를 종료하지 않고 별도 복사 프로젝트에서 빌드·검증했다. 초기 lineup 해석 실패와 복합 방어 점수 수정 전 관측은 최종 50회와 분리했다. 관련 실패·수정 요약은 phase01-bootstrap-failures.json 및 phase02-policy-pilot.json에 보존했다.

최신 기준 10회 누락 ${follow.expected-follow.completed}회, 집계 오류 ${follow.errors.length}건, 무효 런 ${follow.invalid.length}건이다. 측정 조건은 conditions.json 및 followup/conditions.json, 소스 해시는 final-source-manifest.json(V2) 및 followup/source-manifest.json(V3), 개별 결과는 각 campaign/과 analysis/results.json에 있다. 현행 코드를 고친 뒤 같은 plan.json과 시드를 사용해 다음 차수를 비교할 수 있다.
`;
fs.writeFileSync(path.join(root,'Report.md'),text);console.log('기준 파티 보고서 저장');
