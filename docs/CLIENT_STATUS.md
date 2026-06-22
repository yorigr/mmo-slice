# Cliente Unity — Status de Implementação

> Documento de referência para continuação do trabalho.
> Atualizado em: 2026-06-20 (sessão 3)
> Propósito: qualquer pessoa (ou nova sessão de IA) pode ler este arquivo e saber
> exatamente o que está feito, o que está pela metade e o que falta.

---

## Estrutura de scripts

```
unity-client/mmo-client/Assets/Scripts/
├── GameManager.cs              ← orquestrador principal (LEIA AQUI PRIMEIRO)
├── StickManBuilder.cs          ← cria personagens proceduralmente (sem assets)
├── Network/
│   ├── NetworkManager.cs       ← WebSocket + Socket.IO v4 (não modificar)
│   └── SocketIOParser.cs       ← decodifica Engine.IO v4 (não modificar)
├── Player/
│   ├── PlayerController.cs     ← input local, player:move, reconciliação
│   └── CameraController.cs     ← câmera isométrica
├── World/
│   ├── WorldState.cs           ← estado do mundo + LocalFullState (equipment/inv/maestria)
│   ├── MonsterController.cs    ← visual dos monstros (barra de HP)
│   ├── NpcController.cs        ← visual dos NPCs (Ferreiro/Instrutor)
│   ├── ItemWorldController.cs  ← itens no chão, tecla E para pegar
│   └── GroundSampler.cs        ← posiciona objetos no terreno
└── UI/
    ├── UIManager.cs            ← controla os 4 painéis (P/C/K/I/Esc)
    ├── UIPanelBase.cs          ← classe base: canvas procedural, Emit(), Refresh()
    ├── StatusPanel.cs          ← P: stats, crafting/gathering skills, maestria
    ├── PaperDollPanel.cs       ← C: slots de equipment, durabilidade, maestria
    ├── SkillTreePanel.cs       ← K: opções de skill por gear, skill:select + hover tooltip
    ├── InventoryPanel.cs       ← I: grid 30 slots, equipar/usar/dropar
    ├── GearNames.cs            ← tabela PT-BR: gearId/skillId → nome legível
    ├── SkillCatalog.cs         ← 26 skills com stats completos (espelho de skills.json)
    ├── ItemCatalog.cs          ← itens com stats + raridade colorida (espelho de items.json)
    ├── TooltipPopup.cs         ← popup singleton que segue o mouse (rich text TMP)
    ├── HUD.cs                  ← HP/mana/XP/gold/nível/ping
    ├── SkillBar.cs             ← barra 6 slots (1–6), cooldown visual, hover tooltip
    ├── RespawnPanel.cs         ← tela de morte, countdown 8s
    ├── ChatUI.cs               ← chat global/zona
    ├── FloatingText.cs         ← texto flutuante (+30 HP, -15, etc.)
    └── PlayerNameTag.cs        ← tag de nome acima do jogador
```

---

## O que está IMPLEMENTADO E FUNCIONAL

### Backend (Node.js)
- [x] Sistema gear-based completo: weapon/chest/head/boots → 6 slots de skill (Q/W/E/R/D/F)
- [x] Equipment Mastery com anti-abuse (zone mult × mob tier)
- [x] Fama Amarela (Yellow Fame) com NPC Instrutor Magnus
- [x] Durabilidade + reparo no Ferreiro Aldric
- [x] gear:equip, gear:unequip (devolve ao inventário), skill:select
- [x] item:pickup, item:drop (spawna no mundo), item:use (aplica efeito de items.json)
- [x] Chat global/zona, zone:change, ping_rtt, reconexão por sessionToken
- [x] 41/41 testes passando

