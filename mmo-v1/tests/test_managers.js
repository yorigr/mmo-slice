// ============================================================
// PHASE 1 — TESTES UNITÁRIOS
// Usa apenas assert nativo do Node.js. Rodar com:
//   node tests/test_managers.js
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
    this.io       = {
      emit: () => {},
      to: () => ({ emit: () => {} }),
    };
  }
  addPlayer(s)    { this.players.set(s.id, s); }
  removePlayer(id){ this.players.delete(id); }
  getPlayer(id)   { return this.players.get(id); }
  addMonster(s)   { this.monsters.set(s.id, s); }
  removeMonster(id){ this.monsters.delete(id); }
  addItem(s)      { this.items.set(s.id, s); }
  removeItem(id)  { this.items.delete(id); }
  emitHit(h)      { this.events.hits.push(h); }
  emitInterrupt(d){ this.events.interrupts.push(d); }
  emitDeath(d)    { this.events.deaths.push(d); }
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
  const wm = new WorldManager({ emit: () => {} });
  wm.stop && wm.stop(); // não iniciar o setInterval

  // Use the mock instead (WorldManager depends on real io internally)
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

test('createPlayer — default class warrior', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('sock1', { name: 'Yuri', playerClass: 'warrior' });
  assert.strictEqual(p.id, 'sock1');
  assert.strictEqual(p.name, 'Yuri');
  assert.strictEqual(p.class, 'warrior');
  assert.ok(p.hp > 0);
  assert.ok(p.maxHp > 0);
  assert.ok(p.hp === p.maxHp);
  assert.ok(world.getPlayer('sock1'));
});

test('createPlayer — mage has higher mana', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const warrior = pm.createPlayer('w', { name: 'W', playerClass: 'warrior' });
  const mage    = pm.createPlayer('m', { name: 'M', playerClass: 'mage' });
  assert.ok(mage.maxMana > warrior.maxMana, `mage mana ${mage.maxMana} should > warrior mana ${warrior.maxMana}`);
});

test('createPlayer — all 5 classes spawn without error', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  ['warrior','mage','ranger','healer','bruiser'].forEach((cls, i) => {
    const p = pm.createPlayer('id-' + i, { name: cls, playerClass: cls });
    assert.ok(p.hp > 0);
  });
});

test('handleMove — formal movement accepted', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s2', { name: 'Mover', playerClass: 'warrior' });
  p.x = 100; p.y = 100;
  const now = Date.now();
  p.lastSeenAt = now - 500; // 500ms ago → allowed to move 200*0.5*1.5+5 = 155px
  pm.handleMove('s2', { x: 140, y: 140 }, now);
  assert.ok(Math.hypot(p.x - 140, p.y - 140) < 1, `player should reach (140,140), got (${p.x},${p.y})`);
});

test('handleMove — anti-speedhack: large teleport rejected', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s3', { name: 'Hacker', playerClass: 'warrior' });
  p.x = 100; p.y = 100;
  const now = Date.now();
  p.lastSeenAt = now - 16; // 16ms → max ~200*0.016*1.5+5 ≈ 9.8px
  pm.handleMove('s3', { x: 800, y: 800 }, now);
  assert.ok(Math.hypot(p.x - 100, p.y - 100) < 20, `teleport should be rejected, moved ${Math.hypot(p.x-100, p.y-100).toFixed(1)}px`);
  assert.ok(p.rejectedMoves > 0, 'rejectedMoves should be incremented');
});

test('handleMove — dead player cannot move', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s4', { name: 'Dead', playerClass: 'warrior' });
  p.x = 100; p.y = 100; p.dead = true;
  pm.handleMove('s4', { x: 500, y: 500 }, Date.now());
  assert.strictEqual(p.x, 100);
  assert.strictEqual(p.y, 100);
});

test('regenTick — mana and stamina regenerate', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s5', { name: 'Regen', playerClass: 'mage' });
  p.mana    = 10;
  p.stamina = 10;
  pm.regenTick(1.0); // 1 second
  assert.ok(p.mana    > 10, `mana should regen, got ${p.mana}`);
  assert.ok(p.stamina > 10, `stamina should regen, got ${p.stamina}`);
});

test('regenTick — dead player does not regen', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s6', { name: 'DeadRegen', playerClass: 'warrior' });
  p.mana = 5; p.dead = true;
  pm.regenTick(5.0);
  assert.strictEqual(p.mana, 5, 'dead player mana should not change');
});

