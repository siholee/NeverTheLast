import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(process.argv[2] ?? 'Logs/Shi20020261030');
const read = p => JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
const catalog = read(path.join(root, 'code-catalog.json'));
const codes = new Map(catalog.map(x => [x.id, x]));
const hist = entries => Object.fromEntries([1,2,3,4,5].map(t => [t, entries.filter(x => x.tier === t).length]));
const share = entries => ({count:entries.length,tiers:hist(entries),highTierPercent:entries.length ? 100*entries.filter(x=>x.tier>=4).length/entries.length : null});
const lines = p => fs.existsSync(p) ? fs.readFileSync(p,'utf8').trim().split(/\r?\n/).filter(Boolean).map(JSON.parse) : [];
function walk(p) {
    return fs.readdirSync(p,{withFileTypes:true}).filter(x=>x.isDirectory()).flatMap(x=>{
        const q=path.join(p,x.name);
        return fs.existsSync(path.join(q,'ownership.json')) ? [q] : walk(q);
    });
}
const groups = [];
for (const suffix of ['', 'operations', 'combined', 'preparation-fix']) {
    const dir=path.join(root,suffix), result=read(path.join(dir,'analysis/results.json'));
    const folders=walk(path.join(dir,'campaign'));
    const all=name=>folders.flatMap(p=>lines(path.join(p,name+'.jsonl')));
    const offers=all('reward_offers'), choices=all('reward_choices'), loadouts=all('loadouts');
    for(const team of new Set(result.runs.map(x=>x.team))) {
        const runs=result.runs.filter(x=>x.team===team), valid=new Set(runs.map(x=>x.id));
        const oo=offers.filter(x=>valid.has(x.run)&&x.code>0), cc=choices.filter(x=>valid.has(x.run)&&x.code>0);
        const terminal=runs.flatMap(r=>loadouts.filter(x=>x.run===r.id&&x.stage===r.reached));
        if(terminal.length!==runs.length*5)throw Error(team+': terminal loadout missing/duplicate');
        const active=terminal.flatMap(x=>x.active.split(';').map(Number).filter(Boolean).map(id=>({unit:x.unit,...codes.get(id)})));
        const bands=[[1,34],[35,68],[69,100],[101,136],[137,200]].map(([lo,hi])=>{
            const pick=rows=>rows.filter(x=>x.stage>=lo&&x.stage<=hi);
            return {lo,hi,runs:runs.filter(x=>x.reached>=lo).length,rewardOffers:share(pick(oo).filter(x=>x.source!=='shop')),rewardChoices:share(pick(cc).filter(x=>x.source!=='shop')),shopChoices:share(pick(cc).filter(x=>x.source==='shop'))};
        });
        const byCode=catalog.map(c=>({...c,offered:oo.filter(x=>x.code===c.id&&x.source!=='shop').length,chosen:cc.filter(x=>x.code===c.id).length,terminalActive:active.filter(x=>x.id===c.id).length}));
        const highShop=oo.filter(x=>x.source==='shop'&&x.tier>=4);
        groups.push({group:suffix||'primary',team,runs:runs.length,rewardOffers:share(oo.filter(x=>x.source!=='shop')),rewardChoices:share(cc.filter(x=>x.source!=='shop')),shopChoices:share(cc.filter(x=>x.source==='shop')),highTierShop:{repeatedOfferRows:highShop.length,affordableRows:highShop.filter(x=>x.affordable).length,claimableAndAffordableRows:highShop.filter(x=>x.affordable&&x.claimable).length},terminalActive:share(active),terminalByUnit:[...new Set(terminal.map(x=>x.unit))].map(unit=>({unit,...share(active.filter(x=>x.unit===unit))})),bands,uniqueRewardCodesByTier:hist(catalog.filter(c=>oo.some(x=>x.code===c.id&&x.source!=='shop'))),byCode});
    }
}
const result={generatedUtc:new Date().toISOString(),catalog:share(catalog),groups,notes:['Shop offers are a repeated fixed catalog and are excluded from random-offer percentages.','Stage bands pool offers and are weighted by survival. Growth experiments are separate conditions.','Terminal active codes contain five units per completed run; 35 is the party slot capacity.','Catalog counts are all registered reward codes, not the party-eligible pool.','No simulation or balance changes were made by this analysis.']};
fs.writeFileSync(path.join(root,'code-tier-analysis.json'),JSON.stringify(result,null,2));
console.log(JSON.stringify({catalog:result.catalog,groups:groups.map(g=>({team:g.team,runs:g.runs,highTierShop:g.highTierShop,terminalActive:g.terminalActive}))},null,2));
