import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync,rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createTournaments } from '../runtime/tournaments.mjs';
import { tournamentIslands } from '../runtime/tournament-species.mjs';

const tid='a'.repeat(32),ht='b'.repeat(32),gt='c'.repeat(32),third='d'.repeat(32);
function setup(options={}) {
  let now=1000;
  const game=createTournaments({clock:()=>now,random:()=>0,...options});
  const room={host:'host',players:new Map()};
  function player(id,mask=31){const p={id,name:id,room,pose:{islandMask:mask,castSequence:10,world:0}};room.players.set(id,p);return p;}
  const host=player('host'),guest=player('guest');
  const create={op:'create',id:tid,ticket:ht,island:0,wager:100,balance:1000};
  const joinGuest={op:'join',id:tid,ticket:gt,balance:500};
  const start=()=>{game.action(host,create);game.action(guest,joinGuest);now+=5000;game.tick(room);};
  return {game,room,host,guest,create,joinGuest,player,start,setTime:t=>now=t,getTime:()=>now};
}
function catchFish(f,p,weight,cast=11,extra={}){
 p.pose.castSequence=cast;
 return f.game.action(p,{op:'catch',id:tid,cast,island:0,species:f.room.tournament.target.id,kg:weight,...extra});
}
test('only host creates; locked players and insufficient balances cannot wager',()=>{
 const f=setup();assert.throws(()=>f.game.action(f.guest,f.create),/Only the host/);
 f.host.pose.islandMask=1;assert.throws(()=>f.game.action(f.host,{...f.create,island:3}),/not unlocked/);
 assert.throws(()=>f.game.action(f.host,{...f.create,wager:0}),/Wager/);
 assert.throws(()=>f.game.action(f.host,{...f.create,balance:99}),/Not enough/);
 f.guest.pose.islandMask=1;f.host.pose.islandMask=31;
 const v=f.game.action(f.host,{...f.create,island:3});assert.equal(v.phase,'cancelled');assert.equal(v.entries.find(e=>e.id==='guest').status,'locked');
 assert.throws(()=>f.game.action(f.guest,f.joinGuest),/closed/);
 assert.deepEqual(f.game.receipt({ticket:ht}),{ready:true,refund:true,payout:100});
});
test('all decisions reveal a random island species, then exactly six minutes and winner takes pool',()=>{
 const f=setup();let v=f.game.action(f.host,f.create);
 assert.equal(v.phase,'inviting');assert.equal(v.species,-1);
 v=f.game.action(f.guest,f.joinGuest);assert.equal(v.phase,'countdown');assert.equal(v.startSeconds,5);
 assert(tournamentIslands[0].fish.some(x=>x.id===v.species));
 f.setTime(5999);f.game.tick(f.room);assert.equal(f.room.tournament.phase,'countdown');
 f.setTime(6000);f.game.tick(f.room);assert.equal(f.room.tournament.phase,'running');
 catchFish(f,f.host,1);catchFish(f,f.guest,2);catchFish(f,f.host,.5,12);
 f.setTime(365999);assert.equal(f.game.view(f.room).phase,'running');
 f.setTime(366000);v=f.game.view(f.room);assert.equal(v.phase,'ended');assert.equal(v.winnerId,'guest');assert.equal(v.pool,200);
 assert.equal(v.entries.find(e=>e.id==='host').points,150);assert.equal(v.biggestName,'guest');assert.equal(v.biggestKg,2);
 assert.deepEqual(f.game.receipt({ticket:gt}),{ready:true,refund:false,payout:200});
 assert.deepEqual(f.game.receipt({ticket:ht}),{ready:true,refund:false,payout:0});
 assert.throws(()=>catchFish(f,f.host,2,13),/not accepting/);
});
test('retry-safe join and catch; wrong island, species, old cast and excessive weight do not score',()=>{
 const f=setup();f.game.action(f.host,f.create);f.game.action(f.host,f.create);
 f.game.action(f.guest,f.joinGuest);f.game.action(f.guest,f.joinGuest);
 f.setTime(6000);f.game.tick(f.room);
 assert.throws(()=>catchFish(f,f.guest,1,10),/new tournament casts/);
 assert.throws(()=>catchFish(f,f.guest,1,11,{island:3}),/does not match/);
 assert.throws(()=>catchFish(f,f.guest,1,11,{species:999}),/does not match/);
 assert.throws(()=>catchFish(f,f.guest,999999,11),/Invalid tournament catch/);
 catchFish(f,f.guest,1,11);catchFish(f,f.guest,1,11);
 assert.equal(f.game.view(f.room).entries.find(e=>e.id==='guest').points,100);
 assert.equal(f.game.view(f.room).pool,200);
 assert(!JSON.stringify(f.game.view(f.room)).includes(gt));
});
test('invitation timeout, declines, and no qualifying catches refund all stakes',()=>{
 const f=setup();f.game.action(f.host,f.create);f.setTime(46000);
 assert.equal(f.game.view(f.room).phase,'cancelled');assert.equal(f.game.receipt({ticket:ht}).refund,true);
 const g=setup();g.game.action(g.host,g.create);g.game.action(g.guest,{op:'decline',id:tid});
 assert.equal(g.game.view(g.room).phase,'cancelled');assert.equal(g.game.receipt({ticket:ht}).payout,100);
 const h=setup();h.start();h.setTime(366000);assert.equal(h.game.view(h.room).winnerId,'');
 for(const ticket of [ht,gt])assert.equal(h.game.receipt({ticket}).refund,true);
});
test('ties resolve by biggest individual catch then first score; disconnected active guest can still win',()=>{
 const f=setup();f.start();catchFish(f,f.host,1);catchFish(f,f.host,1,12);catchFish(f,f.guest,2);
 f.game.leave(f.guest);f.room.players.delete('guest');f.setTime(366000);
 assert.equal(f.game.view(f.room).winnerId,'guest');assert.equal(f.game.receipt({ticket:gt}).payout,200);
 const g=setup();g.start();catchFish(g,g.host,1);g.setTime(7000);catchFish(g,g.guest,1);g.setTime(366000);
 assert.equal(g.game.view(g.room).winnerId,'host');
});
test('host disconnect cancels, lobby guest leaves refunds, and unknown tickets close before refund',()=>{
 const f=setup();f.start();f.game.leave(f.host);
 for(const ticket of [ht,gt])assert.equal(f.game.receipt({ticket}).refund,true);
 const g=setup();g.player('third');g.game.action(g.host,g.create);g.game.action(g.guest,g.joinGuest);g.game.leave(g.guest);g.room.players.delete('guest');
 assert.equal(g.game.receipt({ticket:gt}).payout,100);assert.equal(g.game.view(g.room).pool,100);
 const h=setup();h.game.action(h.host,h.create);
 assert.equal(h.game.receipt({ticket:third,cancelMissing:true}).refund,true);
 assert.throws(()=>h.game.action(h.guest,{...h.joinGuest,ticket:third}),/closed or used/);
});
test('durable escrow refunds interrupted games on restart and preserves completed payouts for recovery',t=>{
 const dir=mkdtempSync(join(tmpdir(),'fishing-tournament-'));t.after(()=>rmSync(dir,{recursive:true,force:true}));
 const ledgerPath=join(dir,'escrow.json');const f=setup({ledgerPath});f.start();
 const recovery=createTournaments({ledgerPath});
 for(const ticket of [ht,gt])assert.deepEqual(recovery.receipt({ticket}),{ready:true,refund:true,payout:100});
 assert.throws(()=>{const r=setup({ledgerPath});r.game.action(r.host,r.create);},/closed or used/);
 const path2=join(dir,'finished.json'),g=setup({ledgerPath:path2});g.start();catchFish(g,g.guest,2);g.setTime(366000);g.game.tick(g.room);
 const finished=createTournaments({ledgerPath:path2});
 for(let i=0;i<3;i++)assert.deepEqual(finished.receipt({ticket:gt}),{ready:true,refund:false,payout:200});
});

test('eight-player pool pays once, excluding a declined entry, and stays inside supported coin bounds',()=>{
 const f=setup();const players=[f.host,f.guest];for(let i=2;i<8;i++)players.push(f.player('p'+i));
 f.game.action(f.host,{...f.create,wager:1000000,balance:1000000});
 for(let i=1;i<7;i++)f.game.action(players[i],{op:'join',id:tid,ticket:(i+4).toString(16).repeat(32),balance:1000000});
 f.game.action(players[7],{op:'decline',id:tid});f.setTime(6000);f.game.tick(f.room);
 catchFish(f,f.host,1);f.setTime(366000);const view=f.game.view(f.room);
 assert.equal(view.pool,7000000);assert.equal(view.winnerId,'host');
 assert.equal(f.game.receipt({ticket:ht}).payout,7000000);
 assert.equal(f.game.view(f.room).entries.find(e=>e.id==='p7').status,'declined');
});
