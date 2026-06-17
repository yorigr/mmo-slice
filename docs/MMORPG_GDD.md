# 🎮 MMORPG Medieval - Game Design Document (GDD)

**Versão:** 0.1 MVP  
**Data:** 2026  
**Status:** Em Design

---

## 📌 VISÃO GERAL

Um MMORPG multiplayer skill-based com temática medieval low-tech, inspirado em RuneScape, Albion Online e Tibia. Foco em PvP competitivo, economia dinâmica (crafting/gathering) e progressão baseada no equipamento utilizado.

### Pilares de Design
1. **Skill-Based**: Você é o que você usa (equipment = progression)
2. **PvP Zones**: Open-world com zonas vermelhas (PvP hardcore)
3. **Economia Viva**: Crafting, gathering, farming com anti-RMT
4. **Medieval Low-Tech**: Castelos, tavernas, mas nada de futurismo

---

## 🎯 ESCOPO MVP (Protótipo Jogável)

### O que entra no MVP:
- ✅ 5 classes (Warrior, Mage, Ranger, Healer, Bruiser)
- ✅ Mundo isométrico 2D com 10+ players simultâneos
- ✅ Sistema de combate com hotbar + cooldown + mana
- ✅ Equipamento básico (armas, armadura, acessórios)
- ✅ 2-3 monstros para farm
- ✅ 1 dungeon simples (5 players)
- ✅ Sistema de guilds (criar, entrar, membros)
- ✅ Chat local + whisper

### O que sai do MVP:
- ❌ Crafting complexo
- ❌ Gathering (mining, woodcutting, etc)
- ❌ Quests de NPC
- ❌ Raids de 20+ players
- ❌ Sistema de casas/player housing
- ❌ Trading entre players (v0.2)

---

## 👥 CLASSES & BUILDS

### 1. **WARRIOR** (Tank/Melee)
- **Rol:** Tanque na frente absorvendo dano
- **Armas:** Espada + Escudo, Machado 2H
- **Skills:**
  - `Slash` (instant) - ataque básico
  - `Shield Bash` (0.5s cast) - stun inimigo, pode ser cancelado
  - `Defensive Stance` (toggle) - +50% armor, -30% dano
  - `Charge` (1s cast) - move pra inimigo, aplica slow
- **Recursos:** Mana (100) + Cooldowns

### 2. **MAGE** (Ranged Damage)
- **Rol:** Dano à distância alto, frágil
- **Armas:** Bastão, Orbe Mágica
- **Skills:**
  - `Fireball` (2s cast) - AOE 3x3, pode ser cancelado no casting
  - `Frost Bolt` (1.5s cast) - single target + slow
  - `Teleport` (0.5s cast) - escape, cooldown 10s
  - `Mana Shield` (toggle) - converte mana em escudo
- **Recursos:** Mana (150, regenera 20/s) + Cooldowns

### 3. **RANGER** (Ranged Damage/Kiting)
- **Rol:** Dano leve mas rápido, mobility
- **Armas:** Arco, Besta
- **Skills:**
  - `Shot` (1s cast) - ataque rápido
  - `Multi Shot` (1.5s cast) - 3 flechas em cone
  - `Evasion` (passive) - 20% chance de dodge
  - `Sprint` (toggle) - +50% speed, drena stamina lentamente
- **Recursos:** Stamina (80, regenera 30/s) + Cooldowns

### 4. **HEALER** (Support/Sustain)
- **Rol:** Cura aliados, suporte em grupo
- **Armas:** Cajado, Maca Sagrada
- **Skills:**
  - `Heal` (1.5s cast) - cura single target
  - `Holy Nova` (2s cast) - AOE heal em 4x4
  - `Resurrect` (3s cast) - revive aliado caído (só fora de combate)
  - `Divine Shield` (0.3s cast) - torna target imune por 2s
- **Recursos:** Mana (120, regenera 25/s) + Cooldowns

