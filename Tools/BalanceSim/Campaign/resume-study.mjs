import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
const [inputArg,outArg,playerArg]=process.argv.slice(2),root=path.resolve(inputArg),out=path.resolve(outArg),player=path.resolve(playerArg);
const maxWorkers=Number(process.argv[5]??8);
if(!Number.isInteger(maxWorkers)||maxWorkers<1||maxWorkers>40)throw Error('동시 실행 수는 1~40입니다.');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const data=read(path.join(root,'../live/results.json'));
if(data.errors.length)throw Error('기존 집계 오류');
const existing=fs.readdirSync(root).filter(x=>/^worker\d+$/.test(x));
const previouslyPlanned=fs.readdirSync(out).filter(x=>/^interruption-recovery.*\.json$/.test(x)||x==='shard-allocation.json')
    .flatMap(x=>{const m=read(path.join(out,x));return (m.restartedTrials??m.allocations??[]).map(y=>y.worker)});
let serial=Math.max(...[...existing,...previouslyPlanned].map(x=>Number(x.slice(6))))+1;
const jobs=[];
for(const team of data.teams)for(let attempt=1;attempt<=10;attempt++)if(!data.runs.some(x=>x.team===team.key&&x.attempt===attempt))jobs.push({team,attempt,worker:'worker'+String(serial++).padStart(3,'0')});
const active=new Set,finished=[],startedUtc=new Date().toISOString();
const suffix=fs.existsSync(path.join(out,'interruption-recovery.json'))?'-'+Date.now():'';
fs.writeFileSync(path.join(out,'interruption-recovery'+suffix+'.json'),JSON.stringify({startedUtc,reason:'이전 감독 및 QA 프로세스가 종료됨. 완료 결과 보존, 미완료 회차는 신규 런 재실행.',retainedRuns:data.completed,restartedTrials:jobs.map(x=>({worker:x.worker,team:x.team.key,attempt:x.attempt}))},null,2));
let last=0;
while(jobs.length||active.size){
    while(jobs.length&&active.size<maxWorkers){
        const job=jobs.shift(),dir=path.join(root,job.worker);fs.mkdirSync(dir);
        fs.writeFileSync(path.join(dir,'intent-parties.json'),JSON.stringify({teams:[job.team]}));
        for(let attempt=1;attempt<=10;attempt++)if(attempt!==job.attempt)fs.writeFileSync(path.join(dir,`${job.team.key}_${attempt}.done`),JSON.stringify({schedulingOnly:true,notARunResult:true}));
        fs.writeFileSync(path.join(dir,'request.json'),JSON.stringify({scenarios:[{name:'composition_campaign',trigger:'intent_study',lineup:'party5',partyLevel:10,stage:340}],hideDebugBanner:true,captureWidth:640,captureHeight:360}));
        const child=spawn(player,['-batchmode','-nographics','-job-worker-count','1','-screen-width','640','-screen-height','360','--ntl-verify-silent','--ntl-verify-dir',dir,'-logFile',path.join(dir,'player.log')],{windowsHide:true,stdio:'ignore'});
        fs.writeFileSync(path.join(dir,'ownership.json'),JSON.stringify({pid:child.pid,team:job.team.key,attempt:job.attempt,recovery:true}));
        const entry={job,child};active.add(entry);
        child.on('exit',code=>{active.delete(entry);finished.push({worker:job.worker,team:job.team.key,attempt:job.attempt,exit:code})});
        child.on('error',error=>{active.delete(entry);finished.push({worker:job.worker,error:error.message})});
    }
    if(Date.now()-last>30000){fs.writeFileSync(path.join(root,'recovery-progress.json'),JSON.stringify({queued:jobs.length,live:active.size,finished:finished.length,updatedUtc:new Date().toISOString()}));last=Date.now()}
    await new Promise(resolve=>setTimeout(resolve,1000));
}
fs.writeFileSync(path.join(out,'recovery-execution'+suffix+'.json'),JSON.stringify({startedUtc,finishedUtc:new Date().toISOString(),workers:finished},null,2));
