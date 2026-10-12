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
  const a=await post('/v1/open',hello);assert.equal(a.ok,true);assert.equal(a.code.length,8);
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
  assert.equal((await post('/v1/join',{...hello,code:'NOTAROOM'})).status,404);
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
  await post('/v1/join',{...hello,code:'NOTAROOM'});await post('/v1/join',{...hello,code:'NOTAROOM'});await post('/v1/join',{...hello,code:'NOTAROOM'});
  assert.equal((await post('/v1/join',{...hello,code:'NOTAROOM'})).status,429);
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
