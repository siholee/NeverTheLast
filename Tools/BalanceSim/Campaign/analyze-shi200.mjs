import fs from 'node:fs';
import path from 'node:path';
const [inputArg,outArg]=process.argv.slice(2),input=path.resolve(inputArg),out=path.resolve(outArg);
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const lines=p=>fs.existsSync(p)?fs.readFileSync(p,'utf8').trim().split(/\r?\n/).filter(Boolean).map(JSON.parse):[];
const d=read(path.join(out,'analysis/results.json')),folders=[];
function walk(p){for(const e of fs.readdirSync(p,{withFileTypes:true}))if(e.isDirectory()){const q=path.join(p,e.name);if(fs.existsSync(path.join(q,'ownership.json')))folders.push(q);else walk(q)}}
walk(input);
const battles=folders.flatMap(p=>lines(path.join(p,'battles.jsonl'))),diag=folders.flatMap(p=>lines(path.join(p,'fight_diagnostics.jsonl')));
const avg=a=>a.length?a.reduce((s,x)=>s+x,0)/a.length:null;
const counts=a=>Object.entries(a.reduce((s,x)=>(s[x]=(s[x]??0)+1,s),{})).sort((a,b)=>b[1]-a[1]||String(a[0]).localeCompare(String(b[0])));
const failures=d.runs.filter(r=>!r.capped).map(r=>{
    const b=battles.find(b=>b.run===r.id&&b.stage===r.reached),dd=diag.find(x=>x.run===r.id&&x.stage===r.reached);
    if(!b||!dd)throw Error('완료 런의 최종 전투 계측 누락: '+r.id);
    const heroes=dd.units.filter(x=>!x.enemy),shi=heroes.find(x=>x.id===2),enemies=dd.units.filter(x=>x.enemy);
    const k=100+6*(shi.level-1),executionRatios=enemies.map(e=>3*shi.attack*k/(k+Math.max(0,e.defense))/Math.max(1,e.hp));
    return {run:r.id,team:r.team,seed:r.seed,stage:r.reached,category:b.category,enemies:b.enemies,meanLevel:b.meanLevel,
        levelRatio:b.meanLevel/r.reached,enemyHpFraction:dd.enemyHpFraction,
        firstAlliedDeath:dd.deathOrder.split(';').map(Number).find(id=>[2,1,44,60,25].includes(id))??0,
        deathOrder:dd.deathOrder,shiActions:shi.actions,shiAttack:shi.attack,shiHp:shi.maxHp,
        shiInitialExecutionRatioMax:Math.max(...executionRatios),heroes,enemyStats:enemies,
        damage:b.partyDamage,taken:b.damageTaken,healing:b.effectiveHealing,shielding:b.shielding,party:b.party};
});
const profiles=d.summaries.map(s=>{const rr=d.runs.filter(r=>r.team===s.team),ff=failures.filter(x=>x.team===s.team);return {...s,
    entered200:rr.filter(r=>r.reached>=200).length,cleared200:rr.filter(r=>r.cleared>=200).length,
    lossCategories:counts(ff.map(x=>x.category)),lossFloors:counts(ff.map(x=>x.stage)),
    firstDeaths:counts(ff.map(x=>x.firstAlliedDeath)),meanTerminalLevel:avg(ff.map(x=>x.meanLevel)),
    meanLevelRatio:avg(ff.map(x=>x.levelRatio)),meanEnemyHpFractionAtLoss:avg(ff.map(x=>x.enemyHpFraction)),
    shiDiedBeforeAction:ff.filter(x=>x.shiActions===0).length}});
const result={complete:d.complete,completed:d.completed,expected:d.expected,errors:d.errors,profiles,failures};
fs.writeFileSync(path.join(out,'analysis/shi200.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify({complete:result.complete,completed:result.completed,profiles:profiles.map(x=>({name:x.name,n:x.n,mean:x.meanReached,max:x.maxReached,entered200:x.entered200,cleared200:x.cleared200}))},null,2));
