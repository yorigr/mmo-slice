# Assets Gratuitos e Plano de UI — MMORPG

> Documento de referência de implementação para as 4 UIs principais do cliente Unity
> (Inventário, Paper Doll/Equipamento, Status do Personagem, Skill Tree/Seleção de Skills),
> além do catálogo de assets gratuitos pesquisados e licenças.
>
> Data: 2026-06-20
> Escopo: cliente Unity (`unity-client/mmo-client`) + servidor Node (`server/src`)
> Stack do servidor: Express + Socket.IO v4 + ZoneManager + SessionManager (gear-based, sem classes)

---

## 0. Resumo Executivo

O servidor já é **gear-based** (não há classes): as skills do jogador derivam do equipamento.
Cada jogador tem 5 slots de skill ativos — `weapon_Q`, `weapon_W`, `weapon_E` (da arma),
`chest_R` (do peitoral) e `head_D` (do elmo). Há um sistema de **Equipment Mastery**
inspirado no Albion Online (maestria individual por peça + Fama Amarela).

As 4 UIs precisam ler/escrever esse estado. Boa parte dos eventos Socket.IO **já existe**
no servidor (`gear:equip`, `skill:select`, `item:pickup`, `mastery:convert_yellow_fame`,
`repair:item`), mas faltam três eventos de backend para fechar o loop de inventário:
**`gear:unequip`**, **`item:use`** e **`item:drop`**.

Prioridade recomendada: **Paper Doll + Status** primeiro (dependem só de eventos existentes),
depois **Inventário** (precisa dos 3 eventos novos de backend) e por fim **Skill Tree**
(maior superfície de UI, depende de `skill:select` já existente).

---

## 1. Assets Gratuitos Encontrados

Todos os assets abaixo são adequados para uso comercial com atribuição mínima ou nenhuma.
**Sempre verificar a licença na página no momento do download** — licenças podem mudar.

### 1.1 Ícones de Itens / Skills / Inventário

| Pacote | Conteúdo | Formato | Licença | Link |
|---|---|---|---|---|
| **Game-icons.net** | ~4000+ ícones vetoriais (armas, armaduras, poções, skills, status) | SVG + PNG (qualquer tamanho) | CC BY 3.0 (atribuição) | https://game-icons.net |
| **Kenney — Board Game Icons / RPG** | Ícones UI, slots, molduras | PNG (transparente) | CC0 (domínio público) | https://kenney.nl/assets/board-game-icons |
| **Kenney — Game Icons** | Setas, botões, HUD | PNG | CC0 | https://kenney.nl/assets/game-icons |
| **OpenGameArt — 700+ RPG Icons (by Lorc/Delapouite)** | Mesma base do game-icons.net | PNG/SVG | CC BY 3.0 | https://opengameart.org/content/700-rpg-icons |
| **Itch.io — "Free RPG Item Icons" (variados)** | Pacotes de poções, gear, gemas | PNG 32x32 / 64x64 | CC0 ou CC BY (varia por autor) | https://itch.io/game-assets/free/tag-icons |

**Recomendação:** usar **game-icons.net** como base única — cobre praticamente todos os
itens/skills do projeto (espada, machado, adaga, maça, martelo, arco, cajados, capuzes,
peitorais, botas, poções). É vetorial, então escala bem para qualquer DPI. A licença CC BY 3.0
exige apenas creditar os autores (Lorc, Delapouite, etc.) em uma tela de créditos.

### 1.2 Molduras de Slot / Painéis / UI Kit

| Pacote | Conteúdo | Formato | Licença | Link |
|---|---|---|---|---|
| **Kenney — UI Pack (RPG Expansion)** | Painéis, slots, barras, botões com bordas estilo RPG | PNG + 9-slice | CC0 | https://kenney.nl/assets/ui-pack-rpg-expansion |
| **Kenney — UI Pack** | Botões, sliders, checkboxes genéricos | PNG | CC0 | https://kenney.nl/assets/ui-pack |
| **OpenGameArt — "RPG GUI construction kit" (by Lamoot)** | Frames, slots de inventário, barras HP/Mana | PNG | CC BY 3.0 | https://opengameart.org/content/rpg-gui-construction-kit-v10 |

