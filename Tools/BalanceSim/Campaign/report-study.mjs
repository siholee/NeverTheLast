import fs from 'node:fs';
import path from 'node:path';
const root=path.resolve(process.argv[2]??'Logs/PartyBalance20261027');
const imageRoot=root.replaceAll('\\','/');
const d=JSON.parse(fs.readFileSync(path.join(root,'analysis/results.json'),'utf8'));
if(d.missing||d.errors.length)throw Error('최종 결과가 아직 없습니다.');
const f=(x,n=1)=>x==null?'—':x.toLocaleString('ko-KR',{minimumFractionDigits:n,maximumFractionDigits:n});
const pct=x=>x==null?'—':f(x*100,0)+'%';
const table=(heads,rows)=>['| '+heads.join(' | ')+' |','| '+heads.map(()=>'---').join(' | ')+' |',...rows.map(x=>'| '+x.join(' | ')+' |')].join('\n');
const totalBattles=d.runs.reduce((s,x)=>s+x.battles,0),capped=d.runs.filter(x=>x.capped).length;
const top=d.summaries[0],weak=d.summaries.at(-1);
const failedAt34=d.runs.filter(x=>x.firstLoss===34).length;
const noPass34=d.summaries.filter(x=>x.pass34===0).length;
const mostEfficient=[...d.summaries].sort((a,b)=>b.damagePerCycle-a.damagePerCycle)[0];
const prefixPath=path.join(root,'analysis/recovery-prefix-validation.json');
const prefix=fs.existsSync(prefixPath)?JSON.parse(fs.readFileSync(prefixPath,'utf8')):null;
const text=`# 의도별 파티 캠페인 측정 결과

32개 편성을 각 10회, 총 **${d.completed}회** 측정했다. 현행 42개 캐릭터를 모두 포함하며 가능한 모든 5인 조합을 검사한 것은 아니다. 총 전투 ${f(totalBattles,0)}회, 관찰 상한 340층 생존 ${capped}회다. 개별 런과 전투를 실제 게임에서 실행했으며 집계 검증 오류는 ${d.errors.length}건이다.

## 주요 결과

평균 도달층이 가장 높은 편성은 **${top.name}**으로 ${f(top.meanReached)}층, 최고 ${top.maxReached}층이다. 가장 낮은 편성인 **${weak.name}**은 평균 ${f(weak.meanReached)}층이었다. 이 수치는 공통 자동 운영 정책과 해당 10개 시드에서 관측한 결과다. 개별 캐릭터의 강약이나 모든 상성을 단정하는 순위로 사용하지 않는다.

34층에서 전멸한 런은 ${failedAt34}회이고, 10회 모두 34층을 넘지 못한 편성은 ${noPass34}개다. AV 주기당 실제 피해가 가장 높은 편성은 ${mostEfficient.name}(${f(mostEfficient.damagePerCycle,0)})이다. 층수·피해 효율·보스 통과·생존 지원 수치를 함께 비교하도록 아래 표를 구성했다.

![개별 런의 도달층 분포](${imageRoot}/analysis/reached-distribution.png)

## 도달층과 피해

도달층은 **전투에 진입한 최고 누적 위치**, 클리어층은 **승리한 최고 누적 위치**다. 누적 위치는 휴식·사건 슬롯을 포함한다. 평균±표준편차는 10회 표본 기준이다. 런 누적 피해는 생존 길이에 영향을 받으므로 전투당 피해와 AV 주기당 피해를 함께 읽는다.

${table(['편성','평균 도달층 ± SD','중앙값','최소~최고','평균 클리어층','평균 누적 피해/런','피해/전투','피해/AV 주기'],d.summaries.map(t=>[t.name,f(t.meanReached)+' ± '+f(t.sdReached),f(t.medianReached),t.minReached+'~'+t.maxReached,f(t.meanCleared),f(t.meanRunDamage,0),f(t.meanBattleDamage,0),f(t.damagePerCycle,0)]))}

## 보스 돌파

통과율은 10회 중 해당 층 이상의 전투에서 승리한 비율이다. 보스 본체 HP가 0이어도 호위 때문에 전투에서 패배할 수 있으므로 본체 피해율과 승패를 구분한다. 34층 패배 시 잔여 HP는 패배 사례만의 평균이며 보호막은 포함하지 않는다.

${table(['편성','34층 통과','68층 통과','102층 통과','136층 통과','170층 통과','204층 통과','34층 패배 수','34층 패배 시 본체 잔여 HP'],d.summaries.map(t=>[t.name,pct(t.pass34),pct(t.pass68),pct(t.pass102),pct(t.pass136),pct(t.pass170),pct(t.pass204),t.failedBoss34Count,pct(t.boss34RemainingOnLoss)]))}

![고정보스 통과 횟수](${imageRoot}/analysis/boss-clear-rates.png)

## 생존과 지원, 초반 효율

회복은 실제 유효 회복, 보호막은 부여량이다. 보호막 흡수량으로 해석하지 않는다. 받은 피해·회복·보호막은 아군 소환수 대상 사건도 포함한다. 평균 생존자는 전투 종료 순간 heroList 파티원의 생존자 수이며 패배 전투를 포함하고, 별도 summonList의 소환수는 제외한다. 초반 두 구간도 편성별 난수 소비 때문에 적과 보상이 항상 같지는 않다.

${table(['편성','유효 회복/런','보호막 부여/런','받은 피해/런','전투당 AV 주기','평균 생존자','1~16층 피해/AV 주기','17~33층 피해/AV 주기','최대 피해 기여자(비중)'],d.summaries.map(t=>[t.name,f(t.meanRunHealing,0),f(t.meanRunShielding,0),f(t.meanRunTaken,0),f(t.meanCyclesPerBattle),f(t.meanSurvivors),f(t.openingDamagePerCycle,0),f(t.midDamagePerCycle,0),t.topContributor+' ('+pct(t.topContributorShare)+')']))}

## 실제 편성과 의도

${table(['편성','전열','후열','의도'],d.summaries.map(t=>[t.name,t.front,t.rear,t.intent]))}

## 측정 조건과 한계

- 실제 레벨 1 신규 런에서 시작했다. 초기 골드 500, 식량 40, 짧은 휴식 2회이며 레벨·코드·자원을 임의 주입하지 않았다.
- 시작 시드 6101601~6101610을 모든 편성에 사용했다. 파티별 전투 난수 소비가 달라 이후 테마·적·보상까지 동일한 쌍으로 보장되지는 않는다.
- 기존 CampaignPreparation / CampaignRewardScore / CampaignManageInventory 정책으로 합법적인 구매·보상·휴식·무료 코드 이동을 수행했다. 파티별 최적 수동 운영을 뜻하지 않는다.
- 추천·대표 편성의 전후열과 순서를 고정했다. 로키·에비스는 역할에 따른 실험 후보를 추가했다. 가능한 모든 조합·배치의 최적화를 수행하지 않았다.
- 고정 50Hz, 1배속, 프레임당 최소 실시간 5ms로 실행했다. 벽시계 시간이나 실시간 DPS로 비교하지 않는다.
- 피해는 실제 HP·보호막 차감량이며 초과·무효 피해를 제외한다. 소환 피해는 소환자에게 합산한다. AV 주기당 피해 = 총 가한 피해 ÷ 누적 AV/100 합계다.
- 관찰 상한은 340층이다. 상한 생존이 있으면 평균·최고층은 관찰 상한에 제한된 하한이다. 이번 상한 생존은 ${capped}회다.
- 10회는 탐색 표본이다. CSV의 평균 신뢰구간은 표본 SD를 이용한 근사 t 구간이며, 작은 표본·비정규 분포·다중 비교 때문에 확정적인 우열 판정으로 사용하지 않는다.
- 실제 캠페인 전체 진행은 전용 Unity QA Player로 실행했다. 독립 .NET CLI의 고정 전투 반복과 구분한다. 이 측정을 위해 전투 수치나 캐릭터 YAML을 변경하지 않았다.

## 실패와 결과 검증

유효하지 않은 런 기록은 ${d.invalidRuns.length}건이며 결과 원본에 보존했다. watchdog·중단을 패배나 0층으로 바꾸지 않았다. 감독/QA 프로세스 중단 후 이미 끝난 결과는 보존하고 미완료 시드는 신규 런으로 다시 실행했다. 실패한 실행과 재실행 배정은 execution-reconciled.json 및 interruption-recovery.json에 남겼다.

${d.invalidRuns.length?table(['편성ID','시도','시드','중단층','사유'],d.invalidRuns.map(r=>[r.team,r.attempt,r.seed,r.reached,r.stop])):'기록된 검증 오류 런 없음.'}

최종 집계는 편성당 10개 유효 결과, 시드, 중복, 전투 개수, 최고 진입층·최고 승리층, 계측값 범위, 개별 캐릭터 피해와 파티 총합을 검사한다. 원본은 각 worker 폴더에 있으며 보스 계측은 bosses.jsonl로 보존했다. battles.csv와 boss-battles.csv는 중단된 런의 기록도 보존하며 includedInFinalSample=true인 행만 최종 표본에 포함한다. 실행하지 않은 렌더·오디오·실기기 성능 검사는 이 결과에 포함하지 않는다.

${prefix?`중단 전후 재실행의 공통 완료 구간 ${prefix.comparedRuns}개 런·${prefix.comparedBattles}개 전투를 추가 대조했다. 적·코드·전후열·승패·피해·회복·보호막·행동·턴·생존자 기록의 차이는 ${prefix.divergences.length}건이었다. 전체 대조 기록은 [재실행 구간 검증](analysis/recovery-prefix-validation.json)에 보존했다.`:''}

## 제공 자료

- [Excel 통합 문서](outputs/01a17ce4-1350-7001-bd69-9764024be871/PartyBalance.xlsx): 편성 비교, 시드별 도달층, 런별 결과, 실제 파티 구성, 캐릭터 기여. 원본을 참조하는 수식과 편집 가능한 차트 포함.
- [전체 편성 집계 CSV](analysis/party-summary.csv)
- [개별 320회 결과 CSV](analysis/runs.csv)
- [개별 전투 CSV](analysis/battles.csv)
- [캐릭터별 피해 기여 CSV](analysis/unit-contributions.csv)
- [보스별 상세 결과 CSV](analysis/boss-battles.csv)
- [집계 검증](analysis/validation.json), [실험 조건](conditions.json), [데이터·소스 manifest](source-manifest.json)
`;
fs.writeFileSync(path.join(root,'Report.md'),text);
const parties=path.join(root,'parties.md');
fs.writeFileSync(parties,fs.readFileSync(parties,'utf8').replace('현재 측정 진행 중이며 결과의 최종 평균·순위는 전체 집계 후 확정한다.','총 320회 측정을 완료했다. 최종 결과와 지표 정의는 [결과 보고서](Report.md)에 정리했다.'));
console.log('Report.md 저장 완료');
