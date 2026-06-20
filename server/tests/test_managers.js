// ============================================================
// TESTES UNITÁRIOS — sistema gear-based
// Usa apenas assert nativo do Node.js. Rodar com:
//   node tests/test_managers.js
//
// IMPORTANTE: o código de produção (src/) está correto.
// Estes testes refletem a arquitetura atual: sem classes fixas,
// skills derivadas do equipamento (gear-based).
// ============================================================
'use strict';

const assert = require('assert');
const path   = require('path');

// ---- Helpers ----
let passed = 0, failed = 0;

function test(name, fn) {
  try {
    fn();
    console.log('  ✅ ' + name);
    passed++;
  } catch (err) {
    console.error('  ❌ ' + name);
    console.error('     ' + err.message);
    failed++;
  }
}

function section(title) {
  console.log('\n── ' + title + ' ──');
}

// ============================================================
// MOCK: WorldManager (sem io real, sem setInterval)
// ============================================================
class MockWorld {
  constructor() {
    this.players  = new Map();
    this.monsters = new Map();
    this.items    = new Map();
    this.events   = { hits: [], interrupts: [], deaths: [] };
    this._tickMs  = 500;
    this.zoneType = 'yellow';   // MockWorld usa zona yellow (25% destruição)
    this.io       = {
      emit: () => {},
      to: () => ({ emit: () => {} }),
    };
  }
  addPlayer(s)     { this.players.set(s.id, s); }
  removePlayer(id) { this.players.delete(id); }
  getPlayer(id)    { return this.players.get(id); }
  addMonster(s)    { this.monsters.set(s.id, s); }
  removeMonster(id){ this.monsters.delete(id); }
  addItem(s)       { this.items.set(s.id, s); }
  removeItem(id)   { this.items.delete(id); }
  emitHit(h)       { this.events.hits.push(h); }
  emitInterrupt(d) { this.events.interrupts.push(d); }
  emitDeath(d)     { this.events.deaths.push(d); }
}

// ---- Load managers ----
const WorldManager   = require(path.join(__dirname, '../src/managers/WorldManager'));
const PlayerManager  = require(path.join(__dirname, '../src/managers/PlayerManager'));
const CombatEngine   = require(path.join(__dirname, '../src/managers/CombatEngine'));
const MonsterManager = require(path.join(__dirname, '../src/managers/MonsterManager'));
const constants      = require(path.join(__dirname, '../src/config/constants'));

// ============================================================
// WorldManager
// ============================================================
section('WorldManager');

test('addPlayer / removePlayer / getPlayer', () => {
  const world = new MockWorld();
  world.addPlayer({ id: 'p1', name: 'Alice' });
  assert.ok(world.getPlayer('p1'), 'should find p1');
  assert.strictEqual(world.getPlayer('p1').name, 'Alice');
  world.removePlayer('p1');
  assert.strictEqual(world.getPlayer('p1'), undefined);
});

test('addMonster / removeMonster', () => {
  const world = new MockWorld();
  world.addMonster({ id: 'm1', type: 'goblin', hp: 40, maxHp: 40, x: 100, y: 100 });
  assert.ok(world.monsters.has('m1'));
  world.removeMonster('m1');
  assert.ok(!world.monsters.has('m1'));
});

test('addItem / removeItem', () => {
  const world = new MockWorld();
  world.addItem({ id: 'i1', type: 'sword_rusty', x: 50, y: 50 });
  assert.ok(world.items.has('i1'));
  world.removeItem('i1');
  assert.ok(!world.items.has('i1'));
});

// ============================================================
// PlayerManager
// ============================================================
section('PlayerManager');

test('createPlayer — spawns com arma espada e skills gear-based', () => {
  // Sistema gear-based: não existe "class". Identity = o que você equipa.
  // Player inicial nasce sempre com sword + skill_slash/skill_heavy_blow/skill_execute.
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('sock1', { name: 'Yuri' });
  assert.strictEqual(p.id, 'sock1');
  assert.strictEqual(p.name, 'Yuri');
  assert.strictEqual(p.equipment.weapon, 'sword',       'arma inicial deve ser sword');
  assert.strictEqual(p.selectedSkills.weapon_Q, 'skill_slash',      'Q deve ser skill_slash');
  assert.strictEqual(p.selectedSkills.weapon_W, 'skill_heavy_blow', 'W deve ser skill_heavy_blow');
  assert.strictEqual(p.selectedSkills.weapon_E, 'skill_execute',    'E deve ser skill_execute');
  assert.ok(p.hp > 0);
  assert.ok(p.hp === p.maxHp, 'nasce com HP cheio');
  assert.ok(world.getPlayer('sock1'), 'deve estar registrado no mundo');
});