test('regenTick — does not exceed maxMana', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  const p  = pm.createPlayer('s7', { name: 'Cap', playerClass: 'warrior' });
  p.mana = p.maxMana - 1;
  pm.regenTick(100.0); // big tick
  assert.strictEqual(p.mana, p.maxMana, `mana should cap at ${p.maxMana}`);
});

test('removePlayer — removes from world', () => {
  const world = new MockWorld();
  const pm = new PlayerManager(world);
  pm.createPlayer('s8', { name: 'Leave', playerClass: 'warrior' });
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
  const { world, pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c1', { name: 'Cast', playerClass: 'warrior' });
  // Ensure enough stamina for warrior_slash
  caster.stamina = 100;
  const result = ce.startCast('c1', 'warrior_slash', 0, 0);
  assert.ok(result.resolved, `expected resolved, got ${JSON.stringify(result)}`);
});

test('startCast — cast-time skill returns casting', () => {
  const { world, pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c2', { name: 'Caster', playerClass: 'warrior' });
  caster.stamina = 100;
  const result = ce.startCast('c2', 'warrior_heavy_blow', 0, 0);
  assert.ok(result.casting, `expected casting=true, got ${JSON.stringify(result)}`);
  assert.ok(result.endsAt > Date.now(), 'endsAt should be in the future');
  assert.ok(caster.casting, 'player.casting should be set');
});

test('startCast — cooldown rejection', () => {
  const { world, pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c3', { name: 'CD', playerClass: 'warrior' });
  caster.stamina = 1000;
  ce.startCast('c3', 'warrior_slash', 0, 0); // uses cooldown
  const result2 = ce.startCast('c3', 'warrior_slash', 0, 0);
  assert.strictEqual(result2.rejected, 'cooldown');
});

test('startCast — no_stamina rejection', () => {
  const { world, pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c4', { name: 'NoSt', playerClass: 'warrior' });
  caster.stamina = 0; // drain stamina
  const result = ce.startCast('c4', 'warrior_slash', 0, 0);
  assert.strictEqual(result.rejected, 'no_stamina');
});

test('startCast — no_mana rejection for mage', () => {
  const { world, pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c5', { name: 'NoMana', playerClass: 'mage' });
  caster.mana = 0;
  const result = ce.startCast('c5', 'mage_frost_bolt', 0, 0);
  assert.strictEqual(result.rejected, 'no_mana');
});

test('startCast — already_casting rejection', () => {
  const { world, pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c6', { name: 'Busy', playerClass: 'warrior' });
  caster.stamina = 1000;
  ce.startCast('c6', 'warrior_heavy_blow', 0, 0); // starts cast
  const result2 = ce.startCast('c6', 'warrior_heavy_blow', 0, 0);
  assert.strictEqual(result2.rejected, 'already_casting');
});

test('startCast — dead player rejected', () => {
  const { world, pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c7', { name: 'Dead', playerClass: 'warrior' });
  caster.dead = true;
  const result = ce.startCast('c7', 'warrior_slash', 0, 0);
  assert.strictEqual(result.rejected, 'dead');
});

test('startCast — mana consumed at cast start', () => {
  const { world, pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('c8', { name: 'ManaCost', playerClass: 'mage' });
  const before = caster.mana;
  ce.startCast('c8', 'mage_frost_bolt', 0, 0);
  assert.ok(caster.mana < before, 'mana should be consumed at cast start');
});

test('applyDamage — reduces hp', () => {
  const { world, pm, ce } = makeCombatSetup();
  const target = pm.createPlayer('t1', { name: 'Target', playerClass: 'warrior' });
  const hpBefore = target.hp;
  ce.applyDamage(target, 20);
  assert.ok(target.hp < hpBefore, `hp should decrease from ${hpBefore}`);
});

test('applyDamage — interruptible cast is cancelled', () => {
  const { world, pm, ce } = makeCombatSetup();
  const target = pm.createPlayer('t2', { name: 'Casting', playerClass: 'warrior' });
  target.stamina = 1000;
  ce.startCast('t2', 'warrior_heavy_blow', 0, 0); // starts cast (interruptible)
  assert.ok(target.casting, 'should be casting');
  ce.applyDamage(target, 5, null);
  assert.strictEqual(target.casting, null, 'cast should be interrupted');
  assert.ok(world.events.interrupts.length > 0, 'interrupt event should be emitted');
});

test('applyDamage — non-interruptible cast survives damage', () => {
  const { world, pm, ce } = makeCombatSetup();
  const target = pm.createPlayer('t3', { name: 'Tanky', playerClass: 'warrior' });
  target.casting = { abilityId: 'warrior_shield_bash', endsAt: Date.now() + 5000, total: 0, tx: 0, ty: 0 };
  ce.applyDamage(target, 5, null);
  assert.ok(target.casting !== null, 'non-interruptible cast should survive');
});

test('applyDamage — death sets dead=true and emits event', () => {
  const { world, pm, ce } = makeCombatSetup();
  const target = pm.createPlayer('t4', { name: 'Dying', playerClass: 'warrior' });
  target.hp = 1;
  ce.applyDamage(target, 999);
  assert.ok(target.dead, 'target should be dead');
  assert.strictEqual(target.hp, 0);
  assert.ok(world.events.deaths.length > 0, 'death event should be emitted');
});

test('applyDamage — dead player ignores further damage', () => {
  const { world, pm, ce } = makeCombatSetup();
  const target = pm.createPlayer('t5', { name: 'AlreadyDead', playerClass: 'warrior' });
  target.hp = 0; target.dead = true;
  ce.applyDamage(target, 999);
  assert.strictEqual(target.hp, 0, 'hp should not go below 0 again');
});

test('resolveDueCasts — fires ability after castTime', () => {
  const { world, pm, ce } = makeCombatSetup();
  const caster = pm.createPlayer('rc1', { name: 'Caster', playerClass: 'warrior' });
  const victim = pm.createPlayer('rc2', { name: 'Victim', playerClass: 'warrior' });
  caster.x = 100; caster.y = 100;
  victim.x = 120; victim.y = 100;
  caster.stamina = 1000;

  ce.startCast('rc1', 'warrior_heavy_blow', victim.x, victim.y);
  assert.ok(caster.casting, 'should be casting');

  caster.casting.endsAt = Date.now() - 1;
  const hpBefore = victim.hp;
  ce.resolveDueCasts(Date.now());

  assert.strictEqual(caster.casting, null, 'casting should be null after resolve');
  assert.ok(victim.hp < hpBefore, `victim hp should decrease; before=${hpBefore} after=${victim.hp}`);
});

test('getSkillCatalog — returns non-empty object', () => {
  const { ce } = makeCombatSetup();
  const catalog = ce.getSkillCatalog();
  assert.ok(typeof catalog === 'object');
  assert.ok(Object.keys(catalog).length > 0, 'catalog should have skills');
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
  assert.ok(m, 'spawn should return monster state');
  assert.strictEqual(m.type, 'goblin');
  assert.ok(m.hp > 0);
  assert.ok(world.monsters.has(m.id));
});

test('spawn — all 5 monster types spawn', () => {
  const { world, mm } = makeMonsterSetup();
  ['goblin','orc','skeleton','wolf','troll'].forEach(type => {
    const m = mm.spawn(type, { x: 100, y: 100 });
    assert.ok(m, `${type} should spawn`);
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
  assert.ok(m, 'spawnRandom should return a monster');
  assert.ok(world.monsters.has(m.id));
});

test('aiTick — aggros nearby player', () => {
  const { world, pm, mm } = makeMonsterSetup();
  const player = pm.createPlayer('p1', { name: 'Target', playerClass: 'warrior' });
  player.x = 150; player.y = 150;

  const monster = mm.spawn('goblin', { x: 160, y: 160 });
  assert.strictEqual(monster.state, 'idle');

  mm._aiTimer = 1000;
  mm.aiTick(Date.now());

  assert.strictEqual(monster.state, 'aggro');
  assert.strictEqual(monster.target, 'p1');
});

test('aiTick — removes dead monster', () => {
  const { world, mm } = makeMonsterSetup();
  const m = mm.spawn('goblin', { x: 100, y: 100 });
  m.hp = 0;
  mm._aiTimer = 1000;
  mm.aiTick(Date.now());
  assert.ok(!world.monsters.has(m.id));
});

test('aiTick — monster returns to spawn when leashed', () => {
  const { world, pm, mm } = makeMonsterSetup();
  const player = pm.createPlayer('p2', { name: 'Far', playerClass: 'warrior' });
  player.x = 100; player.y = 100;

  const monster = mm.spawn('goblin', { x: 100, y: 100 });
  monster.state  = 'aggro';
  monster.target = 'p2';
  monster.x = monster.spawnX + 700;
  monster.y = monster.spawnY;

  mm._aiTimer = 1000;
  mm.aiTick(Date.now());

  assert.strictEqual(monster.state, 'returning');
});

console.log('\nM');
console.log(`Resultado: ${passed} passou | ${failed} falhou`);
console.log('M\n');
if (failed > 0) process.exit(1);