### Cliente Unity
- [x] Conexão WebSocket + Socket.IO v4 sem dependências externas
- [x] Player local: movimento, câmera isométrica, stick man procedural
- [x] Jogadores remotos: spawn/despawn automático, interpolação de posição
- [x] Monstros: visual com barra de HP, MonsterController
- [x] NPCs estáticos: NpcController renderiza Ferreiro e Instrutor no mundo [NOVO]
- [x] Itens no chão: ItemWorldController, tecla E para pegar
- [x] HUD: HP, mana, XP, gold, nível, ping com cores dinâmicas
- [x] SkillBar: 6 slots (teclas 1–6), cooldown visual radial, hover tooltip via TooltipPopup
- [x] Barra de skills mapeada a abilities[] recebido em player:joined
- [x] Reconexão por sessionToken (salvo em PlayerPrefs)
- [x] RespawnPanel: tela de morte com countdown
- [x] ChatUI: Enter para abrir/enviar, Tab canal, Esc cancelar
- [x] FloatingText: textos flutuantes no mundo (+HP, dano, etc.)
- [x] UIManager: P/C/K/I para abrir painéis, Esc fecha
- [x] StatusPanel (P): stats derivados, gathering/crafting skills, maestria ativa
- [x] PaperDollPanel (C): 4 slots de gear com durabilidade e botões Desequipar/Reparar
- [x] SkillTreePanel (K): opções de skill por gear, seleção via skill:select, hover tooltip
- [x] TooltipPopup.cs: singleton procedural, segue mouse, rich text, reposicionamento automático
- [x] SkillCatalog.cs: lookup estático de 26 skills (espelho de skills.json), método ToTooltipText()
- [x] ItemCatalog.cs: lookup estático de todos os itens (espelho de items.json), raridade colorida
- [x] InventoryPanel.cs: hover nos slots mostra tooltip via ItemCatalog + TooltipPopup
- [x] GameManager.cs: HandleItemUseResult + structs NpcListPayload/ItemUseResultData (Parse manual de nested JSON)
- [x] InventoryPanel (I): grid 30 slots, botões Equipar/Usar/Dropar
- [x] WorldState.Local: equipment, inventory, maestria, gearOptions sincronizados

---

## O que está PARCIALMENTE IMPLEMENTADO

### item:use_result
- ✅ Handler registrado em GameManager: `_net.OnEvent["item:use_result"] = HandleItemUseResult`
- ✅ `ItemUseResultData.Parse()` extrai `effect.hp` e `effect.mana` manualmente (JsonUtility não faz nested)
- ✅ FloatingText "+30 HP" verde / "+40 Mana" azul acima do player ao usar poção
- ✅ `inventory:updated` atualizado via `_world.HandleInventoryUpdated`

### Teclas dos painéis (P/C/K/I)
- O plano original dizia P=PaperDoll, C=Status. Implementado invertido (P=Status, C=PaperDoll)
- Decisão: teclas serão configuráveis pelo jogador no futuro — não corrigir hardcoded

### Painéis de UI — dados reais vs. placeholder
- StatusPanel: dados reais de WorldState.Local ✅ mas campos como `dodgeChance`/`damageReduction`
  precisam de cast float→% (verificar se aparece "0.03" ou "3%")
- PaperDollPanel: dados reais ✅ mas o botão "Reparar" não mostra distância ao Ferreiro
- SkillTreePanel: mostra nomes PT-BR via GearNames ✅ mas sem cooldown/mana/descrição (tooltip)
- InventoryPanel: grid funcional ✅ mas sem ícones (só texto do tipo do item)

---

## O que FALTA implementar

### Alta prioridade

#### ~~Tooltips de skill~~ ✅ FEITO
- TooltipPopup.cs criado: singleton, rich text, segue mouse, reposicionamento automático
- SkillCatalog.cs criado: 26 skills com ToTooltipText() — cooldown, mana, dano, descrição
- SkillTreePanel integrado: hover em cada botão de skill mostra tooltip
- SkillBar integrado: hover em cada slot mostra tooltip da skill equipada

#### ~~Tooltips de item~~ ✅ FEITO
- ItemCatalog.cs criado (espelho de items.json); raridade colorida no texto
- InventoryPanel integrado: hover em cada slot exibe tooltip via ItemCatalog + TooltipPopup

#### ~~Feedback de distância ao NPC~~ ✅ FEITO
- `GameManager.DistanceToNpc(type)` calcula distância em pixels até o NPC pelo tipo
- `PaperDollPanel`: botão "Reparar" só habilitado dentro de 120px do Ferreiro; texto muda para "Reparar ⚒ (longe)"
- `StatusPanel`: botão "Converter Fama Amarela" adicionado; só habilitado perto do Instrutor (120px) e se houver fama pendente

#### ~~FloatingText para item:use_result~~ ✅ FEITO
- Handler `item:use_result` registrado e funcionando (GameManager.cs ~linha 590)

### Média prioridade

#### Ícones de gear/skill/item
- Assets gratuitos mapeados no ASSETS_AND_UI_PLAN.md:
  - game-icons.net (CC BY 3.0) → download manual, importar como Sprite no Unity
  - Kenney UI Pack RPG Expansion (CC0) → slots e molduras
