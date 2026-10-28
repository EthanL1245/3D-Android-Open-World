import {readFileSync} from 'node:fs';
import assert from 'node:assert/strict';
const root='Assets/Resources/FishingTuning/';
const rows=readFileSync(root+'BiomeBaitSpeciesChance.csv','utf8').trim().split(/\r?\n/);
const header=rows.find(x=>x.startsWith('biomeId,')).split(',');
const y=header.findIndex(x=>x.startsWith('16_'));
const m=header.findIndex(x=>x.startsWith('30_'));
assert.equal(m,header.length-2);
assert.ok(header[header.length-1].startsWith('31_picasso_triggerfish'));
let total=0;
for(const row of rows){
 if(row.startsWith('#')||row.startsWith('biomeId,'))continue;
 const c=row.split(',');
 assert.equal(c.length,header.length);
 assert.ok(c.slice(2).every(x=>Number.isInteger(+x)&&+x>=0),row);
 assert.equal(c.slice(2).reduce((s,x)=>s+ +x,0),100,row);
 if(c[0]==='snapper-island'){assert.ok(+c[m]>0);assert.equal(c[y],c[m]);}
 else assert.equal(+c[m],0);
 total++;
}
const stats=readFileSync(root+'FishStats.csv','utf8');
const ranges=readFileSync(root+'BiomeFishWeights.csv','utf8').split(/\r?\n/).filter(x=>x.includes(',30,Mahogany Snapper,'));
assert.ok(stats.includes('30,Mahogany Snapper,'));
assert.equal(ranges.length,5);
assert.equal(ranges.filter(x=>x.startsWith('snapper-island,')).length,1);
console.log('PASS Mahogany Snapper rule checks:',total,'complete chance rows');