**Recomendação:** **Kenney UI Pack RPG Expansion** (CC0, sem atribuição) para molduras de slot
e painéis. Já vem com sprites 9-slice prontos para Unity (escalam sem distorcer bordas).

### 1.3 Fontes

| Fonte | Estilo | Licença | Link |
|---|---|---|---|
| **Press Start 2P** | Pixel/retro | OFL (Open Font License) | https://fonts.google.com/specimen/Press+Start+2P |
| **Cinzel** | Serifada épica (títulos) | OFL | https://fonts.google.com/specimen/Cinzel |
| **Inter** | Sans-serif legível (corpo/números) | OFL | https://fonts.google.com/specimen/Inter |

**Recomendação:** **Cinzel** para títulos de painel + **Inter** para números/stats (alta
legibilidade em valores como HP/Mana/durabilidade). Gerar atlas TMP_FontAsset no Unity.

### 1.4 Barras de Recurso (HP/Mana/Stamina/XP)

Usar sprites simples 9-slice do Kenney UI Pack + Image com `Image Type = Filled (Horizontal)`
no Unity. Não requer asset externo dedicado. Cores sugeridas:
- HP: vermelho `#C0392B`
- Mana: azul `#2980B9`
- Stamina: amarelo/verde `#27AE60`
- XP: dourado `#F1C40F`
- Durabilidade: cinza→laranja→vermelho conforme % cai

### 1.5 Resumo de Licenças (para tela de Créditos)

- **CC0** (Kenney): nenhuma atribuição exigida. Preferir sempre que possível.
- **CC BY 3.0** (game-icons.net, OpenGameArt): exige crédito visível. Manter um arquivo
  `CREDITS.txt` + tela in-game listando: "Ícones por Lorc, Delapouite e contribuidores
  (game-icons.net) — CC BY 3.0".
- **OFL** (fontes): pode embutir e usar comercialmente; não vender a fonte isolada.

---

## 2. Estado do Servidor (campos reais)

Fonte: `server/src/managers/PlayerManager.js` (`createPlayer`) e `server/src/server.js`.
Estes são os campos REAIS do `state` enviado ao cliente em `player:joined`:

```
state = {
  id, name,
  x, y,
  hp, maxHp, mana, maxMana, stamina, maxStamina, speed, maxSpeed,
  dead, casting, cooldowns, shield, dodgeChance, masteryDodgeBonus,
  damageBonus, damageReduction, damageMult,
  statusEffects, ccHistory,
  level, xp, xpMax, gold,

  equipment:      { weapon, chest, head, boots },         // gearId ou null
  selectedSkills: { weapon_Q, weapon_W, weapon_E, chest_R, head_D },
  durability:     { weapon, chest, head, boots },         // 0–100 por slot

  equipmentMastery: { [gearId]: { level, xp, xpMax, yellowFame: { pending, level } } },

  inventory: [ { id, type } | { id, type, qty } ],        // máx 30 itens

  craftingFocus,
  gatheringSkills: { mining, woodcutting, herbalism, hunting, fishing },  // cada: { level, xp, xpMax }
  craftingSkills:  { smithing, leatherwork, alchemy, fletching, runecrafting },

  guildId, lastSeenAt, rejectedMoves
}
```

Catálogos de referência (carregados no servidor):
- `server/src/config/gear.json` — 10 armas + 9 armaduras, cada uma com seus skill slots.
- `server/src/config/skills.json` — catálogo FLAT de skills (id, name, type, castTime,
  cooldown, mana, stamina, range, damage, statusEffect, description).
- `server/src/config/items.json` — itens consumíveis e equipáveis (poções, etc.).

