import fs from 'node:fs';
import path from 'node:path';
// 표시 집계용 최소 필드만 추출한다. 슬롯 간 같은 ID를 합치지 않는다.
const [yamlArg,outArg]=process.argv.slice(2);
const yaml=fs.readFileSync(yamlArg,'utf8');
const passive=/^  passive:\s*\r?\n([\s\S]*?)(?=^  normal:)/m.exec(yaml)?.[1];
if(!passive)throw Error('패시브 코드 구간을 찾지 못했습니다.');
const blockEntries=[...passive.matchAll(/^    - id: (\d+)\s*\r?\n([\s\S]*?)(?=^    - (?:id:|\{)|$(?![\s\S]))/gm)].map(([,id,body])=>({
    id:Number(id),name:/^      verbalName: (.+)$/m.exec(body)?.[1].trim().replace(/^['"]|['"]$/g,''),
    tier:Number(/^      tier: (\d+)/m.exec(body)?.[1]??0),upper:Number(/^      supersededBy: (\d+)/m.exec(body)?.[1]??0)
}));
const inlineEntries=[...passive.matchAll(/^    - \{ (.+) \}\s*$/gm)].map(([,body])=>({
    id:Number(/(?:^|, )id: (\d+)/.exec(body)?.[1]??0),
    name:/(?:^|, )verbalName: ([^,]+)/.exec(body)?.[1].trim().replace(/^['"]|['"]$/g,''),
    tier:Number(/(?:^|, )tier: (\d+)/.exec(body)?.[1]??0),upper:Number(/(?:^|, )supersededBy: (\d+)/.exec(body)?.[1]??0)
}));
const entries=[...blockEntries,...inlineEntries].filter(x=>x.id>=500&&x.id<=999&&x.tier>0).sort((a,b)=>a.id-b.id);
if(!entries.length||new Set(entries.map(x=>x.id)).size!==entries.length||entries.some(x=>!x.name))throw Error('코드 표시 카탈로그 검증 실패');
if(entries.find(x=>x.id===503)?.name!=='청동 갑주')throw Error('패시브 슬롯 구분 확인 실패');
const dest=path.resolve(outArg);fs.mkdirSync(path.dirname(dest),{recursive:true});fs.writeFileSync(dest,JSON.stringify(entries,null,2));
console.log('패시브 보상 코드 '+entries.length+'개');