- `IconRegistry.cs`: mapeia gearId/itemId/skillId → Sprite (mesmo padrão GearNames.cs)
- Até ter os assets, painéis mostram texto (comportamento atual)

#### Tilemap / mundo visual
- Atual: plano vazio cinza
- Plan: tilemap isométrico com assets Kenney/Quaternius/Synty
- Blocker: requer trabalho no Unity Editor (não é só código)

#### Sprites de personagem
- Atual: stick men procedurais (StickManBuilder.cs)
- Plan: sprites reais por tipo de armor (cloth/leather/plate influencia visual)
- Blocker: requer assets e trabalho no Unity Editor

#### Persistência de banco de dados
- `sql.js` já está em `package.json` do servidor
- PlayerManager e SessionManager usam memória por enquanto
- Implementar: salvar/carregar state em SQLite via `sql.js`
- Arquivo: criar `server/src/managers/DatabaseManager.js`

### Baixa prioridade

#### Sistema de configuração de teclas
- UIManager.cs usa KeyCode hardcoded (P/C/K/I)
- Skill hotkeys hardcoded (1–6)
- Implementar: tela de configurações com PlayerPrefs para persistir bindings

#### Drag & Drop no inventário
- InventoryPanel.cs tem botões Equipar/Usar/Dropar mas não drag & drop
- Arrastar item do inventário → slot do PaperDoll chamaria gear:equip

#### Minimapa
- Nenhuma implementação planejada ainda

---

## Eventos Socket.IO — estado de implementação no cliente

| Evento | Direcão | Cliente trata? | Onde |
|--------|---------|---------------|------|
| `player:joined` | S→C | ✅ | GameManager.HandlePlayerJoined |
| `world:update` | S→C | ✅ | GameManager.HandleWorldUpdate → WorldState |
| `player:levelup` | S→C | ✅ | GameManager.HandleLevelUp |
| `player:xp` | S→C | ✅ | GameManager.HandleXp |
| `player:revived` | S→C | ✅ | GameManager.HandleRevived |
| `player:death_loot` | S→C | ✅ | GameManager |
| `skill:result` | S→C | ✅ | SkillBar.OnSkillResult |
| `gear:equipped` | S→C | ✅ | WorldState.HandleGearEquipped |
| `gear:unequipped` | S→C | ✅ | WorldState.HandleGearUnequipped |
| `skill:select_result` | S→C | ✅ | WorldState.HandleSkillSelectResult |
| `repair:result` | S→C | ✅ | WorldState.HandleRepairResult |
| `mastery:xp` | S→C | ✅ | WorldState.HandleMasteryXp |
| `mastery:levelup` | S→C | ✅ | WorldState.HandleMasteryLevelUp |
| `mastery:yellow_fame` | S→C | ✅ | WorldState.HandleMasteryYellowFame |
| `mastery:convert_result` | S→C | ✅ | WorldState.HandleMasteryConvertResult |
| `inventory:updated` | S→C | ✅ | WorldState.HandleInventoryUpdated |
| `item:picked` | S→C | ✅ | WorldState (adiciona ao inventário) |
| `item:use_result` | S→C | ✅ | GameManager.HandleItemUseResult + FloatingText |
| `combat:hits` | S→C | ⚠️ | FloatingText (parcial) |
| `combat:deaths` | S→C | ✅ | GameManager |
| `chat:message` | S→C | ✅ | ChatUI |
| `status:applied` | S→C | ❌ | **Falta** — efeito de status (CC imune, etc.) |

---

## Como testar o servidor localmente

```
# Na pasta server/
npm start          # inicia servidor na porta 3000
npm test           # roda 41 testes (deve mostrar 41 passou · 0 falhou)
```

## Como iniciar o Unity client

1. Abrir Unity Hub → Open → selecionar `unity-client/mmo-client/`
2. Abrir cena `Assets/Scenes/Game.unity`
3. Garantir que o GameObject "GameManager" existe na cena com os scripts atribuídos
4. Play → servidor deve estar rodando na porta 3000

## Próxima sessão de trabalho — por onde começar

1. Leia este arquivo (CLIENT_STATUS.md)
2. Leia `docs/INDEX.md` para entender a arquitetura geral
3. Rode `npm test` no servidor para confirmar 41/41
4. Implemente `DatabaseManager.js` no servidor para persistência SQLite (sql.js já instalado)
5. Baixe assets: game-icons.net e Kenney UI Pack (passos manuais no browser)
6. `IconRegistry.cs`: mapeia gearId/itemId/skillId → Sprite quando os assets chegarem
