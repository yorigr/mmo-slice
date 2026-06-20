# MMORPG — Índice de Navegação do Código

> Leia este arquivo primeiro. Ele mapeia onde tudo está para que você não precise abrir arquivos às cegas.

---

## Estrutura Raiz

```
MMORPG/
├── server/            ← Servidor ativo (Node.js + Socket.IO v4)
├── unity-client/      ← Cliente Unity 6 URP 3D isométrico
│   └── mmo-client/
├── _archive/          ← Referência histórica (protótipo antigo — não usar)
│   └── mmo-slice/     ← Node portátil aqui: node-v24.16.0-win-x64/node.exe
├── docs/              ← Documentação de design e arquitetura
├── iniciar-mmorpg.bat ← Inicia servidor (usa Node portátil)
├── restart-server.bat ← Mata e reinicia o servidor
└── push-github.bat    ← Envia tudo para github.com/yorigr/mmo-slice
```

---

## Servidor (`server/`)

### Entrada
| Arquivo | O que faz |
|---------|-----------|
| `src/server.js` | Ponto de entrada. Express + Socket.IO. Define todos os eventos. Leia aqui primeiro. |

### Managers (lógica de jogo)
| Arquivo | Responsabilidade |
|---------|-----------------|
| `src/managers/PlayerManager.js` | Cria players, valida movimento, colisões, level up, regen. |
| `src/managers/CombatEngine.js` | Resolve habilidades (melee, ranged, AoE, heal, buff, teleport). Dano, crit, dodge, shield, DoT. |
| `src/managers/MonsterManager.js` | Spawn, IA (aggro/leash), ataque, morte, loot. |
| `src/managers/WorldManager.js` | Estado do mundo: players, monstros, itens. Emite eventos globais. |
| `src/managers/ZoneManager.js` | Zonas isoladas (Socket.IO rooms). Game loop por zona. Base para dungeons. |
| `src/managers/SessionManager.js` | Salva estado por 30s ao desconectar. Restaura na reconexão. |

### Config (balanceamento — edite aqui, não no código)
| Arquivo | O que contém |
|---------|-------------|
| `src/config/constants.js` | **Fonte única de verdade.** Tick rate, HP, velocidade, XP, crit, economia, CC DR. |
| `src/config/skills.json` | Catálogo FLAT de skills (sem keying por classe). Indexado por skill_id. |
| `src/config/gear.json` | Famílias de armas e tipos de armadura. Define skill slots e opções por peça. |
| `src/config/items.json` | Catálogo de itens. Stats de equipamento, consumíveis, materiais. |

---

## Cliente Unity (`unity-client/mmo-client/Assets/Scripts/`)

| Script | Responsabilidade |
|--------|-----------------|
| `GameManager.cs` | Orquestrador. Persiste sessionToken. Cria SkillBar e ItemWorldController se não atribuídos. |
| `StickManBuilder.cs` | Cria stick man proceduralmente (sem assets). `Build(go, color)` + `ClassColor("warrior")`. |
| `Network/NetworkManager.cs` | WebSocket puro + Socket.IO v4. Sem dependências externas. |
| `Network/SocketIOParser.cs` | Decodifica Engine.IO v4. Não modificar sem entender o protocolo. |
| `Player/PlayerController.cs` | Input, envio de player:move, reconciliação de posição. |
| `Player/CameraController.cs` | Câmera isométrica. |
| `World/WorldState.cs` | Estado local (players, monstros, **itens**). Deserializa world:update. |
| `World/MonsterController.cs` | Representação visual dos monstros com barra de HP. |
| `World/ItemWorldController.cs` | Itens no chão do mundo. Tecla **E** para coletar o mais próximo. |
| `World/GroundSampler.cs` | Posicionamento correto dos objetos no terreno. |
| `UI/HUD.cs` | HP, mana, XP, gold, nível, ping. |
| `UI/SkillBar.cs` | Barra de skills (teclas **1–5**). Cria Canvas proceduralmente. Cooldown visual. |
| `UI/RespawnPanel.cs` | Tela de morte. Countdown 8s + botão auto-revive. `Show()` / `Hide()` via GameManager. |
| `UI/ChatUI.cs` | Chat global/zona. Enter=abrir/enviar, Tab=canal, Esc=cancelar. Fade automático. |

---

## Eventos Socket.IO

