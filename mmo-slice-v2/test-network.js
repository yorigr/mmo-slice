// Teste de INTEGRAÇÃO: sobe o servidor real e conecta 2 clientes via WebSocket.
const { io } = require('socket.io-client');
const { httpServer } = require('./server.js');

const PORT = 3999;
let passed = 0;
function check(label, cond) {
  if (cond) { console.log(`  ✅ ${label}`); passed++; }
  else { console.log(`  ❌ ${label}`); process.exitCode = 1; }
}

const wait = (ms) => new Promise(r => setTimeout(r, ms));

async function run() {
  await new Promise(r => httpServer.listen(PORT, r));
  console.log(`\nServidor de teste no ar (porta ${PORT})\n`);

  const c1 = io(`http://localhost:${PORT}`, { transports: ['websocket'] });
  const c2 = io(`http://localhost:${PORT}`, { transports: ['websocket'] });

  let id1, id2;
  let lastState = null;
  let hitsReceived = [];

  c1.on('joined', (d) => { id1 = d.id; });
  c2.on('joined', (d) => { id2 = d.id; });
  c1.on('state', (s) => { lastState = s; });
  c1.on('hits', (h) => { hitsReceived.push(h); });

  c1.emit('join', { name: 'Cliente1' });
  c2.emit('join', { name: 'Cliente2' });
  await wait(300);

  console.log('=== Conexão ===');
  check('cliente 1 recebeu ID', !!id1);
  check('cliente 2 recebeu ID', !!id2);
  check('broadcast de estado chegou', lastState !== null);
  check('estado contém os 2 players', lastState && lastState.players.length === 2);

  console.log('\n=== Movimento sincronizado ===');
  // envia alvos fixos e próximos repetidamente; o servidor limita a velocidade,
  // então deixamos eles "andarem" até convergir (respeitando MAX_SPEED).
  for (let i = 0; i < 40; i++) {
    c1.emit('move', { x: 120, y: 100 });
    c2.emit('move', { x: 150, y: 100 });
    await wait(50);
  }
  const p1 = lastState.players.find(p => p.id === id1);
  const p2 = lastState.players.find(p => p.id === id2);
  check('cliente 1 convergiu para o alvo (x≈120)', Math.abs(p1.x - 120) < 10);
  check('cliente 2 está dentro do alcance de ataque', Math.hypot(p2.x - p1.x, p2.y - p1.y) < 60);

  console.log('\n=== Combate pela rede ===');
  const hpAntes = p2.hp;
  c1.emit('attack');
  await wait(150);
  check('evento de hit foi transmitido', hitsReceived.length > 0);
  const p2Depois = lastState.players.find(p => p.id === id2);
  check('cliente 2 perdeu HP após o ataque', p2Depois.hp < hpAntes);

  console.log('\n=== Cooldown pela rede ===');
  const hpMeio = p2Depois.hp;
  c1.emit('attack'); // spam imediato — deve ser ignorado pelo cooldown
  await wait(150);
  const p2Spam = lastState.players.find(p => p.id === id2);
  check('ataque spammado NÃO causou dano extra', p2Spam.hp === hpMeio);

  console.log(`\n=== RESULTADO: ${passed} verificações passaram ===\n`);
  c1.close(); c2.close(); httpServer.close();
  process.exit(process.exitCode || 0);
}

run().catch(e => { console.error(e); process.exit(1); });