**Gear / armas** (de `gear.json`): `sword`, `greataxe`, `daggers`, `mace`, `hammer`, `bow`,
`fire_staff`, `frost_staff`, `arcane_staff`, `holy_staff`.
Cada arma define `slots.Q/W/E.options` (2 opções por slot, exceto E geralmente com 1).

**Armaduras** (de `gear.json`): `cloth_hood`/`leather_cap`/`plate_helm` (head),
`cloth_chest`/`leather_chest`/`plate_chest` (chest),
`cloth_boots`/`leather_boots`/`plate_boots` (boots). Cada uma define `armorType`
(cloth/leather/plate), `stats` e `skill.options` (2 opções).

> Nota: **boots NÃO fornecem slot de skill ativa** no servidor. Os slots de skill são
> apenas `weapon_Q/W/E`, `chest_R`, `head_D`. As botas dão apenas stats passivos
> (speed/maxHp) + uma skill passiva listada em `skill.options`, mas o servidor não
> mapeia botas para um slot de combate (ver `getActiveSkillIds`). A UI deve refletir isso.

---

## 3. Eventos Socket.IO (existentes vs. pendentes)

### 3.1 Já implementados no servidor (`server/src/server.js`)

| Evento (cliente → servidor) | Payload | Resposta (servidor → cliente) |
|---|---|---|
| `player:join` | `{ name, sessionToken?, zoneId? }` | `player:joined { id, sessionToken, world, abilities, npcs, state }` |
| `player:move` | `{ x, y }` | broadcast de estado da zona |
| `skill:use` | `{ skillId, tx, ty }` | `skill:result { skillId, ... }` |
| `item:pickup` | `{ itemId }` | `item:picked { item }` ou `error { msg }` |
| `chat:send` | `{ channel, message }` | `chat:message { channel, from, message, ts }` |
| `zone:change` | `{ zoneId }` | `player:joined { ... }` |
| `gear:equip` | `{ slot, gearId }` | `gear:equipped { slot, gearId, abilities, ok\|error }` |
| `skill:select` | `{ slotKey, skillId }` | `skill:select_result { slotKey, skillId, abilities, ok\|error }` |
| `mastery:convert_yellow_fame` | `{ gearId }` | `mastery:convert_result { ok\|error, ... }` |
| `repair:item` | `{ slot }` | `repair:result { ok\|error, totalCost, repairs, gold }` |
| `ping_rtt` | `ts` | `pong_rtt ts` |

Eventos espontâneos do servidor relevantes às UIs:
- `player:levelup { level, maxHp, maxMana, speed, xp, xpMax }`
- `player:revived { hp, x, y }`
- `mastery:xp { gearId, level, xp, xpMax }`
- `mastery:levelup { gearId, level, xp, xpMax, isMaxed }`
- `mastery:yellow_fame { gearId, pending, level, xpNeeded }`

### 3.2 PENDÊNCIAS DE BACKEND (precisam ser implementadas)

Estes três eventos não existem no servidor e são **necessários** para a UI de Inventário
funcionar por completo. Especificação proposta (seguir o padrão dos handlers existentes):

#### `gear:unequip`
Remove uma peça do slot de equipamento e devolve ao inventário.
```
// cliente → servidor
socket.on('gear:unequip', ({ slot } = {}) => {
  // slot: 'weapon' | 'chest' | 'head' | 'boots'
  // - valida player vivo e slot válido
  // - se vazio: { error: 'slot_empty' }
  // - inventário cheio (>=30): { error: 'inventory_full' }
  // - move gearId para inventory, zera selectedSkills do slot, recalcula stats
  // - boots não têm slot de skill; weapon limpa Q/W/E; chest limpa R; head limpa D
})
// resposta: gear:unequipped { slot, gearId, abilities, state, ok|error }
```
Reaproveitar a lógica de `equipItem` invertida + o mapa `SLOT_TO_SKILLS` já presente em
`handlePlayerDeath` (PlayerManager.js linhas ~478).

