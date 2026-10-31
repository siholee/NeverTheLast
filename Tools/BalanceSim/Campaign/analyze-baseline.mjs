import fs from 'node:fs';
import path from 'node:path';
const [inputArg,outArg]=process.argv.slice(2),input=path.resolve(inputArg),out=path.resolve(outArg);
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const lines=p=>fs.existsSync(p)?fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'').trim().split(/\r?\n/).filter(Boolean).map(JSON.parse):[];
const plan=read(path.join(out,'plan.json'));
const attempts=plan.attempts??Array.from({length:10},(_,i)=>i+1);
if(!attempts.length||new Set(attempts).size!==attempts.length||attempts.some(x=>!Number.isInteger(x)||x<1||x>10))throw Error('시도 번호는 중복 없는 1~10');
const folders=[];
function walk(p){for(const e of fs.readdirSync(p,{withFileTypes:true}))if(e.isDirectory()){const q=path.join(p,e.name);if(fs.existsSync(path.join(q,'ownership.json')))folders.push(q);else walk(q)}}
if(fs.existsSync(input))walk(input);
const all=name=>folders.flatMap(p=>lines(path.join(p,name+'.jsonl')).map(x=>({...x,worker:path.relative(input,p)})));
const raw=all('runs'),battles=all('battles'),offers=all('reward_offers'),choices=all('reward_choices'),loadouts=all('loadouts');
const errors=[],runs=[];
const sum=a=>a.reduce((s,x)=>s+x,0),mean=a=>a.length?sum(a)/a.length:null;
for(const team of plan.teams)for(const attempt of attempts){
    const candidates=raw.filter(x=>x.team===team.key&&x.attempt===attempt&&x.valid);
    if(candidates.length>1)errors.push(team.key+'/'+attempt+': 중복 유효 결과');
    if(!candidates.length)continue;
    const r=candidates[0],bb=battles.filter(x=>x.run===r.id);
    if(r.seed!==6101600+attempt||bb.length!==r.battles||Math.max(...bb.map(x=>x.stage))!==r.reached)errors.push(r.id+': 시드·전투 수·진입층 불일치');
    for(const b of bb){if(sum(b.damage.split(';').filter(Boolean).map(x=>Number(x.split(':')[1])))!==b.partyDamage)errors.push(r.id+'/'+b.stage+': 피해 합계 불일치')}
    const selected=choices.filter(x=>x.run===r.id),oo=offers.filter(x=>x.run===r.id);
    for(const c of selected.filter(x=>x.code>0)){
        if(!team.ids.includes(c.target))errors.push(r.id+': 편성 외 코드 수령');
        if(!oo.some(x=>x.stage===c.stage&&x.code===c.code&&(x.source==='shop')===(c.source==='shop')))errors.push(r.id+': 제시되지 않은 코드 수령');
    }
    const contributions=Object.fromEntries(team.ids.map(id=>[id,sum(bb.map(b=>Number(b.damage.split(';').find(x=>x.startsWith(id+':'))?.split(':')[1]??0)))]));
    runs.push({...r,policy:team.policy,name:team.name,contributions,damage:sum(bb.map(x=>x.partyDamage)),taken:sum(bb.map(x=>x.damageTaken)),healing:sum(bb.map(x=>x.effectiveHealing)),shielding:sum(bb.map(x=>x.shielding)),cycles:sum(bb.map(x=>x.cycles)),codeRewards:selected.filter(x=>x.code>0&&x.source!=='shop').length,codePurchases:selected.filter(x=>x.code>0&&x.source==='shop').length,shopSpend:sum(selected.filter(x=>x.source==='shop').map(x=>x.price))});
}
const valid=new Set(runs.map(x=>x.id));
const catalogFile=path.join(out,'code-catalog.json');
const catalog=fs.existsSync(catalogFile)?read(catalogFile):[];
const codeMap=new Map(catalog.map(x=>[x.id,x]));
const isUpper=(upper,lower)=>{const visited=new Set();for(let id=codeMap.get(lower)?.upper;id&&!visited.has(id);id=codeMap.get(id)?.upper){if(id===upper)return true;visited.add(id)}return false};
const codeRows=new Map();
function bucket(team,code){const key=team+'/'+code;if(!codeRows.has(key))codeRows.set(key,{team,code,shopOffers:0,rewardOffers:0,shopChoices:0,rewardChoices:0,seenRuns:new Set,takenRuns:new Set,himikoTakenRuns:new Set,activeRuns:new Set,himikoActiveRuns:new Set});return codeRows.get(key)}
for(const x of offers.filter(x=>valid.has(x.run)&&x.code>0)){const a=bucket(x.team,x.code);a[x.source==='shop'?'shopOffers':'rewardOffers']++;a.seenRuns.add(x.run)}
for(const x of choices.filter(x=>valid.has(x.run)&&x.code>0)){const a=bucket(x.team,x.code);a[x.source==='shop'?'shopChoices':'rewardChoices']++;a.takenRuns.add(x.run);if(x.target===44)a.himikoTakenRuns.add(x.run)}
for(const x of loadouts.filter(x=>valid.has(x.run))){
    const active=x.active.split(';').map(Number).filter(x=>x>0);
    if(active.length>7||new Set(active).size!==active.length)errors.push(x.run+'/'+x.stage+'/'+x.unit+': 활성 슬롯 오류');
    if(active.some(a=>active.some(b=>a!==b&&isUpper(a,b))))errors.push(x.run+'/'+x.stage+'/'+x.unit+': 상하위 동시 활성');
    if(!battles.some(b=>b.run===x.run&&b.stage===x.stage))continue;
    for(const code of active){const a=bucket(x.team,code);a.activeRuns.add(x.run);if(x.unit===44)a.himikoActiveRuns.add(x.run)}
}
const codes=[...codeRows.values()].map(x=>({...Object.fromEntries(Object.entries(x).map(([k,v])=>[k,v instanceof Set?v.size:v])),name:codeMap.get(x.code)?.name??String(x.code),tier:codeMap.get(x.code)?.tier??null}));
const unitBuckets=new Map();
const unitBucket=(team,unit,code)=>{const key=[team,unit,code].join('/');if(!unitBuckets.has(key))unitBuckets.set(key,{team,unit,code,activeRuns:new Set,takenRuns:new Set});return unitBuckets.get(key)};
for(const x of choices.filter(x=>valid.has(x.run)&&x.code>0))unitBucket(x.team,x.target,x.code).takenRuns.add(x.run);
const combatStages=new Set(battles.map(b=>b.run+'/'+b.stage));
for(const x of loadouts.filter(x=>valid.has(x.run)&&combatStages.has(x.run+'/'+x.stage)))for(const code of x.active.split(';').map(Number).filter(x=>x>0))unitBucket(x.team,x.unit,code).activeRuns.add(x.run);
const unitCodes=[...unitBuckets.values()].map(x=>({...x,activeRuns:x.activeRuns.size,takenRuns:x.takenRuns.size,name:codeMap.get(x.code)?.name??String(x.code),tier:codeMap.get(x.code)?.tier??null}));
const summaries=plan.teams.map(t=>{
    const rr=runs.filter(x=>x.team===t.key),n=rr.length,a=mean(rr.map(x=>x.reached));
    return {team:t.key,name:t.name,policy:t.policy,n,meanReached:a,minReached:n?Math.min(...rr.map(x=>x.reached)):null,maxReached:n?Math.max(...rr.map(x=>x.reached)):null,sdReached:n>1?Math.sqrt(sum(rr.map(x=>(x.reached-a)**2))/(n-1)):null,meanDamage:mean(rr.map(x=>x.damage)),meanTaken:mean(rr.map(x=>x.taken)),meanHealing:mean(rr.map(x=>x.healing)),meanShielding:mean(rr.map(x=>x.shielding)),meanCodeRewards:mean(rr.map(x=>x.codeRewards)),meanCodePurchases:mean(rr.map(x=>x.codePurchases)),meanShopSpend:mean(rr.map(x=>x.shopSpend)),pass34:n?rr.filter(x=>x.cleared>=34).length/n:null,pass68:n?rr.filter(x=>x.cleared>=68).length/n:null,capped:rr.filter(x=>x.capped).length};
}).sort((a,b)=>(b.meanReached??-1)-(a.meanReached??-1));
const result={generatedUtc:new Date().toISOString(),complete:runs.length===plan.teams.length*attempts.length&&errors.length===0,completed:runs.length,expected:plan.teams.length*attempts.length,errors,invalid:raw.filter(x=>!x.valid),summaries,runs,codes,unitCodes};
const destination=path.join(out,'analysis');fs.mkdirSync(destination,{recursive:true});
fs.writeFileSync(path.join(destination,'results.json'),JSON.stringify(result,null,2));
fs.writeFileSync(path.join(destination,'validation.json'),JSON.stringify({complete:result.complete,completed:runs.length,missing:result.expected-runs.length,errors,invalid:result.invalid.length},null,2));
console.log(JSON.stringify({completed:runs.length,expected:result.expected,errors,invalid:result.invalid.length,summaries},null,2));
if(errors.length)process.exitCode=2;
