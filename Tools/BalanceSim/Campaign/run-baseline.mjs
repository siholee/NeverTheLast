import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
const [playerArg,scratchArg,outArg]=process.argv.slice(2);
const player=path.resolve(playerArg),scratch=path.resolve(scratchArg),out=path.resolve(outArg),workers=Number(process.argv[5]??8),stageCap=Number(process.argv[6]??340);
if(!Number.isInteger(workers)||workers<1||workers>20)throw Error('동시 실행 수 범위는 1~20');
if(!Number.isInteger(stageCap)||stageCap<1||stageCap>100000)throw Error('관찰 상한 범위는 1~100000');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));
const plan=read(path.join(out,'plan.json'));
const attempts=plan.attempts??Array.from({length:10},(_,i)=>i+1);
if(!attempts.length||new Set(attempts).size!==attempts.length||attempts.some(x=>!Number.isInteger(x)||x<1||x>10))throw Error('시도 번호는 중복 없는 1~10');
if(fs.existsSync(scratch))throw Error('기존 측정 폴더는 덮어쓰지 않습니다.');
fs.mkdirSync(scratch,{recursive:true});
const queue=plan.teams.flatMap(team=>attempts.map(attempt=>({team,attempt}))),active=new Set,finished=[];
let serial=0,last=0;
while(queue.length||active.size){
    while(queue.length&&active.size<workers){
        const job=queue.shift(),worker='worker'+String(serial++).padStart(3,'0'),dir=path.join(scratch,worker);fs.mkdirSync(dir);
        fs.writeFileSync(path.join(dir,'intent-parties.json'),JSON.stringify({teams:[job.team]}));
        for(let i=1;i<=10;i++)if(i!==job.attempt)fs.writeFileSync(path.join(dir,`${job.team.key}_${i}.done`),JSON.stringify({schedulingOnly:true,notARunResult:true}));
        fs.writeFileSync(path.join(dir,'request.json'),JSON.stringify({scenarios:[{name:'composition_campaign',trigger:'intent_study',lineup:'party5',partyLevel:10,stage:stageCap}],hideDebugBanner:true,captureWidth:640,captureHeight:360}));
        const child=spawn(player,['-batchmode','-nographics','-job-worker-count','1','--ntl-verify-silent','--ntl-verify-dir',dir,'-logFile',path.join(dir,'player.log')],{windowsHide:true,stdio:'ignore'});
        fs.writeFileSync(path.join(dir,'ownership.json'),JSON.stringify({pid:child.pid,worker,team:job.team.key,attempt:job.attempt,seed:6101600+job.attempt}));
        const entry={job,worker,child};active.add(entry);
        child.on('exit',exit=>{active.delete(entry);finished.push({worker,team:job.team.key,attempt:job.attempt,exit})});
        child.on('error',error=>{active.delete(entry);finished.push({worker,error:error.message})});
    }
    if(Date.now()-last>30000){fs.writeFileSync(path.join(scratch,'progress.json'),JSON.stringify({queued:queue.length,live:active.size,finished:finished.length,updatedUtc:new Date().toISOString()}));last=Date.now()}
    await new Promise(r=>setTimeout(r,1000));
}
fs.writeFileSync(path.join(out,'execution.json'),JSON.stringify({finishedUtc:new Date().toISOString(),workers:finished},null,2));