### Cliente → Servidor
| Evento | Payload | Descrição |
|--------|---------|-----------|
| `player:join` | `{ name, sessionToken?, zoneId? }` | Entra no jogo (sem classe — gear-based) |
| `player:move` | `{ x, y }` | Posição |
| `skill:use` | `{ skillId, tx, ty }` | Usa habilidade (skillId = id da skill, ex: `skill_slash`) |
| `gear:equip` | `{ slot, gearId }` | Equipa peça de gear (slot: weapon/chest/head/boots) |
| `skill:select` | `{ slotKey, skillId }` | Muda skill de um slot (ex: slotKey=`weapon_Q`) |
| `repair:item` | `{ slot }` | Repara peça no Ferreiro. `slot` = 'weapon'|'chest'|'head'|'boots'|'all' |
| `mastery:convert_yellow_fame` | `{ gearId }` | Converte Fama Amarela pendente em nível permanente. Requer proximidade ao Instrutor NPC e ouro suficiente. |
| `item:pickup` | `{ itemId }` | Pega item do chão |
| `chat:send` | `{ channel, message }` | Mensagem (global ou zone) |
| `zone:change` | `{ zoneId }` | Troca de zona |
| `ping_rtt` | `timestamp` | Latência |

### Servidor → Cliente
| Evento | Descrição |
|--------|-----------|
| `player:joined` | Confirmação. Campos: `id, sessionToken, world:{w,h}, abilities, npcs:[{id,type,name,x,y}], state`. `npcs` inclui Ferreiro Aldric e Instrutor Magnus. |
| `world:update` | Snapshot a 20Hz — players, monsters, **items** |
| `combat:hits` | Array de hits por tick `[{from,to,damage,crit,hp}]` |
| `combat:deaths` | Array de mortes por tick `[{id,killerId}]` |
| `combat:interrupts` | Casts interrompidos por tick |
| `player:levelup` | `{level, maxHp, maxMana, speed, xp, xpMax}` |
| `player:xp` | `{xp, gold, totalXp, totalGold, xpMax}` |
| `player:revived` | `{hp, x, y}` — ressuscitado (auto ou por aliado) |
| `player:death_loot` | `{destroyed:[{slot,gearId}], dropped:[{slot,gearId,itemId}], kept:[{slot,gearId}]}` — itens destruídos/dropados na morte |
| `repair:result` | `{ok,totalCost,repairs:[{slot,gearId,fromDurability,cost}],gold}` ou `{error}` — resultado do reparo |
| `skill:result` | `{skillId, resolved?} | {skillId, rejected:'reason'}` |
| `status:applied` | `{type, endsAt}` — status effect aplicado no player local |
| `gear:equipped` | `{slot, gearId, abilities, ok?}` — confirmação de equipamento |
| `skill:select_result` | `{slotKey, skillId, abilities, ok?}` — confirmação de seleção |
| `mastery:xp` | `{gearId, xp, level, xpMax, yellowFame:{pending,level}}` — XP de maestria recebido |
| `mastery:levelup` | `{gearId, level, xpMax}` — level up de maestria de equipamento |
| `mastery:yellow_fame` | `{gearId, pending}` — XP excedente convertido em Fama Amarela pendente (maestria maxed) |
| `mastery:convert_result` | `{ok, gearId, yellowFameLevel, gold, goldSpent, pending, nextXpNeeded, nextGoldCost}` ou `{error}` — resultado da conversão de Fama Amarela |
| `item:picked` | Item coletado com sucesso `{item:{id,type,...}}` |
| `chat:message` | `{channel, from, message, ts}` |
| `pong_rtt` | Resposta de latência (timestamp espelhado) |

---

## Fluxo: maestria de equipamento