#### `item:use`
Consome um item do inventário (poção, etc.) aplicando seu `effect` (de `items.json`).
```
socket.on('item:use', ({ itemId } = {}) => {
  // - encontra item no inventory pelo id
  // - lookup em items.json pelo type → effect { hp?, mana? }
  // - aplica clamp (hp<=maxHp, mana<=maxMana)
  // - se stackable e qty>1: decrementa qty; senão remove do inventory
  // - não consumível: { error: 'not_consumable' }
})
// resposta: item:used { itemId, effect, hp, mana, inventory, ok|error }
```

#### `item:drop`
Descarta um item do inventário no chão da zona (vira pickup).
```
socket.on('item:drop', ({ itemId } = {}) => {
  // - remove do inventory
  // - world.addItem({ id, type, x: player.x+jitter, y: player.y+jitter })
  // - broadcast item:spawned para a zona (para outros verem o drop)
})
// resposta: item:dropped { itemId, ok|error }  + broadcast da zona
```

> Estes seguem exatamente o estilo dos handlers atuais (validação de zona → player vivo →
> chamada a um método do PlayerManager → emit de resultado). A maior parte da infra já existe
> (`world.addItem`, `world.removeItem`, `world.getPlayer`).

---

## 4. Plano das 4 UIs

Tecla global sugerida de toggle de painéis: `I` (inventário), `C` (status/character),
`P` (paper doll/equipamento — pode ser aba do mesmo painel de status), `K` (skills).
`Esc` fecha o painel ativo. Painéis são prefabs em um Canvas (Screen Space - Overlay).

### 4.1 UI de Inventário

**Objetivo:** mostrar `state.inventory` (até 30 itens), permitir equipar, usar, dropar.

**Campos do estado usados:**
- `state.inventory` — `[{ id, type }]` ou `[{ id, type, qty }]`
- `state.gold` — exibido no rodapé
- `items.json` / `gear.json` — para resolver `type` → nome, ícone, rarity, effect

**Layout:**
```
┌─ Inventário (I) ───────────────────────┐
│  [grade 6 x 5 = 30 slots]              │
│  ┌──┐┌──┐┌──┐┌──┐┌──┐┌──┐              │
│  │  ││  ││  ││  ││  ││  │   ...        │
│  └──┘└──┘└──┘└──┘└──┘└──┘              │
│                                        │
│  Gold: 1.234   |   Itens: 12/30        │
└────────────────────────────────────────┘
```
Cada slot: ícone (game-icons.net) + badge de quantidade (canto inf. dir. se `qty>1`) +
moldura de rarity (cor por `rarity` de items.json: common cinza, uncommon verde, etc.).

**Interações:**
- **Hover:** tooltip com nome, descrição, stats/effect (de `items.json`/`gear.json`).
- **Clique esquerdo / duplo-clique:** se equipável → `gear:equip { slot, gearId }`;
  se consumível → `item:use { itemId }` *(pendente backend)*.
- **Clique direito (menu de contexto):** "Equipar" / "Usar" / "Dropar" / "Cancelar".
  - "Dropar" → `item:drop { itemId }` *(pendente backend)*.
- **Drag & drop:** arrastar item para um slot do Paper Doll → `gear:equip`.

**Eventos Socket.IO:**
- Escuta: `player:joined` (inventário inicial), `item:picked { item }` (adiciona),
  `item:used` *(pendente)*, `item:dropped` *(pendente)*, `gear:equipped` (remove do inventário).
- Envia: `gear:equip`, `item:use` *(pendente)*, `item:drop` *(pendente)*.

**Teclas:** `I` toggle. Slot `1`–`9` poderia usar consumível do hotbar (futuro).

**Dependências de backend:** `item:use`, `item:drop` (Seção 3.2).

---

### 4.2 UI de Paper Doll / Equipamento

**Objetivo:** mostrar os 4 slots de `state.equipment` com durabilidade, permitir equipar/
desequipar e reparar.

