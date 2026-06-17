# Phase 2 — Unity Client (Isométrico 3D)

> **Status:** Em desenvolvimento  
> **Dependência:** Phase 1 (servidor Node.js + Socket.IO) — concluída e testada

---

## Visão Geral

Phase 2 constrói o cliente Unity que se conecta ao servidor autoritativo já existente. O objetivo é ter um jogador se movendo pelo mundo isométrico com sincronização real via WebSocket, client-side prediction e reconciliação de posição.

**Resultado esperado ao fim da fase:**
- Cliente Unity conecta ao servidor Node.js via WebSocket/Socket.IO
- Jogador local se move com WASD, posição sincronizada com o servidor
- Outros jogadores aparecem e se movem em tempo real
- HUD básico funcional (HP, mana, ping)
- Câmera isométrica travada seguindo o jogador

---

## Stack Técnica

| Componente | Tecnologia |
|---|---|
| Engine | Unity 6 LTS |
| Pipeline de renderização | Universal Render Pipeline (URP) |
| Câmera | Ortográfica isométrica (estilo Albion Online) |
| Rede | NativeWebSocket + parser Socket.IO v4 manual |
| Serialização | JsonUtility (built-in Unity — zero dependências) |
| Plataforma alvo | Windows desktop (Mac/Linux futuro) |
| Linguagem | C# 10 (compilador Unity 6) |

### Por que NativeWebSocket e não SocketIOUnity?

O pacote `SocketIOUnity` adiciona ~5 dependências e tem histórico de quebrar entre versões do Unity. Como o servidor usa Socket.IO v4 (protocolo relativamente simples sobre WebSocket), é mais robusto implementar o parser manualmente — são apenas ~50 linhas (`SocketIOParser.cs`). Isso também facilita debugging e controle total do protocolo.

---

## Instalação do NativeWebSocket

No Unity, abra **Window → Package Manager → + → Add package from git URL** e cole:

```
https://github.com/endel/NativeWebSocket.git#upm
```

Aguarde a importação. O package estará disponível como `com.endel.nativewebsocket`.

> **Nota WebGL:** Se no futuro exportar para WebGL, NativeWebSocket tem suporte nativo — não muda o código C#.

---

## Estrutura de Pastas do Projeto Unity

```
unity-client/
├── Assets/
│   ├── Scenes/
│   │   ├── Login.unity          # Tela de login/autenticação
│   │   ├── Loading.unity        # Loading screen entre cenas
│   │   └── Game.unity           # Cena principal do jogo
│   │
│   ├── Scripts/
│   │   ├── GameManager.cs       # Orquestrador singleton principal
│   │   ├── Network/
│   │   │   ├── NetworkManager.cs    # WebSocket + protocolo Socket.IO
│   │   │   └── SocketIOParser.cs    # Parser do protocolo Socket.IO v4
│   │   ├── Player/
│   │   │   ├── PlayerController.cs  # Input, prediction, reconciliação
│   │   │   └── CameraController.cs  # Câmera isométrica com smooth follow
│   │   ├── World/
│   │   │   ├── WorldState.cs        # Estado do mundo e jogadores remotos
│   │   │   └── GroundSampler.cs     # Raycast para altura do terreno
│   │   └── UI/
│   │       └── HUD.cs               # HP, mana, ping, nome
│   │
│   ├── Art/
│   │   ├── Materials/           # Materiais URP (flat shading)
│   │   ├── Models/              # Meshes 3D dos personagens e tiles
│   │   └── Textures/            # Texturas e sprites
│   │
│   ├── Prefabs/
│   │   ├── Player.prefab        # Prefab do jogador local
│   │   ├── RemotePlayer.prefab  # Prefab de outros jogadores
│   │   └── WorldTile.prefab     # Tile do chão
│   │
│   └── Settings/
│       ├── URPAsset.asset       # Configuração URP
│       └── URPRenderer.asset    # Renderer feature config
│
├── Packages/
│   └── manifest.json            # Inclui NativeWebSocket após instalação
│
└── ProjectSettings/
    └── ...
```

---

## Mapeamento de Coordenadas Servidor ↔ Unity

### Convenção de eixos

| Dimensão | Servidor | Unity | Origem |
|---|---|---|---|
| Horizontal (leste/oeste) | `x` (0 a 2400) | `Vector3.x` | Servidor |
| Vertical (norte/sul) | `y` (0 a 1800) | `Vector3.z` | Servidor |
| Altura (relevo) | — | `Vector3.y` | **GroundSampler (raycast)** |

### Abordagem de elevação — Híbrida

O servidor permanece **2D** (`x, y`): lógica de jogo, colisão e combate operam no plano horizontal. O relevo existe **apenas no cliente Unity**, determinado por raycast downward via `GroundSampler.cs`.

Isso é a mesma abordagem de Albion Online: servidor valida posição 2D, cliente renderiza em 3D com terreno real.

Consequência: jogadores em alturas diferentes estão na mesma posição do ponto de vista do servidor — a câmera mais alta dá vantagem visual, mas não mecânica.

### Fator de escala

O servidor usa pixels como unidade (MAP_W=2400, MAP_H=1800). Unity trabalha bem com objetos na faixa 1–100 unidades. Usamos divisor **50**:

```
servidor (x=2400, y=1800)  →  Unity (48, y_terreno, 36)
servidor (x=0,    y=0)     →  Unity (0,  y_terreno,  0)
```