### 5. **BRUISER** (Hybrid Tank/Damage)
- **Rol:** Dano + Defesa balanceado
- **Armas:** Machado 1H + Escudo, Clava 2H
- **Skills:**
  - `Smash` (0.8s cast) - dano + knockback
  - `Riposte` (passive) - 15% chance counter-ataque
  - `Fortitude` (toggle) - +30% armor, -20% dano
  - `Whirlwind` (1.5s cast) - ataca tudo em volta
- **Recursos:** Mana (100) + Cooldowns

---

## ⚔️ SISTEMA DE COMBATE

### Sequência de Ataque
```
T0: Player clica em skill
  → Cliente mostra animação de casting
  → Valor de casting time baseado na skill

T(cast_time): Servidor recebe comando
  → Valida: mana? cooldown? inimigo em range? 
  → Calcula dano (equipment + bonificadores)
  → Aplica efeito (stun, slow, heal, etc)

T(cast_time + 50ms): Todos recebem atualização
  → Inimigo vê dano, animação
  → Cooldown visível na hotbar
```

### Casting Time & Cancelamento
- **Melee (Warrior, Bruiser):** 0.3-1s casting
  - Pode ser **cancelado** se receber dano durante casting
  - Reduz o "spam" de melee vs ranged
  
- **Ranged (Ranger, Mage):** 1-2.5s casting
  - Mais longo, mas dano maior
  - Pode ser **knockback/interrompido** por inimigos

- **Healer:** 1.5-3s casting
  - Skills de cura podem ser **interrompidas**

### Defesa Ativa
- **Dodge:** Chance de evitar dano
  - Ranger: 20% passivo
  - Todos: podem usar skill de dodge (ativa iframe por 0.5s)
  
- **Block:** Reduz dano em 50%
  - Warrior com escudo: sempre pode bloquear (toggle)
  - Bruiser: pode usar skill de block

- **Armor:** Reduz % de dano
  - Warrior com Full Plate: 60% armor
  - Ranger com Leather: 20% armor

### Mana & Stamina
- **Mana (Warrior, Bruiser, Healer, Mage):**
  - Regenera continuamente (passivo)
  - Custo de skills varia (20-60 mana)
  - Mana Shield (Mage): converte 1 mana = 0.5 armor temp

- **Stamina (Ranger):**
  - Regenera rápido quando não em combate
  - Sprint drena (reduz mobility sem stamina)

- **Cooldown:** Cada skill tem timer próprio
  - Hotbar mostra cooldown visual
  - Não pode usar skill durante cooldown

---

## 🗺️ MUNDO & ZONAS

### Mapa Isométrico
- **Tamanho:** 100x100 tiles (escalável)
- **Visão:** Câmera fixa isométrica (45° angle)
- **Players visíveis:** Todos em render distance (~20 tiles)

### Zonas de Segurança
1. **TOWN (Branca)**
   - Sem PvP
   - NPCs: Guard, Merchant, Banker, Guild Master
   - Safe bank + vendor
   - Respawn point (ao morrer, volta aqui)

2. **COUNTRYSIDE (Amarela)**
   - PvP ativado, risco moderado
   - Monstros fracos (rats, goblins)
   - Loots: ouro baixo, items comuns
   - Ideal pra novos players

3. **DARK FOREST (Vermelha)**
   - PvP hardcore, alto risco
   - Monstros fortes (trolls, wolves)
   - Loots: ouro alto, items raros
   - Estrutura de blocos para cover

---

## 💀 MORTE & LOOT (HARDCORE)

### O que acontece ao morrer em PvP:
1. **Character cai** (ragdoll por 3s, vulnerável)
2. **TUDO dropa** - equipamento, inventory, ouro
   - Itens dropam em tiles aleatórios próximos
   - Assassino tem **10s de exclusividade** no loot
   - Depois de 10s, qualquer um pode loitar
3. **Revive automático** no Town após 5s
   - Você **PERDE TUDO** que foi dropado
   - Nem GG, game just started again