**Campos do estado usados:**
- `state.equipment` — `{ weapon, chest, head, boots }`
- `state.durability` — `{ weapon, chest, head, boots }` (0–100)
- `state.equipmentMastery[gearId]` — para mostrar nível de maestria por peça

**Layout (boneco central com 4 slots):**
```
┌─ Equipamento (P) ──────────────┐
│              [ HEAD ]          │
│     ┌──────────────────┐       │
│     │                  │       │
│  [WEAPON]   (avatar)   │       │
│     │                  │       │
│     └──────────────────┘       │
│             [ CHEST ]          │
│             [ BOOTS ]          │
│                                │
│  Durabilidade por slot (barra) │
│  [ Reparar Tudo ]  (no Ferreiro)│
└────────────────────────────────┘
```
Cada slot mostra: ícone do gear + mini-barra de durabilidade (verde→laranja→vermelho) +
badge de nível de maestria (`equipmentMastery[gearId].level`). Slot quebrado (dur=0)
fica acinzentado com ícone de alerta (skills do slot desabilitadas — ver `getActiveSkillIds`).

**Interações:**
- **Hover:** tooltip com stats da peça (de `gear.json`), durabilidade %, maestria atual.
- **Clique esquerdo num slot equipado:** desequipa → `gear:unequip { slot }` *(pendente backend)*.
- **Drag do inventário → slot:** `gear:equip { slot, gearId }`.
- **Botão "Reparar":** `repair:item { slot }` ou `{ slot: 'all' }` — só funciona perto do
  NPC Ferreiro (`blacksmith_1`, validado pelo servidor via `BLACKSMITH_RANGE`).
  Se longe: servidor responde `repair:result { error: 'too_far', dist }` → mostrar aviso.

**Eventos Socket.IO:**
- Escuta: `gear:equipped`, `gear:unequipped` *(pendente)*, `repair:result`,
  `mastery:levelup`, `mastery:xp`.
- Envia: `gear:equip`, `gear:unequip` *(pendente)*, `repair:item`.

**Teclas:** `P` toggle (ou aba dentro do painel de Status).

**Dependências de backend:** `gear:unequip` (Seção 3.2). Reparo e equip já existem.

---

### 4.3 UI de Status do Personagem

**Objetivo:** painel de leitura mostrando todos os atributos derivados, progressão e recursos.

**Campos do estado usados (todos read-only):**
- Identidade: `state.name`, `state.level`
- Progressão: `state.xp`, `state.xpMax`, `state.gold`
- Recursos: `state.hp/maxHp`, `state.mana/maxMana`, `state.stamina/maxStamina`
- Combate derivado: `state.speed/maxSpeed`, `state.damageReduction`, `state.dodgeChance` +
  `state.masteryDodgeBonus`, `state.shield`, `state.damageBonus`, `state.damageMult`
- Skills de profissão: `state.gatheringSkills` (mining, woodcutting, herbalism, hunting,
  fishing) e `state.craftingSkills` (smithing, leatherwork, alchemy, fletching, runecrafting),
  cada uma `{ level, xp, xpMax }`. + `state.craftingFocus`.

**Layout:**
```
┌─ Personagem (C) ───────────────────────────┐
│  Aventureiro          Nível 7              │
│  XP  ▓▓▓▓▓▓░░░░  4200 / 7100               │
│  ─────────────────────────────────────────│
│  Atributos                                 │
│   HP        420 / 420                      │
│   Mana      180 / 180                      │
│   Stamina   100 / 100                      │
│   Velocidade 220                           │
│   Red. Dano  17%                           │
│   Esquiva    8%  (+maestria)               │
│  ─────────────────────────────────────────│
│  Profissões (Coleta)        (Craft)        │
│   Mineração   L5   Ferraria   L3           │
│   Lenhador    L2   Couro      L1           │
│   ...                ...                    │
│  Foco de Craft: 1000/1000                  │
└────────────────────────────────────────────┘
```