**Funções de conversão (C#) — via GroundSampler:**

```csharp
// Servidor → Unity (com altura do terreno)
Vector3 pos = GroundSampler.ServerToUnity(serverX, serverY, scale: 50f);

// Unity → Servidor (só XZ importa para o servidor)
(float sx, float sy) UnityToServer(Vector3 pos)
    => (pos.x * 50f, pos.z * 50f);
```

### Layer "Ground" — obrigatória

`GroundSampler` usa `LayerMask.GetMask("Ground")` para o raycast. Sem ela, acerta personagens e projéteis.

**No Unity:** Edit → Project Settings → Tags and Layers → adicione a Layer `Ground` → aplique ao(s) objeto(s) de terreno da cena.

### Por que divisor 50?

- Um tile padrão Unity de 1×1 unidade representa 50×50 pixels no servidor
- O mapa inteiro fica 48×36 unidades Unity — tamanho confortável para física e câmera
- Speed no servidor é 200 px/s → 4 unidades Unity/s (razoável visualmente)

---

## Câmera Isométrica

### Configuração base (estilo Albion Online / Diablo)

```
Rotation:  X = 30°,  Y = 45°,  Z = 0°
Projection: Orthographic
Orthographic Size: 10
```

### Por que X=30° e não X=45°?

Com X=45° a câmera seria isométrica "matemática" (ângulos iguais), mas visualmente parece muito inclinada. X=30° é o ângulo usado por Albion Online, Diablo III e a maioria dos ARPGs — dá mais visibilidade horizontal e parece mais natural.

### Offset da câmera

A câmera não fica **em cima** do jogador — fica atrás e acima. Com rotação X=30°, Y=45°, o offset típico é:

```csharp
Vector3 offset = new Vector3(-10f, 14f, -10f); // ajustável no Inspector
```

### Zoom

- Mínimo: `orthographicSize = 5` (detalhe)
- Máximo: `orthographicSize = 20` (visão estratégica)
- Velocidade do zoom: scroll do mouse com interpolação suave

---

## Cenas Unity Necessárias

### 1. `Login.unity`
- Campo de usuário e senha
- Botão de conectar
- Ao conectar com sucesso → carregar `Loading.unity`

### 2. `Loading.unity`
- Barra de progresso
- Aguarda resposta do `player:join` do servidor
- Ao receber confirmação → carregar `Game.unity`

### 3. `Game.unity`
- Cena principal com o mundo
- Contém: GameManager, NetworkManager, WorldState, HUD
- Spawna o PlayerPrefab na posição recebida do servidor

---

## Protocolo Socket.IO v4 (resumo)

O servidor usa Socket.IO, que adiciona um envelope sobre WebSocket puro:

| Tipo | Formato | Significado |
|---|---|---|
| Connect | `40` | Servidor confirma conexão |
| Ping | `2` | Servidor envia ping |
| Pong | `3` | Cliente responde ao ping |
| Message (emit) | `42["evento",{dados}]` | Mensagem bidirecional |

**Enviar evento:**
```
42["player:move",{"x":1200,"y":900,"dir":"N"}]
```

**Receber evento:**
```
42["world:update",{"players":[...],"timestamp":12345}]
```

---

## Eventos do Servidor

| Evento | Direção | Payload |
|---|---|---|
| `player:join` | C→S | `{name, class}` |
| `player:move` | C→S | `{x, y, dir}` |
| `skill:use` | C→S | `{skillId, targetX, targetY}` |
| `world:update` | S→C | `{players[], timestamp}` |
| `player:joined` | S→C | `{id, name, x, y, hp, maxHp}` |
| `player:left` | S→C | `{id}` |
| `player:died` | S→C | `{id}` |

---

## Checklist de Entregáveis — Phase 2

### Infraestrutura
- [x] Estrutura de pastas criada
- [ ] Unity 6 LTS + URP configurado
- [ ] NativeWebSocket instalado via Package Manager
- [ ] Cenas Login, Loading, Game criadas

### Scripts (entregues nesta fase)
- [x] `SocketIOParser.cs` — parser do protocolo
- [x] `NetworkManager.cs` — conexão WebSocket
- [x] `PlayerController.cs` — input + prediction + reconciliação
- [x] `CameraController.cs` — câmera isométrica
- [x] `WorldState.cs` — estado do mundo
- [x] `HUD.cs` — interface básica
- [x] `GameManager.cs` — orquestrador

### Funcionalidades
- [ ] Conectar ao servidor local (ws://localhost:3000)
- [ ] Spawnar jogador na posição do servidor
- [ ] WASD move o jogador (client-side prediction)
- [ ] Posição reconciliada com servidor a 20Hz
- [ ] Outros jogadores visíveis e sincronizados
- [ ] HUD mostrando HP e ping
- [ ] Câmera seguindo o jogador

### Qualidade
- [ ] Sem erros no Console Unity
- [ ] FPS estável > 60 em cena vazia
- [ ] Reconexão automática funciona (testar matando o servidor)
- [ ] Build Windows sem erros

---

## Fora do Escopo de Phase 2

Para manter o foco, os itens abaixo são explicitamente excluídos desta fase:

- ❌ Sistema de combate e habilidades (Phase 3)
- ❌ Inventário e itens (Phase 3)
- ❌ NPCs e monstros (Phase 3)
- ❌ Tela de criação de personagem (Phase 3)
- ❌ Chat em jogo (Phase 4)
- ❌ Minimapa (Phase 4)
- ❌ Efeitos de partícula e VFX (Phase 4)
- ❌ Áudio (Phase 4)
- ❌ Anti-cheat (Phase 5)
- ❌ Build para Mac/Linux (Phase 5)
- ❌ Autenticação real / banco de dados (Phase 3)
- ❌ Animações de personagem além de direção (Phase 3)

---

## Referências

- [NativeWebSocket GitHub](https://github.com/endel/NativeWebSocket)
- [Socket.IO Protocol v4](https://socket.io/docs/v4/socket-io-protocol/)
- [Unity URP Documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest)
- [Albion Online Camera Setup (community)](https://forum.unity.com/threads/isometric-camera-setup.html)
