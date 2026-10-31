import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
const [inputArg,outputArg]=process.argv.slice(2);
const input=path.resolve(inputArg),output=path.resolve(outputArg);
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const rows=p=>fs.existsSync(p)?fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'').trim().split(/\r?\n/).filter(Boolean).map(JSON.parse):[];
const analysis=read(path.join(output,'analysis/results.json'));
if(analysis.missing||analysis.errors.length)throw Error('완료된 실험만 보존합니다.');
const executions=[];
for(const worker of fs.readdirSync(input).filter(x=>/^worker\d+$/.test(x))){
    const source=path.join(input,worker),destination=path.join(output,worker);
    const runtime=path.join(source,'.runtime-result.json');
    const runRows=rows(path.join(source,'runs.jsonl'));
    const result=fs.existsSync(runtime)?read(runtime):{success:false,status:'interrupted',message:'감독/QA 프로세스 종료. 유효하게 기록된 런만 집계하고 미완료 회차는 재실행.'};
    fs.mkdirSync(destination,{recursive:true});
    for(const file of ['runs.jsonl','battles.jsonl','rests.jsonl','theme_recoveries.jsonl','catalog.json','intent-parties.json','reward_rejections.jsonl','reward_fallbacks.jsonl']){
        if(fs.existsSync(path.join(source,file)))fs.copyFileSync(path.join(source,file),path.join(destination,file));
    }
    const eventCounts={};
    for(const field of ['visualEvents','vfxSlots','sfxEvents','portraitRects','uiRects']){eventCounts[field]=result[field]?.length??0;delete result[field]}
    result.eventCounts=eventCounts;
    fs.writeFileSync(path.join(destination,'execution.json'),JSON.stringify(result,null,2));
    const ids=new Set([...runRows.map(x=>x.id),...rows(path.join(source,'battles.jsonl')).map(x=>x.run)]);
    fs.writeFileSync(path.join(destination,'bosses.jsonl'),analysis.bosses.filter(x=>ids.has(x.run)).map(x=>JSON.stringify(x)).join('\n')+'\n');
    executions.push({worker,actualRuns:runRows.length,validRuns:runRows.filter(x=>x.valid).length,success:result.success,owner:read(path.join(source,'ownership.json'))});
}
const changed=[];
for(const item of read(path.join(output,'source-manifest.json'))){
    const hash=crypto.createHash('sha256').update(fs.readFileSync(item.path)).digest('hex').toUpperCase();
    if(hash!==item.hash)changed.push({path:item.path,before:item.hash,after:hash});
}
fs.writeFileSync(path.join(output,'execution-reconciled.json'),JSON.stringify({completedUtc:new Date().toISOString(),actualRuns:executions.reduce((s,x)=>s+x.actualRuns,0),workers:executions},null,2));
const runtimeUnchanged=changed.every(x=>x.path.endsWith('Assets\\Scripts\\Editor\\CompositionCampaignBuild.cs'));
fs.writeFileSync(path.join(output,'final-source-check.json'),JSON.stringify({checkedUtc:new Date().toISOString(),runtimeUnchanged,changes:changed},null,2));
console.log(JSON.stringify({archivedWorkers:executions.length,actualRuns:analysis.completed,sourceChanges:changed.length}));
