import http from 'node:http';

// Deployment foundation. Unity networking is a separate implementation step.
const port = Number(process.env.PORT || 3000);
const host = process.env.HOST || '127.0.0.1';
const server = http.createServer({ requestTimeout: 10000, headersTimeout: 10000 }, (req, res) => {
  res.setHeader('Content-Type', 'application/json');
  res.setHeader('Cache-Control', 'no-store');
  if (req.method === 'GET' && req.url === '/health') {
    res.end(JSON.stringify({ ok: true, service: 'fishing-server', protocol: 1,
      multiplayerReady: false, release: process.env.RELEASE_ID || 'development' }));
  } else {
    res.writeHead(404);
    res.end(JSON.stringify({ error: 'not_found' }));
  }
});
server.maxConnections = 128;
server.listen(port, host, () => console.log(`Fishing service listening on ${host}:${port}`));
for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => {
    server.close(() => process.exit(0));
    setTimeout(() => process.exit(1), 5000).unref();
  });
}
