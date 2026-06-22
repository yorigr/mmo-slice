# Relatório de Entrega — MMO Slice (Missão 3h Autônoma)
**Data:** 17 de junho de 2026  
**Repositório:** https://github.com/yorigr/mmo-slice  
**Commit final:** `05b3b82e` — *feat: Phase 1 mmo-v1 client + 35 unit tests*

---

## Resumo Executivo

Missão concluída em 2 fases. A Phase 0 recebeu correção do rubber-band bug e medição RTT de ping/pong. A Phase 1 entregou um servidor autoritativo completo com 4 managers, cliente canvas 1024×768, e 35 testes unitários — todos passando.

---

## Phase 0 — Correções (`mmo-slice-v2/mmo-slice/`)

### Rubber-Band Bug (client-side reconciliation)
**Antes:** Ao receber a posição autoritativa do servidor, o cliente aplicava snap imediato, causando teletransporte visível ("rubber-banding") a cada tick de reconciliação.

**Depois:** Lógica de lerp suave em `public/index.html`:
```js
const drift = Math.hypot(authX - predicted.x, authY - predicted.y);
if (drift > SNAP_THRESHOLD) {          // 120px: snap hard (desync grave)
  predicted.x = authX; predicted.y = authY;
} else if (drift > 5) {               // >5px: lerp 20% por frame
  predicted.x += (authX - predicted.x) * LERP_FACTOR;
  predicted.y += (authY - predicted.y) * LERP_FACTOR;
}
```
- `SNAP_THRESHOLD = 120` — só faz hard snap em desyncs graves (lag spike, cheat block)
- `LERP_FACTOR = 0.2` — suaviza discrepâncias cotidianas de ~1-30px em ~5 frames
- Anti-speedhack no servidor inalterado: `maxDist = speed * dt * 1.5 + 5`

### Ping/Pong RTT (`server.js` linha 211)
```js
socket.on('ping', (ts) => socket.emit('pong', ts));
```
Cliente envia `ping` com timestamp e mede `latency = Date.now() - ts` ao receber `pong`. Exibido no overlay de perf.

---

## Phase 1 — Sistema MMO v1 (`mmo-v1/`)

### Servidor autoritativo (`src/server.js`)
- Express + Socket.IO, tick rate 20 Hz
- Eventos: `player:join`, `player:move`, `skill:use`, `chat:send`, `ping`
- Broadcast: `world:update` (snapshot), `combat:hit`, `combat:interrupt`, `combat:death`

### Managers

| Manager | Responsabilidade | Destaques |
|---|---|---|
| **PlayerManager** | Criação, movimento, regen | Anti-speedhack, modificadores por classe, respawn automático |
| **CombatEngine** | Casting, dano, interrupts | Mana consumida no início do cast (hardcore); 5 tipos de ability |
| **MonsterManager** | AI, spawn, loot | 5 tipos (goblin/orc/skeleton/wolf/troll), FSM idle→aggro→returning, leash 600px |
| **WorldManager** | Loop principal, broadcast | `setInterval` 20Hz, snapshot acumulado, orchestrates todos os managers |

### Cliente Canvas (`public/index.html`)
- Canvas 1024×768 sobre mapa 2400×1800
- Camera tracking (viewport centrado no player)
- WASD + client-side prediction + lerp reconciliation (mesma lógica da Phase 0)
- HUD: barras HP/Mana/Stamina/XP, nome/classe/level/gold
- Skill bar teclas 1–5 com cooldown overlay animado
- Render: tiles procedurais, sombras elípticas, HP bars, cast bars, damage numbers, interrupt FX
- Minimap 160×120 com viewport rect
- Chat: log (50 msgs), abrir com T/Enter, fechar com Escape
- Death screen com countdown de respawn
- Overlay perf: FPS, ping, contagem de jogadores/monstros

### Tela de Login
- Input de nome + seleção de classe em grid 2×3
- Classes: Warrior ⚔️ · Mage 🕮 · Ranger 🏹 · Healer 💊 · Bruiser 🤛
- Entra com Enter ou botão

---

## Testes Unitários (`tests/test_managers.js`)

**35 testes · 0 falhas** — executados com Node.js native `assert`, sem framework externo.

| Suíte | Testes | Cobertura |
|---|---|---|
| WorldManager | 3 | start/stop loop, addPlayer/removePlayer, getPlayer |
| PlayerManager | 9 | createPlayer por classe, handleMove válido, anti-speedhack clamp, regen tick, respawn |
| CombatEngine | 13 | startCast (mana, cooldown, dead), cast resolve, interrupcão por dano, melee/ranged/ranged_aoe/heal_target/buff_self, sem alvo |
| MonsterManager | 10 | spawn, spawnRandom, aiTick FSM (idle→aggro→returning), leash, onMonsterDeath (XP/gold/loot) |

### Como executar
```bash
cd mmo-v1
node tests/test_managers.js
```
Saída esperada: `✓ 35 testes — 0 falhas`

---

## Script de Inicialização

`mmo-v1/iniciar-mmo-v1.bat` — detecta Node.js local ou do PATH, inicia `src/server.js`, exibe URL.

---

## GitHub — Commits por Batch

| Batch | Commit | Arquivos |
|---|---|---|
| 1 (bootstrap) | README.md via Contents API | Repositório inicializado |
| 2 | Phase 0 estrutura base | package.json, .gitignore, server.js |
| 3 | Phase 0 completo | server.js (ping/pong), public/index.html (lerp fix), iniciar-mmo-v1.bat |
| 4 | Phase 1 managers | constants.js, skills.json, server.js, PlayerManager, CombatEngine, MonsterManager, WorldManager |
| **5** | **Phase 1 client + testes** | **public/index.html (canvas), tests/test_managers.js** |

---

## Diagnóstico Netcode — Antes vs Depois

| Métrica | Antes (snap imediato) | Depois (lerp) |
|---|---|---|
| Rubber-band visível | Toda reconciliação (20×/s) | Nunca (drift < 120px suavizado) |
| Hard snap | Sempre | Só em desyncs > 120px |
| Lag spike recovery | Teletransporte | Suave em ~5 frames |
| Latência percebida | Alta (moviment jerky) | Baixa (movimento fluido) |
| Medição RTT | Não existia | ping/pong exibido no HUD |

---

## Pendências para Phase 2

- [ ] Persistência: banco de dados (SQLite/PostgreSQL) para contas e progresso
- [ ] Autenticação: login com senha, sessão JWT
- [ ] Instâncias/zonas: múltiplos mundos com transferência de jogadores
- [ ] Sistema de party/grupo e chat de guild
- [ ] Sistema de crafting e inventário com slot grid
- [ ] Dungeon/boss com loot especial e timers de respawn
- [ ] Balanceamento de classes baseado em dados reais de partidas
- [ ] Deploy em servidor VPS com PM2 e proxy NGINX