**Interações:** majoritariamente leitura. Hover em cada atributo mostra tooltip explicando
de onde vem o valor (ex: "Redução de dano = gear + maestria de plate"). Barras de XP de
profissão com tooltip de `xp / xpMax`.

**Eventos Socket.IO:**
- Escuta: `player:joined` (snapshot inicial), `player:levelup`, `player:revived`,
  `mastery:levelup`/`mastery:xp` (atualiza derivados), atualizações periódicas de estado da zona.
- Envia: nada (painel passivo).

**Teclas:** `C` toggle.

**Dependências de backend:** nenhuma. Usa só dados já enviados. **Pode ser a primeira UI.**

---

### 4.4 UI de Skill Tree / Seleção de Skills

**Objetivo:** mostrar os 5 slots de skill ativos e permitir trocar entre as opções
disponíveis para cada slot, conforme o gear equipado. Mostrar maestria por peça.

> Importante: o sistema NÃO é uma árvore de talentos clássica. Cada slot tem **2 opções
> fixas** definidas pelo gear (`gear.json`), e o jogador escolhe qual está ativa via
> `skill:select`. A "árvore" é, na prática, um seletor por slot. A UI deve refletir isso.

**Campos do estado usados:**
- `state.selectedSkills` — `{ weapon_Q, weapon_W, weapon_E, chest_R, head_D }`
- `state.equipment` — para saber quais gears (e portanto quais `options`) estão disponíveis
- `gear.json` — `weapons[id].slots.{Q,W,E}.options` e `armors[id].skill.options`
- `skills.json` — metadados de cada skill (nome, cooldown, mana, dano, descrição, ícone)
- `state.equipmentMastery[gearId]` — nível/XP de maestria + Fama Amarela pendente

**Layout:**
```
┌─ Skills (K) ────────────────────────────────────────┐
│  Arma: Espada            Maestria  L6  ▓▓▓▓▓░ (YF:2) │
│   Q ▸ [Golpe]   ◯ Rasgar                            │
│   W ▸ [Bater Forte] ◯ Escudada                      │
│   E ▸ [Executar]                                    │
│  ───────────────────────────────────────────────────│
│  Peitoral: Couro         Maestria L3  ▓▓░░░░        │
│   R ▸ [Evasão]  ◯ Implacável                        │
│  ───────────────────────────────────────────────────│
│  Elmo: Elmo de Placa     Maestria L2                │
│   D ▸ [Vontade de Ferro] ◯ Grito Provocador        │
│  ───────────────────────────────────────────────────│
│  Botas: passivas (sem slot de combate)              │
│  [ Converter Fama Amarela ]  (no Instrutor)         │
└──────────────────────────────────────────────────────┘
```
Cada opção de skill é um card: ícone + nome + tooltip detalhado (cooldown, mana, stamina,
range, dano, statusEffect, descrição — tudo de `skills.json`). A opção ativa fica destacada;
as alternativas ficam clicáveis.

**Interações:**
- **Clique numa opção alternativa:** `skill:select { slotKey, skillId }`. Servidor recusa se
  em combate (`skill:select_result { error: 'in_combat' }`) → mostrar aviso "Fora de combate".
- **Botão "Converter Fama Amarela":** `mastery:convert_yellow_fame { gearId }` — só perto do
  NPC Instrutor (`trainer_1`, validado por `TRAINER_RANGE`). Erros possíveis:
  `too_far`, `mastery_not_maxed`, `yellow_fame_maxed`, `not_enough_pending`, `not_enough_gold`.
- **Hover na barra de maestria:** mostra `level / yellowFame.pending / xpNeeded`.

**Eventos Socket.IO:**
- Escuta: `player:joined`, `skill:select_result`, `gear:equipped` (muda opções disponíveis),
  `mastery:xp`, `mastery:levelup`, `mastery:yellow_fame`, `mastery:convert_result`.
- Envia: `skill:select`, `mastery:convert_yellow_fame`.

**Teclas:** `K` toggle.