```
Armas — XP por skill resolvida (MASTERY_XP_PER_USE = 10):
  CombatEngine._resolveAbility() → MASTERY_SKILL_TYPES.has(sk.type)
    → PlayerManager.gainMasteryXp(player, weaponId, 10)

Armaduras — XP por hit absorvido (MASTERY_XP_PER_HIT = 3):
  CombatEngine.applyDamage() → peça aleatória perde 1 dur
    → PlayerManager.gainMasteryXp(player, armorId, 3)

Bônus por nível (aplicado via _recalcStats):
  Arma:    +2% dano por nível (+5% por nível de Fama Amarela)
  Cloth:   +2 maxMana por nível (+3 por Fama Amarela)
  Leather: +0.3% dodge por nível (+0.5% por Fama Amarela)
  Plate:   +0.2% damageReduction por nível (+0.3% por Fama Amarela)

Fama Amarela (pós-maestria máxima nível 10):
  XP excedente → yellowFame.pending
  Conversão: player próximo ao Instrutor Magnus (TRAINER_RANGE = 120px)
    → mastery:convert_yellow_fame { gearId }
    → Valida: pending >= YELLOW_FAME_XP_TABLE[level], gold >= YELLOW_FAME_GOLD_TABLE[level]
    → Debita ouro, incrementa yellowFame.level (máx 5)
    → Total gasto por peça: 41.000 gold (sink progressivo)
```

## Fluxo: skill ponta a ponta

```
PlayerController.cs → skill:use
  → CombatEngine.startCast()
    → castTime=0: _resolveAbility() imediato
    → castTime>0: p.casting salvo; resolvido no próximo tick
      → applyDamage() / heal / buff / teleport
        → WorldManager.emitHit() → combat:hit → HUD.cs
```

## Fluxo: morte → XP → level up

```
CombatEngine → monster.hp <= 0
  → MonsterManager.onMonsterDeath()
    → player.xp += xpReward
    → PlayerManager.checkLevelUp()
      → player:levelup emitido
    → rola loot table → world.addItem()
      → incluso no próximo world:state
```

---

## Fluxo: durabilidade

```
Por hit recebido (CombatEngine.applyDamage):
  → peça de armadura aleatória perde DURABILITY_LOSS_PER_HIT (1 ponto)
  → item com dur=0: não fornece stats nem skills

Por morte (PlayerManager.handlePlayerDeath):
  → itens destruídos/dropados → slot.durability = 0
  → itens mantidos (kept) → durability[slot] -= DURABILITY_DEATH_PENALTY (30 pts)

Reparo no Ferreiro (repair:item):
  → player dentro de BLACKSMITH_RANGE (120px) do Ferreiro
  → custo = REPAIR_BASE_VALUES[gearId] × (1 - dur/100) × REPAIR_COST_RATE (0.15)
  → gold debitado, durability restaurada a 100
```

## Fluxo: morte de player → destruição de itens

```
CombatEngine.applyDamage() → target.hp <= 0
  → target.dead = true
  → PlayerManager.handlePlayerDeath(target, world.zoneType)
      → Para cada peça equipada:
          roll < taxa → destruído (slot = null, skills resetadas)
          canLoot     → dropado no mundo (world.addItem)
          else        → mantido (yellow zone)
      → Inventário: -10% de materiais
  → world.io.to(target.id).emit('player:death_loot', { destroyed, dropped, kept })
  → PlayerManager.scheduleRespawn(target)  → ressuscita em RESPAWN_MS (3s)
```

---

## Como balancear

1. HP/velocidade/regen → `constants.js`
2. Skills → `skills.json`
3. Monstros (HP, XP, loot) → `MONSTER_TYPES` em `MonsterManager.js`
4. Items → `items.json`
5. Curva XP → `xpNeededForLevel()` em `PlayerManager.js` (padrão: `100 * level^1.5`)
6. Maestria → `MASTERY_XP_TABLE`, `MASTERY_*_PER_LEVEL` em `constants.js`
7. Fama Amarela → `YELLOW_FAME_XP_TABLE`, `YELLOW_FAME_GOLD_TABLE` em `constants.js`

---

## Documentos de design (`docs/`)

| Arquivo | Conteúdo |
|---------|----------|
| `MMORPG_GDD.md` | Game Design Document (mecânicas, mundo) |
| `SERVER_ARCHITECTURE.md` | Arquitetura técnica |
| `SKILL_DEFINITIONS.md` | Design das skills por gear type |
| `DEVELOPMENT_ROADMAP.md` | Fases e timeline |
| `GAME_CONFIG.md` | Guia de balanceamento |
| `SCENE_SETUP.md` | Setup de cena Unity |
| `PHASE2_UNITY.md` | Plano do cliente Unity |
| `COMBAT_AND_PROGRESSION_REFERENCE.md` | Estudo de Albion (CC, GvG, Status Effects) |
| `ECONOMY_AND_GEAR_REFERENCE.md` | Gear-based sandbox + anti-inflação (pesquisa EVE/Albion) |
