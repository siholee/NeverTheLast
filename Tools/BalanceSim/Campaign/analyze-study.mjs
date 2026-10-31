import fs from 'node:fs';
import path from 'node:path';
const options=Object.fromEntries(process.argv.slice(2).reduce((a,x,i,s)=>i%2?a:[...a,[x,s[i+1]]],[]));
const input=path.resolve(options['--input']??'Logs/PartyBalance20261027');
const output=path.resolve(options['--out']??'Logs/PartyBalance20261027/analysis');
const plan=JSON.parse(fs.readFileSync(new URL('./intent-parties.json',import.meta.url),'utf8').replace(/^\uFEFF/,''));
const expected=Number(options['--runs']??10),cap=Number(options['--cap']??340);
const readLines=file=>fs.existsSync(file)?fs.readFileSync(file,'utf8').replace(/^\uFEFF/,'').trim().split(/\r?\n/).filter(Boolean).map((s,i)=>{try{return JSON.parse(s)}catch{throw Error(`${file}:${i+1} 결과 손상`)}}):[];
const workers=fs.readdirSync(input).filter(x=>/^worker\d+$/.test(x));
const rawRuns=workers.flatMap(x=>readLines(path.join(input,x,'runs.jsonl')));
const battles=workers.flatMap(x=>readLines(path.join(input,x,'battles.jsonl')));
const rests=workers.flatMap(x=>readLines(path.join(input,x,'rests.jsonl')));
const bosses=workers.flatMap(worker=>{
    const prepared=path.join(input,worker,'bosses.jsonl');if(fs.existsSync(prepared))return readLines(prepared);
    return readLines(path.join(input,worker,'checkpoint_fights.jsonl')).map(f=>{
        const key=/^checkpoint_([a-f0-9]+)_(\d+)_trace.json$/.exec(f.transcript);if(!key)throw Error('고정보스 결과 키 오류');
        return {run:key[1],stage:Number(key[2]),team:f.team,attempt:f.attempt,bossId:f.bossId,bossMaxHp:f.bossMaxHp,bossHpAtEnd:f.bossHpAtEnd,
            bossAtFirstDeath:f.bossAtFirstDeath,escortDeaths:f.escortDeaths,bossUltimates:f.bossUltimates};
    });
}).map(f=>({...f,won:battles.find(b=>b.run===f.run&&b.stage===f.stage)?.won??null,remainingFraction:f.bossMaxHp?f.bossHpAtEnd/f.bossMaxHp:null,bossFractionAtFirstAllyDeath:f.bossAtFirstDeath>=0?f.bossAtFirstDeath:null}));
const unitsText=fs.readFileSync('Assets/Resources/Data/10_units.yaml','utf8');
const names=Object.fromEntries([...unitsText.matchAll(/^  - id: (\d+)\r?\n    name: (.+)$/gm)].map(x=>[Number(x[1]),x[2].replace(/^"|"$/g,'')]));
const sum=a=>a.reduce((s,x)=>s+x,0),mean=a=>a.length?sum(a)/a.length:null;
const quantile=(a,p)=>{if(!a.length)return null;let b=[...a].sort((x,y)=>x-y),k=(b.length-1)*p;return b[Math.floor(k)]+(b[Math.ceil(k)]-b[Math.floor(k)])*(k%1)};
const ratio=(a,b)=>b?a/b:null;
const errors=[],runs=[],unitRuns=[];
for(const team of plan.teams)for(let attempt=1;attempt<=expected;attempt++){
    const candidates=rawRuns.filter(x=>x.team===team.key&&x.attempt===attempt);
    const valid=candidates.filter(x=>x.valid);
    if(valid.length>1)errors.push(`${team.key}/${attempt}: 유효한 결과가 중복됨`);
    const run=valid.at(-1);if(!run)continue;
    if(run.seed!==6101600+attempt)errors.push(`${team.key}/${attempt}: 시드 불일치`);
    const rows=battles.filter(b=>b.run===run.id);
    if(rows.length!==run.battles)errors.push(`${team.key}/${attempt}: 전투 결과 개수 불일치`);
    if(!['game_over','observation_cap'].includes(run.stop))errors.push(`${team.key}/${attempt}: 유효 런의 종료 사유 오류`);
    if(Math.max(0,...rows.map(x=>x.stage))!==run.reached)errors.push(`${team.key}/${attempt}: 최고 진입층 불일치`);
    if(Math.max(0,...rows.filter(x=>x.won).map(x=>x.stage))!==run.cleared)errors.push(`${team.key}/${attempt}: 최고 승리층 불일치`);
    const damageByUnit=new Map(team.ids.map(id=>[id,0]));
    for(const b of rows){
        if(!Number.isFinite(b.partyDamage))errors.push(`${run.id}/${b.stage}: 피해 계측 없음`);
        for(const field of ['partyDamage','damageTaken','effectiveHealing','shielding','cycles','actions','turns','survivors'])
            if(!Number.isFinite(b[field])||b[field]<0)errors.push(`${run.id}/${b.stage}: ${field} 계측 범위 오류`);
        let measured=0;
        for(const pair of b.damage.split(';').filter(Boolean)){
            const [id,damage]=pair.split(':').map(Number);measured+=damage;
            damageByUnit.set(id,(damageByUnit.get(id)??0)+damage);
        }
        if(measured!==b.partyDamage)errors.push(`${run.id}/${b.stage}: DamageMeter 총합 불일치`);
    }
    const totalDamage=sum(rows.map(x=>x.partyDamage)),cycles=sum(rows.map(x=>x.cycles));
    const last=rows.at(-1);
    const opening=rows.filter(x=>x.stage<=16),mid=rows.filter(x=>x.stage>=17&&x.stage<=33);
    const result={...run,name:team.name,openingDamage:sum(opening.map(x=>x.partyDamage)),openingCycles:sum(opening.map(x=>x.cycles)),midDamage:sum(mid.map(x=>x.partyDamage)),midCycles:sum(mid.map(x=>x.cycles)),damage:totalDamage,damageTaken:sum(rows.map(x=>x.damageTaken)),
        healing:sum(rows.map(x=>x.effectiveHealing)),shielding:sum(rows.map(x=>x.shielding)),
        cycles,turns:sum(rows.map(x=>x.turns)),actions:sum(rows.map(x=>x.actions)),
        wins:rows.filter(x=>x.won).length,losses:rows.filter(x=>!x.won).length,
        meanDamagePerBattle:ratio(totalDamage,rows.length),damagePerCycle:ratio(totalDamage,cycles),
        meanSurvivors:mean(rows.map(x=>x.survivors)),survivorSum:sum(rows.map(x=>x.survivors)),lastEnemy:last?.enemies??'',lastCategory:last?.category??'',
        terminalLevel:last?.meanLevel??null,paidRests:rests.filter(x=>x.team===team.key&&x.attempt===attempt&&!x.free).length,
        freeRests:rests.filter(x=>x.team===team.key&&x.attempt===attempt&&x.free).length,
        invalidAttempts:candidates.filter(x=>!x.valid).length};
    runs.push(result);
    for(const [id,damage]of damageByUnit)unitRuns.push({team:team.key,name:team.name,attempt,seed:run.seed,id,unit:names[id]??String(id),damage,share:ratio(damage,totalDamage)});
}
const summaries=plan.teams.map(team=>{
    const rr=runs.filter(x=>x.team===team.key),bb=battles.filter(x=>rr.some(r=>r.id===x.run));
    const n=rr.length,reached=rr.map(x=>x.reached),avg=mean(reached),sd=n>1?Math.sqrt(sum(reached.map(x=>(x-avg)**2))/(n-1)):null;
    const ci=n===10?2.262157163*sd/Math.sqrt(n):null;
    const damage=sum(rr.map(x=>x.damage)),cycles=sum(rr.map(x=>x.cycles));
    const opening=bb.filter(x=>x.stage<=16),mid=bb.filter(x=>x.stage>=17&&x.stage<=33);
    const ur=unitRuns.filter(x=>x.team===team.key);
    const contributors=[...new Set(ur.map(x=>x.id))].map(id=>({id,name:names[id]??String(id),damage:sum(ur.filter(x=>x.id===id).map(x=>x.damage))})).sort((a,b)=>b.damage-a.damage);
    const failed34=bosses.filter(x=>x.team===team.key&&x.stage===34&&x.won===false&&rr.some(r=>r.id===x.run));
    return {team:team.key,name:team.name,family:team.family,intent:team.intent,n,pending:expected-n,capped:rr.filter(x=>x.capped).length,meanReached:avg,
        sdReached:sd,medianReached:quantile(reached,.5),minReached:n?Math.min(...reached):null,maxReached:n?Math.max(...reached):null,
        ci95Low:ci===null?null:avg-ci,ci95High:ci===null?null:avg+ci,meanCleared:mean(rr.map(x=>x.cleared)),
        reach34:ratio(rr.filter(x=>x.reached>=34).length,n),reach68:ratio(rr.filter(x=>x.reached>=68).length,n),reach102:ratio(rr.filter(x=>x.reached>=102).length,n),
        pass34:ratio(rr.filter(x=>x.cleared>=34).length,n),pass68:ratio(rr.filter(x=>x.cleared>=68).length,n),
        pass102:ratio(rr.filter(x=>x.cleared>=102).length,n),pass136:ratio(rr.filter(x=>x.cleared>=136).length,n),
        failedBoss34Count:failed34.length,boss34RemainingOnLoss:mean(failed34.map(x=>x.remainingFraction)),
        meanRunDamage:mean(rr.map(x=>x.damage)),meanBattleDamage:ratio(damage,bb.length),damagePerCycle:ratio(damage,cycles),
        meanRunHealing:mean(rr.map(x=>x.healing)),meanRunShielding:mean(rr.map(x=>x.shielding)),meanRunTaken:mean(rr.map(x=>x.damageTaken)),
        healingPerBattle:ratio(sum(rr.map(x=>x.healing)),bb.length),shieldingPerBattle:ratio(sum(rr.map(x=>x.shielding)),bb.length),
        meanBattles:mean(rr.map(x=>x.battles)),meanCyclesPerBattle:ratio(cycles,bb.length),meanTurnsPerBattle:ratio(sum(rr.map(x=>x.turns)),bb.length),
        meanSurvivors:mean(bb.map(x=>x.survivors)),battleWinRate:ratio(bb.filter(x=>x.won).length,bb.length),
        openingDamagePerCycle:ratio(sum(opening.map(x=>x.partyDamage)),sum(opening.map(x=>x.cycles))),
        midDamagePerCycle:ratio(sum(mid.map(x=>x.partyDamage)),sum(mid.map(x=>x.cycles))),
        meanTerminalLevel:mean(rr.map(x=>x.terminalLevel)),topContributor:contributors[0]?.name??'',topContributorShare:ratio(contributors[0]?.damage??0,damage),
        front:team.ids.filter((_,i)=>team.xs[i]===-1).map(id=>names[id]).join(' · '),
        rear:team.ids.filter((_,i)=>team.xs[i]===-2).map(id=>names[id]).join(' · '),
        contributors:contributors.map(x=>({...x,meanRunDamage:ratio(x.damage,n),share:ratio(x.damage,damage)})),
        invalidAttempts:sum(rr.map(x=>x.invalidAttempts))};
}).sort((a,b)=>(b.meanReached??-1)-(a.meanReached??-1)||(b.minReached??-1)-(a.minReached??-1)||a.team.localeCompare(b.team));
for(const summary of summaries){
    const samples=runs.filter(x=>x.team===summary.team);
    for(let floor=34;floor<=cap;floor+=34){
        summary['reach'+floor]=ratio(samples.filter(x=>x.reached>=floor).length,samples.length);
        summary['pass'+floor]=ratio(samples.filter(x=>x.cleared>=floor).length,samples.length);
    }
}
const completed=runs.length,missing=plan.teams.length*expected-completed;
const includedIds=new Set(runs.map(x=>x.id));
for(const battle of battles)battle.includedInFinalSample=includedIds.has(battle.run);
for(const boss of bosses)boss.includedInFinalSample=includedIds.has(boss.run);
const prefixValidation={comparedRuns:0,comparedBattles:0,divergences:[]};
for(const priorId of new Set(battles.filter(x=>!x.includedInFinalSample).map(x=>x.run))){
    const prior=battles.filter(x=>x.run===priorId),first=prior[0];
    const completedRun=runs.find(x=>x.team===first.team&&x.attempt===first.attempt);
    if(!completedRun)continue;
    const current=battles.filter(x=>x.run===completedRun.id);
    prefixValidation.comparedRuns++;
    for(let i=0;i<prior.length;i++){
        const a=prior[i],b=current[i];
        const changed=['stage','theme','category','enemies','party','formation','won','partyDamage','damageTaken','effectiveHealing','shielding','actions','turns','survivors']
            .filter(k=>a[k]!==b?.[k]);
        prefixValidation.comparedBattles++;
        if(changed.length)prefixValidation.divergences.push({team:a.team,attempt:a.attempt,priorRun:priorId,currentRun:completedRun.id,index:i,stage:a.stage,changed});
    }
}
const result={generatedUtc:new Date().toISOString(),expectedRuns:plan.teams.length*expected,completed,missing,cap,errors,
    invalidRuns:rawRuns.filter(x=>!x.valid),summaries,runs,unitRuns,bosses,teams:plan.teams.map(x=>({...x,members:x.ids.map((id,i)=>({id,name:names[id],column:-x.xs[i],row:x.ys[i]}))}))};