**Dependências de backend:** nenhuma nova — `skill:select` e `mastery:convert_yellow_fame`
já existem. Maior esforço de UI por causa do volume de cards/tooltips.

---

## 5. Priorização Justificada

| Ordem | UI | Esforço | Dep. backend nova | Justificativa |
|---|---|---|---|---|
| **1** | **Status do Personagem** | Baixo | Nenhuma | Painel 100% de leitura sobre dados já enviados em `player:joined`. Valida o pipeline de estado cliente↔servidor sem risco. Entrega rápida e visível. |
| **2** | **Paper Doll / Equipamento** | Médio | Só `gear:unequip` | Equip e reparo já existem. Desbloqueia o loop visual de gear (durabilidade, maestria). `gear:unequip` é trivial (lógica inversa de `equipItem`). |
| **3** | **Inventário** | Médio-Alto | `item:use`, `item:drop` | Núcleo do loop de itens, mas exige 2 eventos novos de backend. Faz mais sentido depois do Paper Doll porque os dois compartilham drag & drop e tooltips de gear. |
| **4** | **Skill Tree / Seleção** | Alto | Nenhuma | Maior superfície de UI (cards + tooltips de todas as skills + maestria + Fama Amarela). Backend já pronto, então pode vir por último sem bloquear nada. |

**Sequência de backend recomendada (em paralelo à UI):**
1. `gear:unequip` — antes/junto do Paper Doll.
2. `item:use` e `item:drop` — antes/junto do Inventário.

**Componentes compartilhados a construir uma vez (reduz retrabalho):**
- `ItemTooltip` (resolve `type` → metadados de items.json/gear.json/skills.json).
- `RarityFrame` (moldura por rarity — Kenney UI Pack).
- `ResourceBar` (Image Filled — HP/Mana/Stamina/XP/Durabilidade/Maestria).
- `IconRegistry` (mapeia gearId/itemId/skillId → sprite de game-icons.net).
- `SocketClient` wrapper para emit/on tipados dos eventos da Seção 3.

---

## 6. Checklist de Implementação

### Assets
- [ ] Baixar game-icons.net (gear, skills, poções) — CC BY 3.0, adicionar créditos.
- [ ] Baixar Kenney UI Pack RPG Expansion (molduras/painéis) — CC0.
- [ ] Importar fontes Cinzel + Inter, gerar TMP_FontAsset.
- [ ] Criar `CREDITS.txt` + tela de créditos in-game (atribuição CC BY).
- [ ] Montar `IconRegistry` (gearId/itemId/skillId → sprite).

### Backend (pendências)
- [ ] Implementar `gear:unequip` (+ método `unequipItem` no PlayerManager).
- [ ] Implementar `item:use` (+ leitura de `items.json` effect).
- [ ] Implementar `item:drop` (+ `world.addItem` + broadcast `item:spawned`).

### UIs (na ordem priorizada)
- [ ] 1. Status do Personagem (read-only).
- [ ] 2. Paper Doll / Equipamento (depende de `gear:unequip`).
- [ ] 3. Inventário (depende de `item:use`, `item:drop`).
- [ ] 4. Skill Tree / Seleção de Skills.

---

## 7. Referências de Arquivo

- Servidor / eventos Socket.IO: `server/src/server.js`
- Estado do player + equip/skill/repair/maestria: `server/src/managers/PlayerManager.js`
- Catálogo de gear e skill slots: `server/src/config/gear.json`
- Catálogo de skills: `server/src/config/skills.json`
- Catálogo de itens/consumíveis: `server/src/config/items.json`
- Constantes (ranges de NPC, durabilidade, maestria): `server/src/config/constants.js`
- Cliente Unity: `unity-client/mmo-client/`
- Docs relacionadas: `docs/SERVER_ARCHITECTURE.md`, `docs/SKILL_DEFINITIONS.md`,
  `docs/ECONOMY_AND_GEAR_REFERENCE.md`, `docs/COMBAT_AND_PROGRESSION_REFERENCE.md`
