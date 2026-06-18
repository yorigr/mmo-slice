const { io } = require('socket.io-client');
const { httpServer } = require('./server.js');
const PORT = 3998;
let passed = 0, failed = 0;
function check(l, c) { if (c) { console.log('  OK  ' + l); passed++; } else { console.log('  XX  ' + l); failed++; } }
const wait = ms => new Promise(r => setTimeout(r, ms));

async function run() {
  await new Promise(r => httpServer.listen(PORT, r));
  const c1 = io('http://localhost:' + PORT, { transports: ['websocket'] });
  const c2 = io('http://localhost:' + PORT, { transports: ['websocket'] });
  let id1, id2, state = null, abilities = null;
  const interrupts = [];
  c1.on('joined', d => { id1 = d.id; abilities = d.abilities; });
  c2.on('joined', d => { id2 = d.id; });
  c1.on('state', s => state = s);
  c1.on('interrupts', i => interrupts.push(...i));
  c1.emit('join', { name: 'Atacante' });
  c2.emit('join', { name: 'Conjurador' });
  await wait(300);

  console.log('\n=== Setup pela rede ===');
  check('servidor enviou catalogo de abilities', abilities && !!abilities.bolt);
  check('2 players conectados', state && state.players.length === 2);

  // aproxima os dois (dentro do alcance melee)
  for (let i = 0; i < 40; i++) { c1.emit('move', { x: 120, y: 100 }); c2.emit('move', { x: 150, y: 100 }); await wait(50); }
  const p1 = state.players.find(p => p.id === id1);
  const p2 = state.players.find(p => p.id === id2);
  check('players proximos (dentro do alcance)', Math.hypot(p2.x-p1.x, p2.y-p1.y) < 60);

  console.log('\n=== Casting visivel no estado ===');
  c2.emit('cast', { abilityId: 'bolt', tx: p1.x, ty: p1.y }); // c2 comeca um cast longo
  await wait(100);
  let p2c = state.players.find(p => p.id === id2);
  check('estado mostra c2 conjurando bolt', p2c.casting && p2c.casting.abilityId === 'bolt');
  check('barra de cast tem tempo restante', p2c.casting.remaining > 0);

  console.log('\n=== Interrupcao pela rede ===');
  c1.emit('cast', { abilityId: 'slash', tx: p2.x, ty: p2.y }); // c1 interrompe com slash
  await wait(150);
  check('evento de interrupcao chegou', interrupts.length > 0);
  p2c = state.players.find(p => p.id === id2);
  check('c2 nao esta mais conjurando', !p2c.casting);

  console.log('\n=== Mana regenera ===');
  const manaBaixa = state.players.find(p => p.id === id2).mana;
  await wait(1000);
  const manaDepois = state.players.find(p => p.id === id2).mana;
  check('mana subiu com o tempo', manaDepois > manaBaixa);

  console.log('\n=== RESULTADO: ' + passed + ' passaram, ' + failed + ' falharam ===\n');
  c1.close(); c2.close(); httpServer.close();
  process.exit(failed ? 1 : 0);
}
run().catch(e => { console.error(e); process.exit(1); });
