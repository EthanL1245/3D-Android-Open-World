import http from 'node:http';
import { randomBytes, randomInt } from 'node:crypto';

export const PROTOCOL = 'fishing-walk-v1';
const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
export function createSessionServer({ clock = Date.now, timeout = 30000 } = {}) {
  const rooms = new Map(), sessions = new Map(), limits = new Map();
  const fail = (status, message) => { throw Object.assign(new Error(message), { status }); };
  const snapshot = room => [...room.players.values()].map(p => ({ id: p.id, name: p.name, ...p.pose }));
  function remove(p) {
    sessions.delete(p.token);
    p.room.players.delete(p.id);
    if (p.id === p.room.host) {
      for (const peer of p.room.players.values()) sessions.delete(peer.token);
      rooms.delete(p.room.code);
    }
  }
  function expire() {
    const now = clock();
    for (const p of sessions.values()) if (now - p.seen > timeout) remove(p);
    for (const [ip, limit] of limits) if (now - limit.start > 60000) limits.delete(ip);
  }
  const timer = setInterval(expire, 5000); timer.unref();
  function pose(value) {
    if (!value || !['x','y','z','yaw'].every(k => typeof value[k] === 'number' && Number.isFinite(value[k]))) fail(400, 'Invalid player position.');
    if (['x','y','z'].some(k => Math.abs(value[k]) > 100000) || ![0,1,2].includes(value.world)) fail(400, 'Invalid world position.');
    // Optional fields preserve compatibility with older walking-only clients.
    for (const [key, max] of [['rodTier',4],['castSequence',2147483647]])
      if (value[key] !== undefined && (!Number.isInteger(value[key]) || value[key] < 0 || value[key] > max)) fail(400, 'Invalid fishing animation state.');
    if (value.castAge !== undefined && (typeof value.castAge !== 'number' || !Number.isFinite(value.castAge) || value.castAge < -1 || value.castAge > 60)) fail(400, 'Invalid cast age.');
    if (value.castDuration !== undefined && (typeof value.castDuration !== 'number' || !Number.isFinite(value.castDuration) || value.castDuration < .1 || value.castDuration > 60)) fail(400, 'Invalid cast duration.');
    if (value.reelTime !== undefined && (typeof value.reelTime !== 'number' || !Number.isFinite(value.reelTime) || value.reelTime < 0 || value.reelTime > 10000000)) fail(400, 'Invalid reel animation time.');
    if (value.heldFishSpecies !== undefined && (!Number.isInteger(value.heldFishSpecies) || value.heldFishSpecies < -1 || value.heldFishSpecies > 4096)) fail(400, 'Invalid held fish.');
    if (value.heldFishWeight !== undefined && (typeof value.heldFishWeight !== 'number' || !Number.isFinite(value.heldFishWeight) || value.heldFishWeight < 0 || value.heldFishWeight > 1000000)) fail(400, 'Invalid held fish weight.');
    if (value.heldFishSpecies >= 0 && !(value.heldFishWeight > 0)) fail(400, 'Missing held fish weight.');
    return { x: value.x, y: value.y, z: value.z, yaw: ((value.yaw % 360) + 360) % 360,
      world: value.world, swimming: value.swimming === true,
      rodEquipped: value.rodEquipped === true, rodTier: value.rodTier ?? 0,
      castSequence: value.castSequence ?? 0, castAge: value.castAge ?? -1,
      castDuration: value.castDuration ?? .72,
      reeling: value.reeling === true, hasFish: value.hasFish === true,
      lureInWater: value.lureInWater === true, reelTime: value.reelTime ?? 0,
      heldFishSpecies: value.heldFishSpecies ?? -1, heldFishWeight: value.heldFishWeight ?? 0 };
  }
  function add(room, data) {
    const p = { id: randomBytes(8).toString('hex'), token: randomBytes(32).toString('hex'),
      name: typeof data.name === 'string' ? data.name.replace(/[^a-zA-Z0-9 _-]/g, '').trim().slice(0,20) || 'Angler' : 'Angler',
      pose: pose(data.pose), seen: clock(), room };
    room.players.set(p.id, p); sessions.set(p.token, p);
    if (!room.host) room.host = p.id;
    return p;
  }
  function dispatch(path, data, token) {
    expire();
    if (path === '/v1/open' || path === '/v1/join') {
      if (data.protocol !== PROTOCOL || typeof data.build !== 'string' || data.build.length > 100) fail(409, 'Install the same updated game version on both devices.');
      if (sessions.size >= 32) fail(503, 'Server is full. Try again later.');
      pose(data.pose); // Validate before allocating a room.
      let room;
      if (path === '/v1/open') {
        if (rooms.size >= 8) fail(503, 'All rooms are in use. Try again later.');
        let code;
        do { code = Array.from({length:8}, () => alphabet[randomInt(alphabet.length)]).join(''); } while (rooms.has(code));
        room = { code, build: data.build, players: new Map(), host: null };
        rooms.set(code, room);
      } else {
        room = rooms.get(typeof data.code === 'string' ? data.code.trim().toUpperCase() : '');
        if (!room) fail(404, 'Room not found. Ask the host for the current code.');
        if (room.build !== data.build) fail(409, 'Both players need the same game version.');
        if (room.players.size >= 8) fail(409, 'This room is full (8 players).');
      }
      const p = add(room, data);
      return { ok:true, id:p.id, token:p.token, code:room.code, hostId:room.host, players:snapshot(room) };
    }
    const p = sessions.get(token);
    if (!p) fail(401, 'Session ended. The host may have left. Open or join again.');
    if (path === '/v1/leave') { remove(p); return {ok:true}; }
    if (path !== '/v1/state') fail(404, 'Not found.');
    p.pose = pose(data.pose); p.seen = clock();
    return { ok:true, code:p.room.code, hostId:p.room.host, players:snapshot(p.room) };
  }
  const server = http.createServer({ requestTimeout:5000, headersTimeout:5000, maxHeaderSize:8192 }, async (req, res) => {
    const reply = (status, value) => { if (!res.destroyed) { res.writeHead(status, {'Content-Type':'application/json','Cache-Control':'no-store'}); res.end(JSON.stringify(value)); } };
    const ip = req.socket.remoteAddress;
    let limit = limits.get(ip);
    if (!limit || clock() - limit.start > 1000) {
      if (!limit && limits.size >= 4096) { reply(503,{ok:false,error:'Busy. Try again.'}); req.resume(); return; }
      limit = { start:clock(), count:0, opens:0 }; limits.set(ip,limit);
    }
    if (++limit.count > 100) { reply(429,{ok:false,error:'Too many requests.'});req.resume();return; }
    if (req.method === 'GET' && req.url === '/health') { reply(200,{ok:true,protocol:PROTOCOL});return; }
    if (req.method !== 'POST' || !['/v1/open','/v1/join','/v1/state','/v1/leave'].includes(req.url)) {reply(404,{ok:false,error:'Not found.'});req.resume();return;}
    if ((req.url === '/v1/open' || req.url === '/v1/join') && ++limit.opens > 4) {reply(429,{ok:false,error:'Wait a moment before trying again.'});req.resume();return;}
    if (!(req.headers['content-type'] || '').startsWith('application/json')) {reply(415,{ok:false,error:'JSON required.'});req.resume();return;}
    try {
      let size=0; const chunks=[];
      for await (const chunk of req) { size+=chunk.length; if(size>4096){fail(413,'Request too large.');} chunks.push(chunk); }
      let data;
      try {data=JSON.parse(Buffer.concat(chunks).toString('utf8'));} catch {fail(400,'Invalid JSON.');}
      if (!data || typeof data !== 'object' || Array.isArray(data)) fail(400,'Invalid request.');
      reply(200,dispatch(req.url,data,(req.headers.authorization || '').replace(/^Bearer /,'')));
    } catch (e) { reply(e.status || 500,{ok:false,error:e.status ? e.message : 'Server error.'}); }
  });
  server.maxConnections=128;
  server.keepAliveTimeout=5000;
  server.on('close',()=>clearInterval(timer));
  return server;
}