test('createPlayer — equipar armadura de pano aumenta maxMana', () => {
  // No sistema gear-based, bônus de mana vem do equipamento, não de uma "classe".
  // cloth_chest dá +30 maxMana (gear.json).
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p = pm.createPlayer('m', { name: 'Caster' });
  const baseMana = p.maxMana;
  pm.equipItem(p, 'chest', 'cloth_chest');
  assert.ok(p.maxMana > baseMana,
    `cloth_chest deve aumentar maxMana: ${baseMana} → ${p.maxMana}`);
});

test('createPlayer — múltiplos players nascem de forma independente', () => {
  // Qualquer player pode usar qualquer arma: não existe restrição de classe.
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  ['Alice', 'Bob', 'Carol', 'Dave', 'Eve'].forEach((name, i) => {
    const p = pm.createPlayer('id-' + i, { name });
    assert.ok(p.hp > 0,               `${name} deve nascer vivo`);
    assert.ok(p.equipment.weapon,      `${name} deve ter arma equipada`);
    assert.ok(p.durability,            `${name} deve ter objeto de durabilidade`);
  });
});

test('handleMove — normal movement accepted', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s2', { name: 'Mover' });
  p.x = 100; p.y = 100;
  const now = Date.now();
  p.lastSeenAt = now - 500; // 500ms ago → pode mover 200*0.5*1.5+5 ≈ 155px
  pm.handleMove('s2', { x: 140, y: 140 }, now);
  assert.ok(Math.hypot(p.x - 140, p.y - 140) < 1,
    `player deve chegar em (140,140), ficou em (${p.x.toFixed(1)},${p.y.toFixed(1)})`);
});

test('handleMove — anti-speedhack: large teleport rejected', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s3', { name: 'Hacker' });
  p.x = 100; p.y = 100;
  const now = Date.now();
  p.lastSeenAt = now - 16; // 16ms → max ~200*0.016*1.5+5 ≈ 9.8px
  pm.handleMove('s3', { x: 800, y: 800 }, now);
  assert.ok(Math.hypot(p.x - 100, p.y - 100) < 20,
    `teleport deve ser rejeitado, moveu ${Math.hypot(p.x-100, p.y-100).toFixed(1)}px`);
  assert.ok(p.rejectedMoves > 0, 'rejectedMoves deve ser incrementado');
});

test('handleMove — dead player cannot move', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s4', { name: 'Dead' });
  p.x = 100; p.y = 100; p.dead = true;
  pm.handleMove('s4', { x: 500, y: 500 }, Date.now());
  assert.strictEqual(p.x, 100);
  assert.strictEqual(p.y, 100);
});

test('regenTick — mana and stamina regenerate', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s5', { name: 'Regen' });
  p.mana    = 10;
  p.stamina = 10;
  pm.regenTick(1.0); // 1 segundo
  assert.ok(p.mana    > 10, `mana deve regen, ficou ${p.mana}`);
  assert.ok(p.stamina > 10, `stamina deve regen, ficou ${p.stamina}`);
});

test('regenTick — dead player does not regen', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s6', { name: 'DeadRegen' });
  p.mana = 5; p.dead = true;
  pm.regenTick(5.0);
  assert.strictEqual(p.mana, 5, 'mana de player morto não deve mudar');
});

test('regenTick — mana does not exceed maxMana', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s7', { name: 'Cap' });
  p.mana = p.maxMana - 1;
  pm.regenTick(100.0); // tick grande
  assert.strictEqual(p.mana, p.maxMana, `mana deve ficar em ${p.maxMana}`);
});

test('removePlayer — removes from world', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  pm.createPlayer('s8', { name: 'Leave' });
  pm.removePlayer('s8');
  assert.strictEqual(world.getPlayer('s8'), undefined);
});

// ============================================================
// CombatEngine
// ============================================================
section('CombatEngine');

function makeCombatSetup() {
  const world = new MockWorld();
  const pm    = new PlayerManager(world);
  const ce    = new CombatEngine(world, pm);
  return { world, pm, ce };
}

