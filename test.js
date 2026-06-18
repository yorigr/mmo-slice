// Teste da lógica autoritativa SEM rede — chama as funções do servidor direto.
const { players, ABILITIES, handleMove, startCast, applyDamage, newEvents, spawnPlayer, CONFIG } = require('./server.js');

let passed = 0;
function check(label, cond) {
  if (cond) { console.log(`  ✅ ${label}`); passed++; }
  else { console.log(`  ❌ ${label}`); process.exitCode = 1; }
}

console.log('\n=== TESTE 1: Movimento legítimo é aceito ===');
const a = spawnPlayer('A', 'Atacante');
a.x = 100; a.y = 100; a.lastSeenAt = Date.now() - 50;
players.set('A', a);
handleMove(a, { x: 105, y: 100 });
check('player moveu para perto da posição pedida', Math.abs(a.x - 105) < 1);
check('nenhum movimento rejeitado', a.rejectedMoves === 0);

console.log('\n=== TESTE 2: Speedhack é rejeitado e corrigido ===');
players.clear(); // limpar antes para evitar colisões do teste anterior
const b = spawnPlayer('B', 'Cheater');
b.x = 100; b.y = 100; b.lastSeenAt = Date.now() - 50;
players.set('B', b);
handleMove(b, { x: 700, y: 100 });
check('movimento foi marcado como rejeitado', b.rejectedMoves === 1);
check('player NÃO teleportou para 700', b.x < 700);
check('player avançou muito menos do que pediu (< 200px)', b.x < 300);

console.log('\n=== TESTE 3: Slash respeita alcance ===');
const atk = spawnPlayer('ATK', 'Atacante');
const near = spawnPlayer('NEAR', 'Perto');
const far  = spawnPlayer('FAR',  'Longe');
players.clear();
atk.x = 100; atk.y = 100;
near.x = 130; near.y = 100;
far.x  = 400; far.y  = 100;
players.set('ATK', atk); players.set('NEAR', near); players.set('FAR', far);
const r1 = startCast(atk, 'slash', near.x, near.y, 1000);
check('slash resolveu imediatamente', r1.resolved === true);
check('NEAR perdeu HP correto (12)', near.hp === CONFIG.MAX_HP - ABILITIES.slash.damage);
check('FAR não tomou dano', far.hp === CONFIG.MAX_HP);
check('hit event foi gerado', r1.events && r1.events.hits.length >= 1);

console.log('\n=== TESTE 4: Cooldown de slash é forçado ===');
const r2 = startCast(atk, 'slash', near.x, near.y, 1100);
check('segundo ataque imediato rejeitado por cooldown', r2.rejected === 'cooldown');

console.log('\n=== TESTE 5: Morte por dano acumulado ===');
players.clear();
const killer = spawnPlayer('K', 'Killer');
const victim = spawnPlayer('V', 'Victim');
killer.x = 100; killer.y = 100;
victim.x = 120; victim.y = 100; victim.hp = ABILITIES.slash.damage;
players.set('K', killer); players.set('V', victim);
const ev = newEvents();
applyDamage(victim, ABILITIES.slash.damage, ev);
check('vítima chegou a 0 HP', victim.hp === 0);
check('vítima marcada como morta', victim.dead === true);
check('evento de morte foi gerado', ev.deaths.length === 1);


console.log('\n=== TESTE 6: Colisao player-player ===');
players.clear();
const p1 = spawnPlayer('P1', 'Um');
const p2 = spawnPlayer('P2', 'Dois');
p1.x = 100; p1.y = 100; p1.lastSeenAt = Date.now() - 100;
p2.x = 110; p2.y = 100; // sobreposto (dist=10, menor que raio*2=36)
players.set('P1', p1); players.set('P2', p2);
handleMove(p1, { x: 108, y: 100 }); // tentar andar em cima de P2
const distAfter = Math.hypot(p1.x - p2.x, p1.y - p2.y);
check('colisao empurrou players para longe um do outro', distAfter >= 34);
check('p1 nao teleportou para longe', Math.abs(p1.x - 100) < 80);

console.log('\n=== RESULTADO: ' + passed + ' verificacoes passaram ===\n');
process.exit(process.exitCode || 0);