### Risk/Reward Philosophy
- **Zona Branca (Safe):** Sem PvP, sem risco, farming lento
- **Zona Amarela (Yellow):** PvP ligado, risco moderado
- **Zona Vermelha (Red):** PvP hardcore - você PODE perder literalmente TUDO
  - Exemplo: Mataram você com full Mithril? Eles ficam com tudo
  - Exemplo: Tinha 5000 ouro? Tchau, virou do assassino

### Anti-Grief (Protegendo Iniciantes)
- **Noobs (nível < 5):** Só podem ser atacados por outros noobs em red zones
- **Mentoria:** Veteranos podem "mentorear" noobs (sem PvP entre eles)
- **Safe duels:** Você marca duelo no Town - pode perder sem morrer realmente

### Item Durability & Degradation
- Cada item tem durabilidade (100%)
- **Por morte:** -30% durability
- **Por hit recebido:** -0.5% durability
- **Ao quebrar:** Item não funciona mais, precisa repairar
- **Custo de reparo:** ~30% do valor do item

**Consequência:** Veteranos cuidadosos com gear, iniciantes podem ficar "geared naked"

---

## 📊 EQUIPAMENTO & PROGRESSÃO

### Raridades
- **Common** (branco): Madeira, couro básico
- **Uncommon** (verde): Bronze, ferro
- **Rare** (azul): Aço, couro forjado
- **Epic** (roxo): Mithril, couro elfo
- **Legendary** (ouro): Adamantite (raro de dropar)

### Equipamentos por Classe
**WARRIOR:**
- Helm: Iron Helm (+10 armor)
- Chest: Full Plate (+30 armor)
- Legs: Plate Legs (+15 armor)
- Weapon: Longsword (120 dano) + Shield (25 armor)
- Accessory: Ring of Defense (+5 armor)

**MAGE:**
- Helm: Wizard Hat (+5 mana regen)
- Chest: Mage Robe (+40 mana)
- Legs: Silk Pants (+20 mana)
- Weapon: Frost Staff (80 dano + 30% slow)
- Accessory: Mana Ring (+15 mana)

*[Pattern similar para Ranger, Healer, Bruiser]*

### Item Durability
- Cada item tem durabilidade (100%)
- Degradam com uso (-1% por morte, -0.5% por hit)
- Pode repairar no NPC (custa ouro)
- Item quebrado = sem efeitos

---

## 🧙 SISTEMA DE GUILDS

### Estrutura
- **Guild Master:** Criador, pode disband
- **Officer (2-3):** Gerencia membros, convites
- **Member:** Jogador comum
- **Rank:** Baseado em contribution points

### Features
- **Guild Bank:** Storage compartilhado (100 slots)
- **Guild Chat:** Canal separado do global
- **Guild Wars:** Declarar guerra a outras guilds (24h)
- **Leveling:** Guild ganha XP com kills, dropa melhor loot

### Anti-Grief
- Líderes podem kick
- Dissolver guild: requer 24h de inatividade ou votação

---

## 🏴‍☠️ DUNGEONS (MVP)

### Dungeon 1: "Goblin Caves" (5 players)
- **Dificuldade:** Easy
- **Boss:** Goblin King (200 HP)
  - Skills: Stab (instant, 30 dano), Roar (AOE slow)
  - Loot: Green Dagger, 50 ouro
- **Trash:** 5-6 Goblins (50 HP each)
- **Duração:** ~15 min
- **Cooldown:** 1h global (respa pra todos)

### Anti-Farm System
- **Energy:** Cada kill custa 10 energy
- **Regeneração:** 1 energy/min (max 100)
- **Raidlogging:** Afk > 5min = kick automático
- **Distribuição de loot:** Baseado em damage/healing

---

## 💬 CHAT & COMUNICAÇÃO

### Canais
- **Global:** Visível para todos (20 tile radius default, scalable)
- **Guild:** Só membros
- **Whisper:** 1v1 privado
- **System:** Notificações (level up, loot, etc)

### Anti-Spam
- Max 5 mensagens por 10 segundos
- Mute automático após violação (10 min)
- Admin pode banir