test('startCast — instant skill resolves immediately', () => {
  // skill_slash: arma inicial espada, slot Q, castTime=0
  const { pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c1', { name: 'Cast' });
  caster.stamina = 100;
  const result = ce.startCast('c1', 'skill_slash', 0, 0);
  assert.ok(result.resolved, `esperava resolved, recebeu ${JSON.stringify(result)}`);
});

test('startCast — cast-time skill returns casting', () => {
  // skill_heavy_blow: arma inicial espada, slot W, tem castTime > 0
  const { pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c2', { name: 'Caster' });
  caster.stamina = 100;
  const result = ce.startCast('c2', 'skill_heavy_blow', 0, 0);
  assert.ok(result.casting, `esperava casting=true, recebeu ${JSON.stringify(result)}`);
  assert.ok(result.endsAt > Date.now(), 'endsAt deve ser no futuro');
  assert.ok(caster.casting, 'player.casting deve ser definido');
});

test('startCast — cooldown rejection', () => {
  const { pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c3', { name: 'CD' });
  caster.stamina = 1000;
  ce.startCast('c3', 'skill_slash', 0, 0);          // usa cooldown
  const result2 = ce.startCast('c3', 'skill_slash', 0, 0);
  assert.strictEqual(result2.rejected, 'cooldown');
});

test('startCast — no_stamina rejection', () => {
  // skill_slash custa stamina; com stamina=0 deve rejeitar
  const { pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c4', { name: 'NoSt' });
  caster.stamina = 0;
  const result = ce.startCast('c4', 'skill_slash', 0, 0);
  assert.strictEqual(result.rejected, 'no_stamina');
});

test('startCast — no_mana rejection for mage', () => {
  // Equipa fire_staff → skill_fire_bolt (mana 15). Com mana=0 deve rejeitar.
  const { pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c5', { name: 'NoMana' });
  pm.equipItem(caster, 'weapon', 'fire_staff');  // weapon_Q = skill_fire_bolt
  caster.mana = 0;
  const result = ce.startCast('c5', 'skill_fire_bolt', 0, 0);
  assert.strictEqual(result.rejected, 'no_mana');
});

test('startCast — already_casting rejection', () => {
  const { pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c6', { name: 'Busy' });
  caster.stamina = 1000;
  ce.startCast('c6', 'skill_heavy_blow', 0, 0); // inicia cast
  const result2 = ce.startCast('c6', 'skill_heavy_blow', 0, 0);
  assert.strictEqual(result2.rejected, 'already_casting');
});

test('startCast — dead player rejected', () => {
  const { pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c7', { name: 'Dead' });
  caster.dead = true;
  const result = ce.startCast('c7', 'skill_slash', 0, 0);
  assert.strictEqual(result.rejected, 'dead');
});

test('startCast — mana consumed at cast start', () => {
  // fire_staff → skill_fire_bolt tem castTime > 0; mana é consumida no início do cast
  const { pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c8', { name: 'ManaCost' });
  pm.equipItem(caster, 'weapon', 'fire_staff');  // weapon_Q = skill_fire_bolt
  const before = caster.mana;
  ce.startCast('c8', 'skill_fire_bolt', 0, 0);
  assert.ok(caster.mana < before, 'mana deve ser consumida no início do cast');
});

test('applyDamage — reduces hp', () => {
  const { pm, ce } = makeCombatSetup();
  const target = pm.createPlayer('t1', { name: 'Target' });
  const hpBefore = target.hp;
  ce.applyDamage(target, 20);
  assert.ok(target.hp < hpBefore, `hp deve diminuir de ${hpBefore}`);
});

test('applyDamage — interruptible cast is cancelled', () => {
  // skill_heavy_blow: interruptible=true. Receber dano durante o cast cancela.
  const { pm, ce, world } = makeCombatSetup();
  const target = pm.createPlayer('t2', { name: 'Casting' });
  target.stamina = 1000;
  ce.startCast('t2', 'skill_heavy_blow', 0, 0);
  assert.ok(target.casting, 'deve estar em cast');
  ce.applyDamage(target, 5, null);
  assert.strictEqual(target.casting, null, 'cast deve ser interrompido');
  assert.ok(world.events.interrupts.length > 0, 'evento de interrupt deve ser emitido');
});

test('applyDamage — non-interruptible cast survives damage', () => {
  // Seta casting manualmente com skill sem interruptible=true.
  // skill_shield_bash (castTime=0, sem interruptible) → cast sobrevive ao dano.
  const { pm, ce } = makeCombatSetup();
  const target = pm.createPlayer('t3', { name: 'Tanky' });
  target.casting = { skillId: 'skill_shield_bash', endsAt: Date.now() + 5000, total: 0, tx: 0, ty: 0 };
  ce.applyDamage(target, 5, null);
  assert.ok(target.casting !== null, 'cast não interrompível deve sobreviver ao dano');
});

test('applyDamage — death sets dead=true and emits event', () => {
  const { pm, ce, world } = makeCombatSetup();
  const target = pm.createPlayer('t4', { name: 'Dying' });
  target.hp = 1;
  ce.applyDamage(target, 999);
  assert.ok(target.dead, 'target deve estar morto');
  assert.strictEqual(target.hp, 0);
  assert.ok(world.events.deaths.length > 0, 'evento de morte deve ser emitido');
});

test('applyDamage — dead player ignores further damage', () => {
  const { pm, ce } = makeCombatSetup();
  const target = pm.createPlayer('t5', { name: 'AlreadyDead' });
  target.hp = 0; target.dead = true;
  ce.applyDamage(target, 999);
  assert.strictEqual(target.hp, 0, 'hp não deve cair abaixo de 0 novamente');
});

test('resolveDueCasts — fires ability after castTime', () => {
  // skill_heavy_blow: tem castTime > 0. Após o cast terminar, deve aplicar dano.
  const { pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('rc1', { name: 'Caster' });
  const victim  = pm.createPlayer('rc2', { name: 'Victim' });
  caster.x = 100; caster.y = 100;
  victim.x  = 120; victim.y = 100; // dentro do melee range (65px)
  caster.stamina = 1000;

  ce.startCast('rc1', 'skill_heavy_blow', victim.x, victim.y);
  assert.ok(caster.casting, 'deve estar em cast');

  // Força o cast a estar vencido
  caster.casting.endsAt = Date.now() - 1;
  const hpBefore = victim.hp;
  ce.resolveDueCasts(Date.now());

  assert.strictEqual(caster.casting, null, 'casting deve ser null após resolve');
  assert.ok(victim.hp < hpBefore,
    `hp da vítima deve diminuir; antes=${hpBefore} depois=${victim.hp}`);
});

test('getSkillCatalog — returns non-empty object', () => {
  const { ce } = makeCombatSetup();
  const catalog = ce.getSkillCatalog();
  assert.ok(typeof catalog === 'object');
  assert.ok(Object.keys(catalog).length > 0, 'catálogo deve ter skills');
});

// ============================================================
// MonsterManager
// ============================================================
section('MonsterManager');

function makeMonsterSetup() {
  const world = new MockWorld();
  const pm    = new PlayerManager(world);
  const ce    = new CombatEngine(world, pm);
  const mm    = new MonsterManager(world, ce);
  return { world, pm, ce, mm };
}

test('spawn — creates monster in world', () => {
  const { world, mm } = makeMonsterSetup();
  const m = mm.spawn('goblin', { x: 200, y: 200 });
  assert.ok(m, 'spawn deve retornar o estado do monstro');
  assert.strictEqual(m.type, 'goblin');
  assert.ok(m.hp > 0);
  assert.ok(world.monsters.has(m.id));
});

test('spawn — all 5 monster types spawn', () => {
  const { world, mm } = makeMonsterSetup();
  ['goblin','orc','skeleton','wolf','troll'].forEach(type => {
    const m = mm.spawn(type, { x: 100, y: 100 });
    assert.ok(m, `${type} deve spawnar`);
    assert.ok(m.hp > 0);
  });
});

test('spawn — unknown type returns null', () => {
  const { mm } = makeMonsterSetup();
  const result = mm.spawn('dragon', { x: 0, y: 0 });
  assert.strictEqual(result, null);
});

test('spawnRandom — creates a monster', () => {
  const { world, mm } = makeMonsterSetup();
  const m = mm.spawnRandom();
  assert.ok(m, 'spawnRandom deve retornar um monstro');
  assert.ok(world.monsters.has(m.id));
});

test('aiTick — monster aggros nearby player', () => {
  const { pm, mm } = makeMonsterSetup();
  const player = pm.createPlayer('p1', { name: 'Target' });
  player.x = 150; player.y = 150;

  const monster = mm.spawn('goblin', { x: 160, y: 160 }); // dentro de AGGRO_RANGE (200)
  assert.strictEqual(monster.state, 'idle');

  mm._aiTimer = 1000; // força expirar o timer de IA
  mm.aiTick(Date.now());

  assert.strictEqual(monster.state, 'aggro',
    `monstro deve estar em aggro, ficou ${monster.state}`);
  assert.strictEqual(monster.target, 'p1');
});

test('aiTick — monster removes self when hp <= 0', () => {
  const { world, mm } = makeMonsterSetup();
  const m = mm.spawn('goblin', { x: 100, y: 100 });
  m.hp = 0;
  mm._aiTimer = 1000;
  mm.aiTick(Date.now());
  assert.ok(!world.monsters.has(m.id), 'monstro morto deve ser removido do mundo');
});

test('aiTick — monster returns to spawn when leashed', () => {
  const { pm, mm } = makeMonsterSetup();
  const player = pm.createPlayer('p2', { name: 'Far' });
  player.x = 100; player.y = 100;

  const monster = mm.spawn('goblin', { x: 100, y: 100 });
  monster.state  = 'aggro';
  monster.target = 'p2';
  // Move o monstro longe do spawn
  monster.x = monster.spawnX + 700; // > LEASH_RANGE (600)
  monster.y = monster.spawnY;

  mm._aiTimer = 1000;
  mm.aiTick(Date.now());

  assert.strictEqual(monster.state, 'returning',
    `monstro deve leash, ficou ${monster.state}`);
});

// ============================================================
// SUMMARY
// ============================================================
console.log('\n══════════════════════════════════════');
console.log(`  Resultado: ${passed} passou · ${failed} falhou`);
console.log('══════════════════════════════════════\n');
if (failed > 0) process.exit(1);