fs.mkdirSync(output,{recursive:true});fs.writeFileSync(path.join(output,'results.json'),JSON.stringify(result,null,2));
const csv=(name,rows,columns)=>{
    const escape=v=>'"'+String(v??'').replaceAll('"','""')+'"';
    fs.writeFileSync(path.join(output,name),'\uFEFF'+[columns.map(escape).join(','),...rows.map(x=>columns.map(k=>escape(x[k])).join(','))].join('\r\n'));
};
csv('party-summary.csv',summaries,Object.keys(summaries[0]).filter(k=>k!=='contributors'));
csv('runs.csv',runs,Object.keys(runs[0]??{}));
csv('unit-contributions.csv',unitRuns,['team','name','attempt','seed','id','unit','damage','share']);
csv('battles.csv',battles,['includedInFinalSample','run','team','attempt','seed','stage','theme','category','won','survivors','meanLevel','actions','turns','cycles','partyDamage','damageTaken','effectiveHealing','shielding','gold','food','enemies','damage','formation','party','reason']);
csv('boss-battles.csv',bosses,['includedInFinalSample','run','team','attempt','stage','bossId','won','bossMaxHp','bossHpAtEnd','remainingFraction','bossFractionAtFirstAllyDeath','escortDeaths','bossUltimates']);
fs.writeFileSync(path.join(output,'boss-battles.jsonl'),bosses.map(x=>JSON.stringify(x)).join('\n')+'\n');
fs.writeFileSync(path.join(output,'validation.json'),JSON.stringify({complete:missing===0&&errors.length===0,completed,missing,errors,invalid:result.invalidRuns.length},null,2));
fs.writeFileSync(path.join(output,'recovery-prefix-validation.json'),JSON.stringify(prefixValidation,null,2));
console.log(JSON.stringify({completed,missing,errors,top:summaries.slice(0,5).map(x=>({name:x.name,n:x.n,mean:x.meanReached,max:x.maxReached}))},null,2));
if(errors.length)process.exitCode=2;
