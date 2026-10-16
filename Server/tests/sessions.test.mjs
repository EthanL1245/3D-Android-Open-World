import test from 'node:test';
import assert from 'node:assert/strict';
import { createSessionServer, PROTOCOL } from '../runtime/sessions.mjs';

const pose = (x=0,world=0) => ({x,y:2,z:1,yaw:30,world,swimming:false});
async function fixture(t,options={}) {
  const server=createSessionServer(options);
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  t.after(()=>{server.closeAllConnections();server.close();});
  return async (path,body={},token='')=>{
    const r=await fetch(`http://127.0.0.1:${server.address().port}${path}`,{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+token},body:JSON.stringify(body)});
    return {status:r.status,...await r.json()};
  };
}
const hello = {protocol:PROTOCOL,build:'1.0',name:'Tester',pose:pose()};
test('two players open/join and exchange walking poses without crossing rooms',async t=>{
  const post=await fixture(t);
  const a=await post('/v1/open',hello);assert.equal(a.ok,true);assert.equal(a.code.length,4);
  const b=await post('/v1/join',{...hello,code:a.code,name:'Guest'});assert.equal(b.players.length,2);assert.equal(b.hostId,a.id);
  const c=await post('/v1/open',hello);
  const moved=await post('/v1/state',{pose:pose(12)},b.token);assert.equal(moved.ok,true);
  const seen=await post('/v1/state',{pose:pose(4)},a.token);
  assert.equal(seen.players.find(p=>p.id===b.id).x,12);
  assert.equal(seen.players.some(p=>p.id===c.id),false);
  assert.equal(seen.players.some(p=>'token' in p),false);
  assert.equal((await post('/v1/state',{pose:pose()},'wrong')).status,401);
});
test('version mismatch, missing rooms, invalid positions, and malformed body rejected',async t=>{
  const post=await fixture(t);const a=await post('/v1/open',hello);
  assert.equal((await post('/v1/join',{...hello,code:a.code,build:'different'})).status,409);
  assert.equal((await post('/v1/join',{...hello,code:'0000'})).status,404);
  assert.equal((await post('/v1/open',{...hello,protocol:'old'})).status,409);
  assert.equal((await post('/v1/state',{pose:pose(100001)},a.token)).status,400);
  assert.equal((await post('/v1/state',{pose:{x:'0'}},a.token)).status,400);
  assert.equal((await post('/v1/state',null,a.token)).status,400);
});
test('guest leaves alone; host leaves and closes the room',async t=>{
  const post=await fixture(t);const a=await post('/v1/open',hello);
  const b=await post('/v1/join',{...hello,code:a.code});
  await post('/v1/leave',{},b.token);
  assert.equal((await post('/v1/state',{pose:pose()},a.token)).players.length,1);
  const c=await post('/v1/join',{...hello,code:a.code});
  await post('/v1/leave',{},a.token);
  assert.equal((await post('/v1/state',{pose:pose()},c.token)).status,401);
});
test('idle disconnected rooms expire and release their capacity',async t=>{
  let now=0;const post=await fixture(t,{clock:()=>now,timeout:100});
  const a=await post('/v1/open',hello);now=101;
  assert.equal((await post('/v1/state',{pose:pose()},a.token)).status,401);
  assert.equal((await post('/v1/join',{...hello,code:a.code})).status,404);
});
test('room capacity is eight; rate limiting bounds attempts',async t=>{
  let now=0;const post=await fixture(t,{clock:()=>now});const a=await post('/v1/open',hello);
  for(let i=0;i<7;i++){now+=1100;assert.equal((await post('/v1/join',{...hello,code:a.code})).ok,true);}
  now+=1100;assert.equal((await post('/v1/join',{...hello,code:a.code})).status,409);
  await post('/v1/join',{...hello,code:'0000'});await post('/v1/join',{...hello,code:'0000'});await post('/v1/join',{...hello,code:'0000'});
  assert.equal((await post('/v1/join',{...hello,code:'0000'})).status,429);
});
test('oversized and non-JSON requests cannot crash the server',async t=>{
  const server=createSessionServer();await new Promise(r=>server.listen(0,'127.0.0.1',r));
  t.after(()=>{server.closeAllConnections();server.close();});
  const url=`http://127.0.0.1:${server.address().port}`;
  assert.equal((await fetch(url+'/v1/open',{method:'POST',body:'hello'})).status,415);
  assert.equal((await fetch(url+'/v1/open',{method:'POST',headers:{'Content-Type':'application/json'},body:'{bad'})).status,400);
  try {
    const oversized=await fetch(url+'/v1/state',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({padding:'x'.repeat(5000)})});
    assert.equal(oversized.status,413);
  } catch(e) { assert.equal(e.cause?.code,'UND_ERR_SOCKET'); }
  assert.equal((await fetch(url+'/health')).status,200);
});

