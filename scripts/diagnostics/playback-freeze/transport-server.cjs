// Loopback-only HTTP/1.1 + TLS/ALPN HTTP/2 fault fixture. Node built-ins only.
const fs = require('node:fs');
const http = require('node:http');
const http2 = require('node:http2');
const cfg = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
// Parent death/outer timeout closes stdin, so fixtures cannot outlive a run.
process.stdin.resume();
process.stdin.on('end', () => process.exit(0));
const media = Object.fromEntries(['video.mp4', 'audio.m4a'].map(n => ['/' + n, fs.readFileSync(cfg.media + '/' + n)]));
const start = performance.now();
const now = () => (performance.now() - start) / 1000;
const log = x => fs.appendFileSync(cfg.ledger, JSON.stringify({t: now(), ...x}) + '\n');
let count = 0, nextConnection = 0, firstVideo = null;
const connections = new WeakMap();
const server = cfg.tls ? http2.createSecureServer({key: fs.readFileSync(cfg.key), cert: fs.readFileSync(cfg.cert), allowHTTP1: true}) : http.createServer();
server.on('sessionError', e => log({event: 'session-error', error: e.code}));
server.on('request', (req, res) => {
  req.on('error', () => {}); res.on('error', () => {});
  const socket = req.httpVersionMajor === 2 ? req.stream.session.socket : req.socket;
  if (!connections.has(socket)) connections.set(socket, ++nextConnection);
  const data = media[req.url];
  if (!data) { res.writeHead(404); res.end(); return; }
  const match = /^bytes=(\d+)-(\d*)$/.exec(req.headers.range || '');
  const first = match ? Number(match[1]) : 0;
  const last = match && match[2] ? Math.min(Number(match[2]), data.length - 1) : data.length - 1;
  if (first >= data.length) { res.writeHead(416); res.end(); return; }
  let stop = last + 1;
  if (req.url === '/video.mp4') {
    if (firstVideo === null) firstVideo = now();
    count++;
    const cutoff = Math.floor(data.length / 3) + 17;
    if (cfg.fault === 'outage' && now() - firstVideo < cfg.release) stop = Math.max(first, Math.min(stop, cutoff));
    // N failed attempts at a fixed offset, then healthy: distinct from time outage.
    if (cfg.fault === 'attempts' && count <= cfg.failures) stop = Math.max(first, Math.min(stop, cutoff));
    // Long requests are refused, bounded requests succeed (request/stream policy).
    if (cfg.fault === 'request-limit' && last - first + 1 > cfg.limit) stop = Math.max(first, Math.min(stop, cutoff));
    // Connections/streams recycled after bytes; retries can make forward progress.
    if (cfg.fault === 'recycle') stop = Math.min(stop, first + cfg.limit);
    if (cfg.fault === 'h2-reset' && req.httpVersionMajor === 2 && count <= cfg.failures) stop = Math.max(first, Math.min(stop, cutoff));
  }
  const broken = stop < last + 1;
  log({event: 'request', path: req.url, protocol: req.httpVersion, alpn: socket.alpnProtocol || null,
       connection: connections.get(socket), stream: req.stream?.id, range: req.headers.range || null,
       first, last, sent: stop - first, truncated: broken});
  const headers = {'content-type': 'video/mp4', 'accept-ranges': 'bytes', 'content-length': last - first + 1};
  if (match) headers['content-range'] = `bytes ${first}-${last}/${data.length}`;
  if (req.httpVersionMajor === 1) headers.connection = 'close';
  res.writeHead(match ? 206 : 200, headers);
  res.flushHeaders();
  const finish = () => {
    if (!broken) { res.end(); return; }
    // Allow body to reach client before truncation/RST_STREAM, not discard writes.
    setTimeout(() => {
      if (req.httpVersionMajor === 2) { if (!req.stream.destroyed) req.stream.close(http2.constants.NGHTTP2_INTERNAL_ERROR); }
      else res.destroy();
    }, 5);
  };
  if (stop > first) res.write(data.subarray(first, stop), finish); else finish();
});
server.listen(0, '127.0.0.1', () => console.log(JSON.stringify({port: server.address().port})));
