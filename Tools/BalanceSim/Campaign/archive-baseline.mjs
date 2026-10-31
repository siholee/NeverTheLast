import fs from 'node:fs';
import path from 'node:path';
const [inputArg,outArg]=process.argv.slice(2),input=path.resolve(inputArg),out=path.resolve(outArg);
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const result=read(path.join(out,'analysis/results.json'));
if(!result.complete)throw Error('완료된 기준 파티 측정만 보존합니다.');
const summaries=[];
function walk(folder){for(const e of fs.readdirSync(folder,{withFileTypes:true}))if(e.isDirectory()){
    const source=path.join(folder,e.name);
    if(!fs.existsSync(path.join(source,'ownership.json'))){walk(source);continue}
    const relative=path.relative(input,source),dest=path.join(out,'campaign',relative);fs.mkdirSync(dest,{recursive:true});
    for(const name of ['ownership.json','intent-parties.json','runs.jsonl','battles.jsonl','rests.jsonl','theme_recoveries.jsonl','reward_offers.jsonl','reward_choices.jsonl','loadouts.jsonl','reward_rejections.jsonl','reward_fallbacks.jsonl','fight_diagnostics.jsonl','resource_rests.jsonl'])
        if(fs.existsSync(path.join(source,name)))fs.copyFileSync(path.join(source,name),path.join(dest,name));
    const terminal=path.join(source,'last-fight-input.json');
    if(fs.existsSync(terminal)){
        const t=read(terminal),s=t.preparation;
        const conditions={run:t.run,team:t.team,seed:t.seed,stage:t.stage,gold:s.gold,heldItemIds:s.storedItemIds,
            units:s.heroUnits.map(u=>({id:u.unitId,level:u.level,hp:u.currentHP,x:u.xPos,y:u.yPos,bench:u.isBench,
                resource:u.ultimateResource,active:u.activeRewardCodeSlots,stored:u.storedRewardCodeIds,
                charges:(u.activeRewardCodeInstances??[]).filter(Boolean).map(c=>({code:c.codeId,charge:c.charge})),
                nectar:u.nectarStats,stacks:u.permanentStacks}))};
        fs.writeFileSync(path.join(dest,'terminal-conditions.json'),JSON.stringify(conditions,null,2));
    }
    const runtime=read(path.join(source,'.runtime-result.json')),eventCounts={};
    for(const name of ['visualEvents','vfxSlots','sfxEvents','portraitRects','uiRects']){eventCounts[name]=runtime[name]?.length??0;delete runtime[name]}
    fs.writeFileSync(path.join(dest,'execution.json'),JSON.stringify({...runtime,eventCounts},null,2));
    const checkpoints=path.join(source,'checkpoint_fights.jsonl');
    if(fs.existsSync(checkpoints)){
        const compact=fs.readFileSync(checkpoints,'utf8').trim().split(/\r?\n/).filter(Boolean).map(JSON.parse).map(x=>{const match=/checkpoint_([a-f0-9]+)_(\d+)_trace\.json/.exec(x.transcript);delete x.transcript;return {...x,run:match?.[1],stage:Number(match?.[2])}});
        fs.writeFileSync(path.join(dest,'bosses.jsonl'),compact.map(x=>JSON.stringify(x)).join('\n')+'\n');
    }
    summaries.push({worker:relative,success:runtime.success,exceptions:runtime.consoleExceptions??[]});
}}
walk(input);
fs.writeFileSync(path.join(out,'campaign-execution.json'),JSON.stringify(summaries,null,2));
if(summaries.some(x=>!x.success||x.exceptions.length))throw Error('실행 검증 실패 또는 예외가 있습니다. 보존 기록을 확인하십시오.');
console.log('보존 완료: '+summaries.length+'개 실행');
