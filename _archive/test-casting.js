const { players, ABILITIES, spawnPlayer, startCast, applyDamage, resolveDueCasts, newEvents, CONFIG } = require('./server.js');

let passed = 0, failed = 0;
function check(label, cond) {
  if (cond) { console.log('  OK  ' + label); passed++; }
  else { console.log('  XX  ' + label); failed++; }
}

console.log('\n=== TESTE A: Cast instantaneo (Slash) resolve na hora ===');
players.clear();
const a1 = spawnPlayer('A1'); const t1 = spawnPlayer('T1');
a1.x = 100; a1.y = 100; t1.x = 130; t1.y = 100;
players.set('A1', a1); players.set('T1', t1);
const rA = startCast(a1, 'slash', 130, 100, 1000);
check('slash resolveu imediatamente', rA.resolved === true);
check('alvo tomou dano de slash (12)', t1.hp === CONFIG.MAX_HP - 12);

console.log('\n=== TESTE B: Cast com tempo NAO resolve antes da hora ===');
players.clear();
const a2 = spawnPlayer('A2'); const t2 = spawnPlayer('T2');
a2.x = 100; a2.y = 100; t2.x = 130; t2.y = 100;
players.set('A2', a2); players.set('T2', t2);
const rB = startCast(a2, 'heavy', 130, 100, 1000); // castTime 400ms
check('heavy entrou em estado de casting', rB.casting === true);
check('mana foi consumida no inicio (15)', a2.mana === CONFIG.MAX_MANA - 15);
let ev = newEvents();
resolveDueCasts(1200, ev); // 200ms depois — ainda nao terminou
check('alvo NAO tomou dano antes do cast terminar', t2.hp === CONFIG.MAX_HP);
check('ainda esta conjurando', a2.casting !== null);

console.log('\n=== TESTE C: Cast resolve quando o tempo passa ===');
ev = newEvents();
resolveDueCasts(1400, ev); // 400ms depois — terminou
check('alvo tomou dano de heavy (30)', t2.hp === CONFIG.MAX_HP - 30);
check('estado de casting foi limpo', a2.casting === null);
check('evento de hit foi gerado', ev.hits.length === 1 && ev.hits[0].ability === 'Heavy Blow');

console.log('\n=== TESTE D: Cast e INTERROMPIDO por dano ===');
players.clear();
const caster = spawnPlayer('C'); const enemy = spawnPlayer('E');
caster.x = 100; caster.y = 100; enemy.x = 130; enemy.y = 100;
players.set('C', caster); players.set('E', enemy);
startCast(caster, 'bolt', 130, 100, 2000); // bolt: castTime 900ms, interruptivel
check('caster esta conjurando bolt', caster.casting !== null);
const manaAposInicio = caster.mana;
ev = newEvents();
applyDamage(caster, 10, ev); // leva um golpe no meio do cast
check('cast foi interrompido (casting = null)', caster.casting === null);
check('evento de interrupcao gerado', ev.interrupts.length === 1);
check('mana NAO foi devolvida (custo da interrupcao)', caster.mana === manaAposInicio);
// agora o cast nunca resolve:
ev = newEvents();
resolveDueCasts(3000, ev);
check('inimigo NAO tomou dano (cast cancelado)', enemy.hp === CONFIG.MAX_HP);

console.log('\n=== TESTE E: Cooldown bloqueia recast ===');
players.clear();
const c2 = spawnPlayer('C2'); const e2 = spawnPlayer('E2');
c2.x = 100; c2.y = 100; e2.x = 130; e2.y = 100;
players.set('C2', c2); players.set('E2', e2);
startCast(c2, 'slash', 130, 100, 5000);
const rE = startCast(c2, 'slash', 130, 100, 5100); // 100ms depois, cooldown 600ms
check('recast dentro do cooldown e rejeitado', rE.rejected === 'cooldown');
const rE2 = startCast(c2, 'slash', 130, 100, 5700); // 700ms depois — cooldown acabou
check('recast apos cooldown e aceito', rE2.resolved === true);

console.log('\n=== TESTE F: Sem mana, sem cast ===');
players.clear();
const c3 = spawnPlayer('C3');
c3.mana = 5; // bolt custa 20
players.set('C3', c3);
const rF = startCast(c3, 'bolt', 0, 0, 6000);
check('cast rejeitado por falta de mana', rF.rejected === 'no_mana');
check('mana intacta apos rejeicao', c3.mana === 5);

console.log('\n=== TESTE G: Ranged so acerta dentro do alcance ===');
players.clear();
const c4 = spawnPlayer('C4'); const perto = spawnPlayer('P'); const longe = spawnPlayer('L');
c4.x = 100; c4.y = 100; perto.x = 200; perto.y = 100; longe.x = 700; longe.y = 100; // bolt range 320
players.set('C4', c4); players.set('P', perto); players.set('L', longe);
startCast(c4, 'bolt', 0, 0, 7000);
ev = newEvents();
resolveDueCasts(7900, ev); // resolve apos 900ms
check('bolt acertou o alvo dentro do alcance', perto.hp === CONFIG.MAX_HP - 22);
check('bolt NAO acertou o alvo fora do alcance', longe.hp === CONFIG.MAX_HP);

console.log('\n=== RESULTADO: ' + passed + ' passaram, ' + failed + ' falharam ===\n');
process.exit(failed ? 1 : 0);