---

## 🎮 CONTROLES (MVP Web)

### Movimento
- **WASD ou Arrow Keys:** Movimento 8-direcional
- **Click:** Movimento do mouse também funciona
- **Espaço:** Dodge roll (se tem skill)

### Combate
- **1-5:** Hotbar skills
- **Q/E:** Trocar hotbar
- **Shift:** Sprint (se Ranger)
- **Right-click inimigo:** Auto-atacar

### Interface
- **I:** Inventory
- **B:** Bank
- **C:** Character sheet
- **G:** Guild menu
- **P:** Party invite

---

## 🔐 SEGURANÇA, ANTI-CHEESE & SKILL-BASED INTEGRITY

### Validação Server-Side (99% das decisions no servidor)
- ✅ Todos os danos calculados no servidor
- ✅ Posição validada a cada movimento
- ✅ Mana/stamina sincronizado
- ✅ Equipamento verificado antes de aplicar bônus
- ✅ Cooldowns forçados no servidor
- ✅ Casting time validado (não pode pular 1.5s cast em 0.1s)

### Anti-Exploit (Protegendo Skill-Based Integrity)
- **Speedhacking:** Detecta movimento impossível (> max speed)
- **Teleporting:** Valida caminho entre posições
- **Mana infinita:** Mana deletada no servidor se > max
- **Skill spam:** Cooldown forçado, client pode desync = servidor é verdade
- **Animation skipping:** Casting time é hardcoded no servidor
- **Wallhacking:** Collision detection validado no servidor

### Ban System (Progressivo)
1. **First exploit:** Warn + item rollback
2. **Second:** 24h ban
3. **Third+:** Permanent ban + items seized

### Anti-Bot & Raidlogging
- Cooldown mínimo 0.5s entre ações (anti-macro)
- Detecção de padrões repetitivos (29 kills de mesmo NPC = flagged)
- AFK > 5 min = kick automático
- Loot picking requer movimento/interação (não afk-farm)

### Anti-RMT Economics
- **Ouro limpa:** Boss drops têm diminishing returns
- **Energy system:** Limita grinding (10 energy/kill, regen 1/min)
- **Respawn timers:** Bosses não respawnam até X tempo passar
- **Loot diversity:** Não pode fazer Dungeon de novo em < 1 hora

### Skill-Based Philosophy
- **Nenhum build é OP:** Se descobrir combo quebradora, será nerfado
- **Counter-play sempre existe:** Rock-Paper-Scissors (e.g., Mage counters Warrior mas Ranger counters Mage)
- **Parece "glitch"? É feature:** Se players acham forma criativa de vencer = é válido
- **Comunidade balanceia:** Vítimas de exploit podem documentar e reportar

---

## 🎯 BUILD PHILOSOPHY & META-GAMING

### O Espírito Shangri-La Frontier

Em Shangri-La, **Akatsuki não segue a meta.** Ele experimenta, falha, aprende e cria builds que ninguém esperava. Vamos replicar isso:

### Builds Fixas por Classe (Mas Altamente Variáveis)

Cada classe tem 5 skills **FIXAS**, mas a ordem na hotbar, timing e combos são totalmente flexíveis:

**WARRIOR - 5 Skills Fixas:**
1. `Slash` (instant) - Basic attack
2. `Shield Bash` (0.5s cast) - Stun interruptível
3. `Defensive Stance` (toggle)
4. `Charge` (1s cast) - Mobility
5. `Riposte` (passive, chance-based)

Mas você pode:
- Spammar Slash em kiting (boring way)
- Charge → Bash → Slash combo (fast trade)
- Def Stance toggle antes de boss (tank)
- Bait inimigo em Charge knockback (creative)

**Resultado:** Mesmas skills, 100 formas diferentes de jogar.

### Counter-Play & Discovery

O jogo deve ser desenhado pra que:
- **Nenhum build é OP:** Se Mage está muito forte, Ranger kita melhor
- **Sempre há counter:** Se Bruiser está dominando, Healer sustaina e Ranger full kita
- **Descoberta > Spreadsheet:** Players descobrem combos ao vivo, não no teórica

