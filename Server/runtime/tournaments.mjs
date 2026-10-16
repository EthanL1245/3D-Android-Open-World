import { randomInt } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync, renameSync, mkdirSync } from 'node:fs';
import { dirname } from 'node:path';
import { tournamentIslands } from './tournament-species.mjs';

const validId=v=>typeof v==='string' && /^[a-f0-9]{32}$/.test(v);
const fail=message=>{throw Object.assign(new Error(message),{status:400});};
const active=t=>t && ['inviting','countdown','running'].includes(t.phase);
export function createTournaments({clock=Date.now,random=randomInt,ledgerPath=null}={}) {
  // Only escrow receipts are durable. A process restart cancels games and refunds
  // pending entries rather than recreating rooms with missing players.
  const receipts=new Map();
  if(ledgerPath && existsSync(ledgerPath)) {
    const saved=JSON.parse(readFileSync(ledgerPath,'utf8'));
    if(saved.version!==1 || !Array.isArray(saved.receipts))throw Error('Invalid tournament escrow ledger');
    for(const r of saved.receipts){if(!validId(r.ticket))throw Error('Invalid tournament receipt');receipts.set(r.ticket,r);}
    for(const r of receipts.values())if(!r.ready){r.ready=true;r.refund=true;r.payout=r.amount;}
  }
  function save(){
    if(!ledgerPath)return;
    mkdirSync(dirname(ledgerPath),{recursive:true});
    const temp=ledgerPath+'.tmp';
    writeFileSync(temp,JSON.stringify({version:1,receipts:[...receipts.values()]}),{mode:0o600});renameSync(temp,ledgerPath);
  }
  if(receipts.size)save();
  function acceptEntry(t,p,c){
    if(!validId(c.ticket))fail('Invalid wager receipt.');
    const prior=receipts.get(c.ticket);
    if(prior){if(prior.id===t.id && prior.player===p.id && !prior.ready)return;fail('Wager receipt already closed or used.');}
    if(!Number.isInteger(c.balance)||c.balance<t.wager||c.balance>2147483647)fail('Not enough coins for this wager.');
    const receipt={ticket:c.ticket,id:t.id,player:p.id,amount:t.wager,ready:false,refund:false,payout:0};
    receipts.set(c.ticket,receipt);
    try{save();}catch(e){receipts.delete(c.ticket);throw e;}
  }
  function refundEntry(e){
    if(!e.ticket)return;
    const r=receipts.get(e.ticket);if(r && !r.ready){r.ready=true;r.refund=true;r.payout=r.amount;}
  }
  function finish(t,cancel=false,reason=''){
    if(!active(t))return;
    const previous={phase:t.phase,reason:t.reason,winnerId:t.winnerId,winnerName:t.winnerName,pool:t.pool};
    const previousReceipts=t.entries.filter(e=>e.ticket).map(e=>({...receipts.get(e.ticket)}));
    t.phase=cancel?'cancelled':'ended';t.reason=reason;
    const ranked=t.entries.filter(e=>e.status==='joined').sort((a,b)=>b.points-a.points || b.biggestKg-a.biggestKg || a.scoreAt-b.scoreAt || a.order-b.order);
    const winner=!cancel && ranked[0]?.points>0?ranked[0]:null;
    t.winnerId=winner?.id??'';t.winnerName=winner?.name??'';
    t.pool=ranked.length*t.wager;
    for(const e of ranked){const r=receipts.get(e.ticket);if(r && !r.ready){r.ready=true;r.refund=!winner;r.payout=winner?(e===winner?t.pool:0):r.amount;}}
    if(!winner && !cancel)t.reason='No qualifying catches. All entry fees refunded.';
    try{save();}catch(error){Object.assign(t,previous);for(const r of previousReceipts)receipts.set(r.ticket,r);throw error;}
  }
  function tick(room){
    const t=room.tournament;if(!active(t))return;
    if(t.phase==='inviting'){
      for(const e of t.entries)if(e.status==='pending' && (!room.players.has(e.id)||clock()>=t.inviteEnds))e.status='declined';
      if(t.entries.every(e=>e.status!=='pending')){
        const joined=t.entries.filter(e=>e.status==='joined');
        if(joined.length<2){finish(t,true,'At least two players must join. Entry fees refunded.');return;}
        const choices=tournamentIslands[t.island].fish;
        t.target=choices[random(choices.length)];
        t.phase='countdown';t.startsAt=clock()+5000;t.endsAt=t.startsAt+360000;
      }
    }
    if(t.phase==='countdown' && clock()>=t.startsAt){
      t.phase='running';
      for(const e of t.entries)e.startSequence=room.players.get(e.id)?.pose.castSequence??0;
    }
    if(t.phase==='running' && clock()>=t.endsAt)finish(t);
  }
  function view(room){
    tick(room);const t=room.tournament;if(!t)return null;
    const rows=t.entries.map(({ticket,seen,...e})=>e).sort((a,b)=>b.points-a.points || b.biggestKg-a.biggestKg || a.scoreAt-b.scoreAt || a.order-b.order);
    const biggest=rows.filter(e=>e.status==='joined').sort((a,b)=>b.biggestKg-a.biggestKg || a.order-b.order)[0];
    return {serverTime:clock(),id:t.id,phase:t.phase,island:t.island,islandName:tournamentIslands[t.island].name,wager:t.wager,
      pool:rows.filter(e=>e.status==='joined').length*t.wager,species:t.target?.id??-1,speciesName:t.target?.name??'',
      inviteSeconds:Math.max(0,(t.inviteEnds-clock())/1000),startSeconds:Math.max(0,((t.startsAt??0)-clock())/1000),
      secondsLeft:Math.max(0,((t.endsAt??0)-clock())/1000),entries:rows,winnerId:t.winnerId??'',winnerName:t.winnerName??'',
      biggestName:biggest?.biggestKg>0?biggest.name:'',biggestKg:biggest?.biggestKg??0,reason:t.reason??''};
  }
  function action(p,c){
    if(!c || typeof c!=='object')fail('Missing tournament action.');
    const room=p.room;tick(room);let t=room.tournament;
    if(c.op==='create'){
      if(p.id!==room.host)fail('Only the host can start a tournament.');
      if(t?.id===c.id && t.entries[0].ticket===c.ticket)return view(room);
      if(active(t))fail('A tournament is already in progress.');
      if(!validId(c.id)||!Number.isInteger(c.island)||![0,1,3,4].includes(c.island))fail('Choose a fishing island.');
      if(!(p.pose.islandMask&(1<<c.island)))fail('You have not unlocked that island.');
      if(!Number.isInteger(c.wager)||c.wager<1||c.wager>1000000)fail('Wager must be 1 to 1,000,000 coins.');
      if(room.players.size<2)fail('At least two players must be in the room.');
      t={id:c.id,island:c.island,wager:c.wager,phase:'inviting',inviteEnds:clock()+45000,entries:[]};
      for(const q of room.players.values())t.entries.push({id:q.id,name:q.name,status:q===p?'joined':(q.pose.islandMask&(1<<c.island))?'pending':'locked',
        ticket:q===p?c.ticket:'',points:0,biggestKg:0,catches:0,scoreAt:0,order:t.entries.length,seen:new Set()});
      acceptEntry(t,p,c);room.tournament=t;return view(room);
    }
    if(!t || c.id!==t.id)fail('Tournament no longer available.');
    const e=t.entries.find(e=>e.id===p.id);
    if(c.op==='join'){
      if(e?.status==='joined' && e.ticket===c.ticket)return view(room);
      if(t.phase!=='inviting'||!e||e.status!=='pending')fail('This invitation has closed.');
      if(!(p.pose.islandMask&(1<<t.island)))fail('You have not unlocked that island.');
      acceptEntry(t,p,c);e.status='joined';e.ticket=c.ticket;
    } else if(c.op==='decline'){
      if(t.phase==='inviting' && e?.status==='pending')e.status='declined';
    } else if(c.op==='catch'){
      if(t.phase!=='running'||!e||e.status!=='joined')fail('Tournament is not accepting catches.');
      if(!Number.isInteger(c.cast)||c.cast<=e.startSequence||c.cast!==p.pose.castSequence)fail('Only new tournament casts count.');
      if(e.seen.has(c.cast))return view(room);
      if(c.island!==t.island || c.species!==t.target.id || p.pose.world!==0)fail('This catch does not match the tournament.');
      if(typeof c.kg!=='number'||!Number.isFinite(c.kg)||c.kg<=0||c.kg>t.target.maxKg+.01||e.catches>=120)fail('Invalid tournament catch.');
      e.seen.add(c.cast);e.points+=Math.max(1,Math.round(c.kg*100));e.biggestKg=Math.max(e.biggestKg,c.kg);e.catches++;e.scoreAt=clock();
    } else if(c.op==='cancel'){
      if(p.id!==room.host || t.phase!=='inviting')fail('Only the host can cancel invitations.');
      finish(t,true,'The host cancelled. Entry fees refunded.');
    } else fail('Unknown tournament action.');
    return view(room);
  }
  function leave(p){
    const t=p.room.tournament;if(!active(t))return;
    if(p.id===p.room.host){finish(t,true,'The host disconnected. Entry fees refunded.');return;}
    if(t.phase==='inviting'||t.phase==='countdown'){
      const e=t.entries.find(e=>e.id===p.id);
      if(e){refundEntry(e);e.status='declined';save();}
      if(t.phase==='countdown' && t.entries.filter(e=>e.status==='joined').length<2)finish(t,true,'Not enough players remain. Entry fees refunded.');
    }
  }
  function receipt(c){
    if(!c || !validId(c.ticket))fail('Invalid wager receipt.');
    let r=receipts.get(c.ticket);
    // Close an unknown reservation before refunding it; delayed join/create
    // requests with that ticket can never subsequently charge the entry.
    if(!r && c.cancelMissing===true){r={ticket:c.ticket,id:'',amount:0,ready:true,refund:true,payout:0};receipts.set(c.ticket,r);save();}
    return r?{ready:r.ready,refund:r.refund,payout:r.payout}:{ready:false,refund:false,payout:0};
  }
  return {action,view,tick,leave,receipt};
}
