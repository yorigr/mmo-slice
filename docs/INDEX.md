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
| `src/config/constants.js` | **Fonte única de verdade.** Tick rate, HP, velocidade, XP, crit, classes. |
| `src/config/skills.json` | 25 skills (5 por classe). Tipo, cast time, cooldown, mana, dano, efeitos. |
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
| `player:join` | `{ name, playerClass, sessionToken?, zoneId? }` | Entra no jogo |
| `player:move` | `{ x, y }` | Posição |
| `skill:use` | `{ skillId, tx, ty }` | Usa habilidade |
| `item:pickup` | `{ itemId }` | Pega item do chão |
| `chat:send` | `{ channel, message }` | Mensagem (global ou zone) |
| `zone:change` | `{ zoneId }` | Troca de zona |
| `ping_rtt` | `timestamp` | Latência |

### Servidor → Cliente
| Evento | Descrição |
|--------|-----------|
| `player:joined` | Confirmação, abilities (array da classe), sessionToken, state |
| `world:update` | Snapshot a 20Hz — players, monsters, **items** |
| `combat:hits` | Array de hits por tick `[{from,to,damage,crit,hp}]` |
| `combat:deaths` | Array de mortes por tick `[{id,killerId}]` |
| `combat:interrupts` | Casts interrompidos por tick |
| `player:levelup` | `{level, maxHp, maxMana, speed, xp, xpMax}` |
| `player:xp` | `{xp, gold, totalXp, totalGold, xpMax}` |
| `player:revived` | `{hp}` — ressuscitado por outro jogador |
| `skill:result` | `{skillId, resolved?} | {skillId, rejected:'reason'}` |
| `item:picked` | Item coletado com sucesso `{item:{id,type,...}}` |
| `chat:message` | `{channel, from, message, ts}` |
| `pong_rtt` | Resposta de latência (timestamp espelhado) |

---

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

## Como balancear

1. HP/velocidade/regen → `constants.js`
2. Skills → `skills.json`
3. Monstros (HP, XP, loot) → `MONSTER_TYPES` em `MonsterManager.js`
4. Items → `items.json`
5. Curva XP → `xpNeededForLevel()` em `PlayerManager.js` (padrão: `100 * level^1.5`)

---

## Documentos de design (`docs/`)

| Arquivo | Conteúdo |
|---------|----------|
| `MMORPG_GDD.md` | Game Design Document (classes, mecânicas, mundo) |
| `SERVER_ARCHITECTURE.md` | Arquitetura técnica |
| `SKILL_DEFINITIONS.md` | Design das 25 skills |
| `DEVELOPMENT_ROADMAP.md` | Fases e timeline |
| `GAME_CONFIG.md` | Guia de balanceamento |
| `SCENE_SETUP.md` | Setup de cena Unity |
| `PHASE2_UNITY.md` | Plano do cliente Unity |