### Exemplos de "Descoberta Legítima"

❌ **Exploit (Ban):**
- Teleportar fora do mapa
- Stackear mana infinita via bug
- Usar speedhack pra kitar sem tomar dano

✅ **Criatividade (Reward):**
- Mage descobre que pode Teleport + Fireball pra alcance impossível (legítimo, sem teleport pra fora do mapa)
- Ranger descobre que spammar Sprint + Shot é melhor que regular rotation
- Healer descobre que pre-heal antes da fight > reactive heal
- Tank descobre que Charge em parede teleporta inimigo pra alcance (edge case, legal!)

### Balancing Philosophy

Se uma build fica muito strong:
1. **Comunidade reporta** (screenshot/video)
2. **Devs analisam:** É exploit ou é skill?
3. **Se for skill:** Deixa, comunidade vai descobrir counter
4. **Se for exploit:** Fix & compensate players prejudicados

---

## 🎮 SKILL COMBOS & INTERACTIONS

Algumas skills podem interagir de formas criativas:

| Combo | Classes | Efeito |
|-------|---------|--------|
| Charge + Shield Bash | Warrior | Knockback + Stun (hard CC) |
| Fireball + Frost Bolt | Mage | Monstro on fire + slow (Dot + kite) |
| Sprint + Multi Shot | Ranger | 3 shots rápidas enquanto kita |
| Holy Nova + Heal | Healer | AOE heal + single target (expensive) |
| Smash + Whirlwind | Bruiser | AOE spam (mana expensive) |

**Mas:**
- Combos requerem **timing perfeito**
- Se inimigo cancela seu casting = combo quebra
- Mana/cooldown limita spam

---

### Backend
- **Runtime:** Node.js 18+
- **Framework:** Express.js
- **Real-time:** Socket.io (WebSocket)
- **Database:** MongoDB (flex) ou SQLite (simples)
- **Auth:** JWT tokens

### Frontend
- **Framework:** React 18+
- **Graphics:** Phaser 3 ou Canvas2D (isométrico)
- **State:** Redux ou Context API
- **Styling:** Tailwind CSS

### Deployment (v0.2)
- **Backend:** AWS EC2 / Heroku
- **Frontend:** Vercel / Netlify
- **Database:** MongoDB Atlas / AWS RDS

---

## 📈 ROADMAP PÓS-MVP

**v0.2:**
- Crafting system
- Gathering (mining, woodcutting, fishing)
- Quests de NPC
- Trading entre players
- 3 mais dungeons

**v0.3:**
- Raids (20+ players)
- Player housing
- Mounts
- Fishing/Cooking
- Leaderboards

**v1.0:**
- 20+ classes
- World bosses
- Battlegrounds (PvP arena)
- Seasonal events
- Mobile client (React Native)

---

## 🎨 REFERÊNCIAS VISUAIS & TONE

**PRIMARY:** Shangri-La Frontier (anime)
- **Visual:** Cel Shading (personagens estilo anime)
- **Tone:** Hardcore PvP, risk/reward alto, descoberta de builds criativas
- **Filosofia:** "Qualquer coisa pode ser explorada, qualquer tática é válida se for skill-based"

**SECONDARY:**
- **Albion Online:** Sistema de zonas, loot PvP hardcore
- **RuneScape (Classic):** Isométrica, progression
- **Tibia:** Open-world PvP, dungeon mechanics
- **AION:** Combat feel, skill system

### Inspirações Diretas de Shangri-La Frontier
1. **Akatsuki**: Build flex (não segue meta, improvisa combos únicos)
2. **Monstros em forma de "Glitch"**: Mechanics ocultas que rewarda players curiosos
3. **Risk/Reward absoluto**: Perder tudo na morte é possível e traumático
4. **Comunidade descobrindo builds**: Não deve haver "one true build", devem haver counter-plays

---

**Status:** Pronto para codificação! 🚀
