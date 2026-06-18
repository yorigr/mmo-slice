# Unity Scene Setup — MMO v1 Client

Guia para montar a cena no Unity Editor após importar os scripts de Phase 2.

---

## 1. Criar o Projeto Unity

1. Abra Unity Hub → **New Project**
2. Template: **3D (URP)** — Universal Render Pipeline
3. Nome: `mmo-client` (ou qualquer nome)
4. Salve dentro da pasta `unity-client/`

---

## 2. Instalar NativeWebSocket

O arquivo `Packages/manifest.json` já foi criado. O Unity instala automaticamente ao abrir o projeto.

Se der erro, instale manualmente:
- **Window → Package Manager → + → Add package from git URL...**
- Cole: `https://github.com/endel/NativeWebSocket.git#upm`

---

## 3. Copiar os Scripts

Copie a pasta `unity-client/Assets/Scripts/` para dentro do seu projeto Unity em `Assets/Scripts/`.

Estrutura esperada:
```
Assets/
  Scripts/
    GameManager.cs
    Network/
      NetworkManager.cs
      SocketIOParser.cs
    Player/
      PlayerController.cs
      CameraController.cs
    World/
      WorldState.cs
      GroundSampler.cs
      MonsterController.cs
    UI/
      HUD.cs
```

---

## 4. Criar a Layer "Ground"

Necessário para o `GroundSampler` encontrar o terreno via raycast.

1. Selecione qualquer GameObject → **Inspector → Layer → Add Layer...**
2. Em **User Layer 8** (ou qualquer slot livre) escreva: `Ground`
3. Selecione seu terreno/plano → Inspector → **Layer → Ground**

---

## 5. Hierarquia da Cena

Crie os seguintes GameObjects (GameObject → Create Empty):

```
Scene
├── _Systems                    ← GameObject vazio, pai de todos os managers
│   ├── NetworkManager          ← Adicionar script NetworkManager.cs
│   ├── WorldState              ← Adicionar script WorldState.cs
│   └── GameManager             ← Adicionar scripts GameManager.cs + MonsterController.cs
├── Camera                      ← Main Camera existente
│   └── [Adicionar CameraController.cs]
├── Terrain (ou Plane)          ← Layer = "Ground"
└── Canvas (HUD)
    └── [Estrutura de HUD — ver seção 8]
```

---

## 6. Configurar a Câmera

Selecione a Main Camera:

| Propriedade | Valor |
|-------------|-------|
| Projection | **Orthographic** |
| Size | 10 |
| Position | qualquer (o CameraController ajusta) |
| Rotation | X=30, Y=45, Z=0 |

No script **CameraController**:
- `Fixed Rotation`: X=30, Y=45, Z=0
- `Offset`: X=-10, Y=14, Z=-10
- `Zoom Default`: 10
- `Zoom Min`: 5 / `Zoom Max`: 20

---

## 7. Configurar o GameManager

Selecione o GameObject **GameManager** → Inspector:

| Campo | O que arrastar |
|-------|---------------|
| Player Prefab | Prefab do jogador local (com PlayerController + Rigidbody) |
| Remote Player Prefab | Prefab simples (cubo) para outros jogadores |
| Monster Prefab | Prefab simples (cubo vermelho) para monstros |
| Player Name | Seu nome no jogo |
| Player Class | warrior / mage / ranger / healer / bruiser |
| Camera Controller | Arrastar a Main Camera |
| Hud | Arrastar o Canvas/HUD |

---

## 8. Criar Prefab do Jogador Local

1. **GameObject → 3D Object → Capsule** → renomear para `Player`
2. Adicionar componentes:
   - `PlayerController` (script)
   - `Rigidbody` → Is Kinematic: **true** (o PlayerController move via transform)
   - `Capsule Collider`
3. Criar como Prefab: arraste para `Assets/Prefabs/`

---

## 9. Criar Prefab do Jogador Remoto

1. **GameObject → 3D Object → Capsule** → renomear para `RemotePlayer`
2. Cor diferente (material azul/cinza)
3. **NÃO adicionar** PlayerController (controlado pelo servidor)
4. Criar como Prefab

---

## 10. Criar o HUD (Canvas)

1. **GameObject → UI → Canvas** → renomear para `HUD`
2. Canvas: **Screen Space — Overlay**
3. Adicionar `HUD.cs` ao Canvas

Filhos do Canvas necessários pelo HUD.cs:

```
Canvas (HUD)
├── HPBar          ← UI/Slider  → referência: hpBar
│   └── Fill Area/Fill  → referência: hpFill (Image)
├── ManaBar        ← UI/Slider  → referência: manaBar
│   └── Fill Area/Fill  → referência: manaFill (Image)
├── PlayerNameText ← TextMeshProUGUI → referência: playerNameText
├── LevelText      ← TextMeshProUGUI → referência: levelText
└── PingText       ← TextMeshProUGUI → referência: pingText
```

---

## 11. Verificar a Conexão

1. Rode o servidor: duplo clique em `iniciar-mmorpg.bat` na pasta raiz
2. No Unity: **Play**
3. Console deve mostrar:
   ```
   [NetworkManager] Conectando em ws://localhost:3000/...
   [NetworkManager] Conectado!
   [GameManager] Conectado ao servidor. Enviando player:join...
   [GameManager] Jogador local spawnado. ID: xxx em (24.0, 0.0, 18.0)
   ```

---

## 12. Checklist Final

- [ ] Layer "Ground" criada e aplicada ao terreno
- [ ] NativeWebSocket instalado (sem erros de compilação)
- [ ] Todos os scripts compilando sem erros
- [ ] GameManager com todos os campos do Inspector preenchidos
- [ ] Servidor mmo-v1 rodando em `localhost:3000`
- [ ] Console Unity sem erros após Play

---

## Troubleshooting

**"NativeWebSocket não encontrado"** → Verifique Package Manager, aguarde reimport

**"WorldState não encontrado"** → Certifique que o GameObject WorldState está na cena com o script

**"Jogador não aparece"** → Verifique que playerPrefab está atribuído no GameManager

**Jogador sempre warrior** → Era um bug corrigido — GameManager agora envia `playerClass` corretamente