test('observer receives equipped rod and timed cast, then unequip; old clients default safely',async t=>{
  const post=await fixture(t);
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:host.code});
  const fishing={...pose(3),rodEquipped:true,rodTier:2,castSequence:1,castAge:.2,castDuration:.72};
  await post('/v1/state',{pose:fishing},guest.token);
  const seen=(await post('/v1/state',{pose:pose()},host.token)).players;
  const observed=seen.find(p=>p.id===guest.id);
  for(const key of ['rodEquipped','rodTier','castSequence','castAge','castDuration']) assert.equal(observed[key],fishing[key]);
  const old=seen.find(p=>p.id===host.id);
  assert.equal(old.rodEquipped,false);assert.equal(old.castAge,-1);assert.equal(old.castSequence,0);
  await post('/v1/state',{pose:{...fishing,rodEquipped:false,castAge:-1}},guest.token);
  const unequipped=(await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id);
  assert.equal(unequipped.rodEquipped,false);assert.equal(unequipped.castAge,-1);
  assert.equal(unequipped.castSequence,1);
  for(const change of [{rodTier:9},{castSequence:-1},{castSequence:1.5},{castAge:'0'},{castAge:61},{castDuration:0}])
    assert.equal((await post('/v1/state',{pose:{...fishing,...change}},guest.token)).status,400);
});

test('reeling, fish pulling, and paused lure poses preserve the sender animation clock',async t=>{
  const post=await fixture(t);
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:host.code});
  const base={...pose(),rodEquipped:true,castSequence:1,castAge:-1};
  async function exchange(state) {
    assert.equal((await post('/v1/state',{pose:{...base,...state}},guest.token)).ok,true);
    return (await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id);
  }
  for(const state of [
    {hasFish:true,lureInWater:false,reeling:true,reelTime:.35},
    {hasFish:true,lureInWater:false,reeling:false,reelTime:.35},
    {hasFish:false,lureInWater:true,reeling:true,reelTime:.55},
    {hasFish:false,lureInWater:true,reeling:false,reelTime:.55},
    {hasFish:false,lureInWater:true,reeling:false,reelTime:.55},
    {hasFish:false,lureInWater:true,reeling:true,reelTime:.7},
    {hasFish:false,lureInWater:false,reeling:false,reelTime:.7}
  ]) {
    const observed=await exchange(state);
    for(const key of Object.keys(state)) assert.equal(observed[key],state[key]);
  }
  const old=(await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===host.id);
  assert.equal(old.reelTime,0);assert.equal(old.hasFish,false);assert.equal(old.lureInWater,false);assert.equal(old.reeling,false);
  for(const reelTime of [-1,'1',10000001])
    assert.equal((await post('/v1/state',{pose:{...base,reelTime}},guest.token)).status,400);
});

test('observer receives moving fishing state and held fish identity; clearing removes the catch',async t=>{
  const post=await fixture(t);
  const host=await post('/v1/open',hello);
  const guest=await post('/v1/join',{...hello,code:host.code});
  async function exchange(fields) {
    assert.equal((await post('/v1/state',{pose:{...pose(),...fields}},guest.token)).ok,true);
    return (await post('/v1/state',{pose:pose()},host.token)).players.find(p=>p.id===guest.id);
  }
  const walking=await exchange({x:4,rodEquipped:true,reeling:true,lureInWater:true});
  assert.equal(walking.x,4);assert.equal(walking.reeling,true);
  const fighting=await exchange({x:6,rodEquipped:true,reeling:false,hasFish:true});
  assert.equal(fighting.x,6);assert.equal(fighting.hasFish,true);
  const held=await exchange({heldFishSpecies:21,heldFishWeight:2.75,rodEquipped:false});
  assert.equal(held.heldFishSpecies,21);assert.equal(held.heldFishWeight,2.75);assert.equal(held.rodEquipped,false);
  const switched=await exchange({heldFishSpecies:5,heldFishWeight:15});
  assert.equal(switched.heldFishSpecies,5);assert.equal(switched.heldFishWeight,15);
  const cleared=await exchange({rodEquipped:true,heldFishSpecies:-1,heldFishWeight:0});
  assert.equal(cleared.heldFishSpecies,-1);
  const legacy=await exchange({});assert.equal(legacy.heldFishSpecies,-1);assert.equal(legacy.heldFishWeight,0);
  for (const fields of [{heldFishSpecies:-2},{heldFishSpecies:1.5},{heldFishSpecies:1,heldFishWeight:0},{heldFishWeight:-1},{heldFishWeight:'3'}])
    assert.equal((await post('/v1/state',{pose:{...pose(),...fields}},guest.token)).status,400);
});


test('four-character room codes are distinct and accept lowercase with surrounding spaces',async t=>{
  const post=await fixture(t);
  const first=await post('/v1/open',hello);
  const second=await post('/v1/open',hello);
  assert.match(first.code,/^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$/);
  assert.match(second.code,/^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$/);
  assert.notEqual(first.code,second.code);
  const joined=await post('/v1/join',{...hello,code:'  '+first.code.toLowerCase()+'  '});
  assert.equal(joined.ok,true);assert.equal(joined.hostId,first.id);
});
