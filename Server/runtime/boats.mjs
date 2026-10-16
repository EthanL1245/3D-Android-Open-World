const boats = { raft:1, rowboat:2, sailboat:4, yacht:8 };
function invalid() { throw Object.assign(new Error('Invalid boat state.'),{status:400}); }
function number(v,min,max,fallback=0) { if(v===undefined)return fallback; if(typeof v!=='number'||!Number.isFinite(v)||v<min||v>max)invalid(); return v; }
function key(v) { if(v===undefined||v===null)return ''; if(typeof v!=='string'||v.length>64||!/^[a-zA-Z0-9]*$/.test(v))invalid();return v; }
export function boatPose(v) {
  let boat=null;
  if(v.boat!=null){
    const b=v.boat;if(typeof b!=='object'||!Object.hasOwn(boats,b.kind)||!key(b.key))invalid();
    boat={key:key(b.key),kind:b.kind,x:number(b.x,-100000,100000),y:number(b.y,-100000,100000),z:number(b.z,-100000,100000),
      pitch:number(b.pitch,-360,360),yaw:number(b.yaw,-360,360),roll:number(b.roll,-360,360)};
  }
  const seat=number(v.rideSeat,-1,2,-1),revision=number(v.rideRevision,0,2147483647);
  if(!Number.isInteger(seat)||!Number.isInteger(revision))invalid();
  return {boat:v.world===0?boat:null,rideOwner:key(v.rideOwner),rideKey:key(v.rideKey),rideSeat:seat,rideRevision:revision,
    rideX:number(v.rideX,-12,12),rideY:number(v.rideY,-5,5),rideZ:number(v.rideZ,-12,12),rideYaw:number(v.rideYaw,-360,360),
    boatInputX:number(v.boatInputX,-1,1),boatInputY:number(v.boatInputY,-1,1),boatLookYaw:number(v.boatLookYaw,-360,360)};
}
export function clearRide(p){Object.assign(p.pose,{rideOwner:'',rideKey:'',rideSeat:-1,boatInputX:0,boatInputY:0});}
export function pruneBoatRiders(room) {
  for(const p of room.players.values()){
    const owner=room.players.get(p.pose.rideOwner);
    if(p.pose.world!==0||!owner||owner.pose.world!==0||!owner.pose.boat||owner.pose.boat.key!==p.pose.rideKey)clearRide(p);
  }
}
export function acceptBoatRide(p, previous) {
  const v=p.pose,room=p.room,owner=room.players.get(v.rideOwner),boat=owner?.pose.boat;
  if(v.rideSeat<0||v.world!==0||!boat||owner.pose.world!==0||boat.key!==v.rideKey){clearRide(p);return;}
  const riders=[...room.players.values()].filter(q=>q!==p&&q.pose.rideOwner===owner.id&&q.pose.rideKey===boat.key);
  const already=previous?.rideOwner===owner.id&&previous?.rideKey===boat.key;
  if((!already&&Math.hypot(v.x-boat.x,v.y-boat.y,v.z-boat.z)>14)||riders.length>=boats[boat.kind]){clearRide(p);return;}
  // Reserve the owner's front position; guests never get promoted into it.
  if(boat.kind==='rowboat'&&p!==owner&&riders.some(q=>q!==owner)){clearRide(p);return;}
  if(v.rideSeat>0){
    const allowed=boat.kind==='rowboat'?(p===owner?1:2):1;
    if(v.rideSeat!==allowed||riders.some(q=>q.pose.rideSeat===v.rideSeat))v.rideSeat=0;
  }
  if(v.rideSeat===0){v.boatInputX=0;v.boatInputY=0;}
}
