// MapGenerator.cs
// Gera o ambiente visual do mapa proceduralmente ao iniciar o jogo.
// Não requer assets externos — usa primitivos Unity + texturas Perlin (ProceduralTextureGen).
//
// Layout do mapa (48u × 36u, convertido de 2400px × 1800px):
//   Centro-cidade: (24u, 18u) — ferreiro e área segura
//   Trainer:       (20u, 12u) — noroeste do centro
//   Wilderness:    resto do mapa — florestas, rochas, monstros
//
// Hierarquia gerada (todos filhos de "Environment"):
//   Environment/
//     Ground        ← plano de grama texturizado (Perlin)
//     TownStone     ← pedras da cidade (textura stone)
//     Roads/        ← caminhos de terra/pedra (textura dirt)
//     Trees/        ← 3 variantes de árvore com collider no tronco
//     Rocks/        ← grupos de rochas com textura stone
//     Buildings/    ← construções (ferreiro, treino, cabanas)
//     Props/        ← decorações (fogueira, barril, fonte)
//     Boundary/     ← muros nas bordas do mapa
//     Water/        ← lago com textura de água
//
// Chamado por: GameManager.Start()

using UnityEngine;

namespace MMORPG.World
{
    public static class MapGenerator
    {
        // ─── Dimensões do mapa ────────────────────────────────────────────────────
        private const float MAP_W = 48f;  // 2400px / 50
        private const float MAP_H = 36f;  // 1800px / 50

        // Centro da cidade (servidor: 1200,900 → Unity: 24,18)
        private const float TOWN_X      = 24f;
        private const float TOWN_Z      = 18f;
        private const float TOWN_RADIUS = 7f;    // raio da praça de pedra
        private const float SAFE_RADIUS = 10f;   // sem árvores/rochas perto da cidade

        // Trainer (servidor: 1000,600 → Unity: 20,12)
        private const float TRAINER_X = 20f;
        private const float TRAINER_Z = 12f;

        // Lago (sudeste)
        private const float LAKE_X = 40f;
        private const float LAKE_Z = 8f;

        // Seed fixa: mapa idêntico entre sessões (importante para colisões online)
        private const int RANDOM_SEED = 42;

        // ─── Paleta de cores Albion-like ──────────────────────────────────────────
        // Cores base (usadas junto com texturas Perlin)
        private static readonly Color GrassColor    = new Color(0.30f, 0.55f, 0.20f);
        private static readonly Color StoneColor    = new Color(0.52f, 0.50f, 0.46f);
        private static readonly Color PathColor     = new Color(0.58f, 0.50f, 0.38f);
        private static readonly Color TrunkColor    = new Color(0.38f, 0.25f, 0.10f);
        private static readonly Color FoliageColor  = new Color(0.18f, 0.44f, 0.12f);
        private static readonly Color FoliageDark   = new Color(0.10f, 0.30f, 0.06f);
        private static readonly Color FoliageYellow = new Color(0.50f, 0.52f, 0.10f); // árvore outono
        private static readonly Color RockColor     = new Color(0.50f, 0.48f, 0.45f);
        private static readonly Color RockDark      = new Color(0.36f, 0.34f, 0.32f);
        private static readonly Color WoodColor     = new Color(0.45f, 0.30f, 0.15f);
        private static readonly Color RoofColor     = new Color(0.55f, 0.32f, 0.15f);
        private static readonly Color WallColor     = new Color(0.72f, 0.68f, 0.60f);
        private static readonly Color WaterColor    = new Color(0.16f, 0.42f, 0.70f, 0.90f);
        private static readonly Color BoundaryColor = new Color(0.32f, 0.26f, 0.18f);
        private static readonly Color OreColor      = new Color(0.48f, 0.62f, 0.80f); // minério azul

        // ─── Cache de texturas (evita recriar a cada objeto) ─────────────────────
        private static Texture2D _texGrass;
        private static Texture2D _texDirt;
        private static Texture2D _texStone;
        private static Texture2D _texBark;
        private static Texture2D _texFoliage;
        private static Texture2D _texWater;
        private static Texture2D _texWood;

        // ─── Ponto de entrada ─────────────────────────────────────────────────────

        /// <summary>
        /// Gera todo o ambiente do mapa. Chamado por GameManager.Start().
        /// Destrói e recria o container "Environment" a cada chamada (ex: reconexão).
        /// </summary>
        public static void Generate()
        {
            // Destrói ambiente anterior para reconexões limpas
            var old = GameObject.Find("Environment");
            if (old != null) Object.Destroy(old);

            // Gera texturas procedurais (uma vez por sessão)
            _texGrass   = ProceduralTextureGen.Grass(0);
            _texDirt    = ProceduralTextureGen.Dirt(1);
            _texStone   = ProceduralTextureGen.Stone(2);
            _texBark    = ProceduralTextureGen.Bark(3);
            _texFoliage = ProceduralTextureGen.Foliage(5);
            _texWater   = ProceduralTextureGen.Water(4);
            _texWood    = ProceduralTextureGen.Wood(6);

            var root = new GameObject("Environment");
            var rng  = new System.Random(RANDOM_SEED);

            BuildGround(root);
            BuildTownArea(root);
            BuildPaths(root);
            BuildWater(root);
            BuildTrees(root, rng);
            BuildRocks(root, rng);
            BuildBlacksmithBuilding(root);
            BuildTrainerBuilding(root);
            BuildCabins(root, rng);
            BuildProps(root, rng);
            BuildBoundary(root);
            SetupLightingAndAtmosphere();

            Debug.Log("[MapGenerator] Mapa gerado com texturas procedurais.");
        }

        // ─── Terreno base ─────────────────────────────────────────────────────────

        private static void BuildGround(GameObject root)
        {
            // Reutiliza Ground existente (mantém layer "Ground" = 8 para GroundSampler)
            var existingGround = GameObject.Find("Ground");
            if (existingGround != null)
            {
                existingGround.transform.localScale = new Vector3(MAP_W / 10f, 1f, MAP_H / 10f);
                existingGround.transform.position   = new Vector3(MAP_W * 0.5f, 0f, MAP_H * 0.5f);
                ApplyMaterial(existingGround, GrassColor, _texGrass, tilingX: 12f, tilingY: 9f);
                return;
            }

            // Fallback: cria novo plane (pode não ter layer "Ground" correto)
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground";
            go.transform.SetParent(root.transform);
            go.transform.localScale = new Vector3(MAP_W / 10f, 1f, MAP_H / 10f);
            go.transform.position   = new Vector3(MAP_W * 0.5f, 0f, MAP_H * 0.5f);
            ApplyMaterial(go, GrassColor, _texGrass, tilingX: 12f, tilingY: 9f);
        }

        // ─── Área urbana ──────────────────────────────────────────────────────────

        private static void BuildTownArea(GameObject root)
        {
            var container = new GameObject("TownStone");
            container.transform.SetParent(root.transform);

            // Praça central — tiles de pedra
            CreateFlatRect(container, "TownSquare",
                new Vector3(TOWN_X, 0.01f, TOWN_Z), TOWN_RADIUS * 1.8f, TOWN_RADIUS * 1.8f,
                StoneColor, _texStone, 4f, 4f);

            // Fonte no centro da praça (decorativa)
            SpawnFountain(container, TOWN_X, TOWN_Z);

            // Zona de treinamento
            CreateFlatRect(container, "TrainingArea",
                new Vector3(TRAINER_X, 0.01f, TRAINER_Z), 6f, 6f,
                new Color(0.44f, 0.42f, 0.38f), _texStone, 2f, 2f);
        }

        // ─── Caminhos ─────────────────────────────────────────────────────────────

        private static void BuildPaths(GameObject root)
        {
            var container = new GameObject("Roads");
            container.transform.SetParent(root.transform);

            // Estrada principal: cidade → borda leste
            CreateFlatRect(container, "RoadEast",
                new Vector3(TOWN_X + 8f, 0.005f, TOWN_Z), 16f, 2.8f,
                PathColor, _texDirt, 6f, 1f);

            // Estrada → norte
            CreateFlatRect(container, "RoadNorth",
                new Vector3(TOWN_X, 0.005f, TOWN_Z + 8f), 2.8f, 16f,
                PathColor, _texDirt, 1f, 6f);

            // Estrada → sul (para lago)
            CreateFlatRect(container, "RoadSouth",
                new Vector3(TOWN_X + 6f, 0.005f, TOWN_Z - 7f), 2.0f, 14f,
                PathColor, _texDirt, 1f, 5f);

            // Estrada → trainer
            CreateFlatRect(container, "RoadToTrainer",
                new Vector3(22f, 0.005f, 15.5f), 5f, 2.2f,
                PathColor, _texDirt, 2f, 1f);
        }

        // ─── Água ─────────────────────────────────────────────────────────────────

        private static void BuildWater(GameObject root)
        {
            var container = new GameObject("Water");
            container.transform.SetParent(root.transform);

            CreateFlatRect(container, "Lake",
                new Vector3(LAKE_X, 0.03f, LAKE_Z), 12f, 10f,
                WaterColor, _texWater, 3f, 2.5f);

            // Margem de areia ao redor do lago
            CreateFlatRect(container, "LakeShore",
                new Vector3(LAKE_X, 0.01f, LAKE_Z), 14f, 12f,
                new Color(0.70f, 0.64f, 0.44f), _texDirt, 3f, 3f);
        }

        // ─── Árvores (3 variantes) ────────────────────────────────────────────────

        private static void BuildTrees(GameObject root, System.Random rng)
        {
            var container = new GameObject("Trees");
            container.transform.SetParent(root.transform);

            int count = 0, attempts = 0;
            while (count < 85 && attempts < 800)
            {
                attempts++;
                float x = (float)(rng.NextDouble() * (MAP_W - 4f)) + 2f;
                float z = (float)(rng.NextDouble() * (MAP_H - 4f)) + 2f;

                float distCity    = Vector2.Distance(new Vector2(x, z), new Vector2(TOWN_X,    TOWN_Z));
                float distTrainer = Vector2.Distance(new Vector2(x, z), new Vector2(TRAINER_X, TRAINER_Z));
                float distLake    = Vector2.Distance(new Vector2(x, z), new Vector2(LAKE_X, LAKE_Z));
                if (distCity < SAFE_RADIUS || distTrainer < 6f || distLake < 7f) continue;

                // Scale 2.5–4.5: árvores devem ser 3–5× a altura do personagem (~1u).
                // Valor anterior (0.65–1.45) gerava árvores menores ou iguais ao jogador.
                float scale   = 2.5f + (float)rng.NextDouble() * 2.0f;
                int   variant = rng.Next(3); // 0=pinheiro, 1=carvalho, 2=outono
                SpawnTree(container, $"Tree_{count}", x, z, scale, variant, rng);
                count++;
            }
        }

        // Modelos Kenney por variante de árvore
        // Ordem: pinheiro, carvalho, outono
        private static readonly string[][] TREE_MODELS =
        {
            new[]{ "tree_pineDefaultA", "tree_pineDefaultB", "tree_cone",    "tree_thin"     }, // pinheiro
            new[]{ "tree_oak",          "tree_fat",          "tree_detailed", "tree_blocks"   }, // carvalho
            new[]{ "tree_simple",       "tree_tall",         "tree_default"                   }, // outono/simples
        };

        private static void SpawnTree(GameObject parent, string name, float x, float z,
                                      float scale, int variant, System.Random rng)
        {
            int   v       = Mathf.Clamp(variant, 0, TREE_MODELS.Length - 1);
            var   models  = TREE_MODELS[v];
            string model  = models[rng.Next(models.Length)];
            float  yRot   = (float)rng.NextDouble() * 360f;

            // ── Tenta modelo Kenney ───────────────────────────────────────────────
            var go = KenneyAssetLoader.SpawnTree(model, parent,
                         new Vector3(x, 0f, z), scale);
            if (go != null)
            {
                go.name = name;
                return;
            }

            // ── Fallback: primitivos procedurais ──────────────────────────────────
            var tree = new GameObject(name);
            tree.transform.SetParent(parent.transform);
            tree.transform.position = new Vector3(x, 0f, z);
            tree.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 0.9f * scale, 0f);
            trunk.transform.localScale    = new Vector3(0.20f * scale, 0.9f * scale, 0.20f * scale);
            ApplyMaterial(trunk, TrunkColor, _texBark, 1f, 2f);

            Color folMain, folSec;
            switch (variant)
            {
                case 1:
                    folMain = FoliageColor; folSec = FoliageDark;
                    SpawnSphere(tree, 0f,           2.2f * scale, 0f,           1.5f * scale, folMain, _texFoliage);
                    SpawnSphere(tree, 0.5f * scale, 1.7f * scale, 0.3f * scale, 1.1f * scale, folSec,  _texFoliage);
                    SpawnSphere(tree, -0.4f* scale, 1.9f * scale, -0.3f* scale, 1.0f * scale, folMain, _texFoliage);
                    break;
                case 2:
                    folMain = FoliageYellow; folSec = new Color(0.65f, 0.38f, 0.05f);
                    SpawnSphere(tree, 0f,           2.5f * scale, 0f,           1.3f * scale, folMain, _texFoliage);
                    SpawnSphere(tree, 0.3f * scale, 2.0f * scale, 0.2f * scale, 0.9f * scale, folSec,  _texFoliage);
                    break;
                default:
                    folMain = FoliageColor; folSec = FoliageDark;
                    SpawnSphere(tree, 0f, 3.0f * scale, 0f, 1.1f * scale, folMain, _texFoliage);
                    SpawnSphere(tree, 0f, 2.2f * scale, 0f, 1.4f * scale, folSec,  _texFoliage);
                    SpawnSphere(tree, 0f, 1.6f * scale, 0f, 1.2f * scale, folMain, _texFoliage);
                    break;
            }
        }

        private static void SpawnSphere(GameObject parent, float lx, float ly, float lz,
                                        float size, Color color, Texture2D tex)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.transform.SetParent(parent.transform, false);
            s.transform.localPosition = new Vector3(lx, ly, lz);
            s.transform.localScale    = Vector3.one * size;
            ApplyMaterial(s, color, tex, 1f, 1f);
            RemoveCollider(s);
        }

        // ─── Rochas e nós de minério ──────────────────────────────────────────────

        private static void BuildRocks(GameObject root, System.Random rng)
        {
            var container = new GameObject("Rocks");
            container.transform.SetParent(root.transform);

            // Rochas normais
            int count = 0, attempts = 0;
            while (count < 45 && attempts < 600)
            {
                attempts++;
                float x = (float)(rng.NextDouble() * (MAP_W - 4f)) + 2f;
                float z = (float)(rng.NextDouble() * (MAP_H - 4f)) + 2f;

                float distCity    = Vector2.Distance(new Vector2(x, z), new Vector2(TOWN_X,    TOWN_Z));
                float distTrainer = Vector2.Distance(new Vector2(x, z), new Vector2(TRAINER_X, TRAINER_Z));
                float distLake    = Vector2.Distance(new Vector2(x, z), new Vector2(LAKE_X, LAKE_Z));
                if (distCity < SAFE_RADIUS - 2f || distTrainer < 5f || distLake < 5f) continue;

                bool cluster = rng.NextDouble() < 0.35f; // 35% chance de grupo de 2-3 rochas
                SpawnRock(container, $"Rock_{count}", x, z, rng);
                if (cluster)
                {
                    float ox = (float)(rng.NextDouble() - 0.5f) * 2f;
                    float oz = (float)(rng.NextDouble() - 0.5f) * 2f;
                    SpawnRock(container, $"Rock_{count}b", x + ox, z + oz, rng);
                }
                count++;
            }

            // Nós de minério (Albion tem recursos espalhados no mapa)
            int oreCount = 0;
            attempts = 0;
            while (oreCount < 12 && attempts < 300)
            {
                attempts++;
                float x = (float)(rng.NextDouble() * (MAP_W - 6f)) + 3f;
                float z = (float)(rng.NextDouble() * (MAP_H - 6f)) + 3f;

                float distCity = Vector2.Distance(new Vector2(x, z), new Vector2(TOWN_X, TOWN_Z));
                if (distCity < SAFE_RADIUS + 2f) continue;

                SpawnOreNode(container, $"Ore_{oreCount}", x, z, rng);
                oreCount++;
            }
        }

        // Modelos Kenney de rocha — alternados sequencialmente pelo rng
        private static readonly string[] ROCK_MODELS_LARGE = { "rock_largeA", "rock_largeB", "rock_largeC" };
        private static readonly string[] ROCK_MODELS_TALL  = { "rock_tallA",  "rock_tallB",  "rock_tallC"  };
        private static readonly string[] ROCK_MODELS_SMALL = { "rock_smallA", "rock_smallB", "rock_smallC" };

        private static void SpawnRock(GameObject parent, string name, float x, float z,
                                      System.Random rng)
        {
            float yRot = (float)rng.NextDouble() * 360f;
            float scale;
            string model;

            // Escolhe tamanho da rocha
            int sizeRoll = rng.Next(3);
            if (sizeRoll == 0)
            {
                model = ROCK_MODELS_TALL[rng.Next(ROCK_MODELS_TALL.Length)];
                scale = 0.5f + (float)rng.NextDouble() * 0.5f;
            }
            else if (sizeRoll == 1)
            {
                model = ROCK_MODELS_LARGE[rng.Next(ROCK_MODELS_LARGE.Length)];
                scale = 0.6f + (float)rng.NextDouble() * 0.6f;
            }
            else
            {
                model = ROCK_MODELS_SMALL[rng.Next(ROCK_MODELS_SMALL.Length)];
                scale = 0.8f + (float)rng.NextDouble() * 0.8f;
            }

            // ── Tenta modelo Kenney ───────────────────────────────────────────────
            var go = KenneyAssetLoader.SpawnRock(model, parent,
                         new Vector3(x, 0f, z), yRot, scale);
            if (go != null) { go.name = name; return; }

            // ── Fallback: primitivos procedurais ──────────────────────────────────
            var rock = new GameObject(name);
            rock.transform.SetParent(parent.transform);
            rock.transform.position = new Vector3(x, 0f, z);
            Color c = (rng.Next(2) == 0) ? RockColor : RockDark;
            float w = 0.5f + (float)rng.NextDouble() * 1.1f;
            float h = 0.3f + (float)rng.NextDouble() * 0.5f;
            float d = 0.4f + (float)rng.NextDouble() * 0.9f;
            var main = GameObject.CreatePrimitive(PrimitiveType.Cube);
            main.name = "RockMain";
            main.transform.SetParent(rock.transform, false);
            main.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            main.transform.localScale    = new Vector3(w, h, d);
            main.transform.rotation      = Quaternion.Euler(
                (float)rng.NextDouble() * 10f - 5f, yRot, (float)rng.NextDouble() * 10f - 5f);
            ApplyMaterial(main, c, _texStone, 1f, 1f);
        }

        private static void SpawnOreNode(GameObject parent, string name, float x, float z,
                                         System.Random rng)
        {
            // Nó de minério: rocha escura com cristal azul no topo
            var node = new GameObject(name);
            node.transform.SetParent(parent.transform);
            node.transform.position = new Vector3(x, 0f, z);

            // Base (rocha)
            var baseRock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseRock.name = "OreBase";
            baseRock.transform.SetParent(node.transform, false);
            baseRock.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            baseRock.transform.localScale    = new Vector3(0.7f, 0.44f, 0.7f);
            baseRock.transform.rotation      = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            ApplyMaterial(baseRock, new Color(0.25f, 0.25f, 0.28f), _texStone, 1f, 1f);
            // BoxCollider mantido

            // Cristal (cubo estreito e alto, azul)
            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "OreCrystal";
            crystal.transform.SetParent(node.transform, false);
            crystal.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            crystal.transform.localScale    = new Vector3(0.18f, 0.45f, 0.18f);
            crystal.transform.rotation      = Quaternion.Euler(0f, 45f, 15f);
            ApplyMaterial(crystal, OreColor, null, 1f, 1f, emissive: true);
            RemoveCollider(crystal);
        }

        // ─── Construções ──────────────────────────────────────────────────────────

        private static void BuildBlacksmithBuilding(GameObject root)
        {
            var container = new GameObject("Buildings");
            container.transform.SetParent(root.transform);

            // Ferreiro — noroeste da praça central
            float bx = TOWN_X - 5f, bz = TOWN_Z + 3f;
            SpawnBuilding(container, "Blacksmith", bx, bz, 4.5f, 2.8f, 4f, WallColor, RoofColor);

            // Bigorna decorativa
            var anvil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anvil.name = "Anvil";
            anvil.transform.SetParent(container.transform);
            anvil.transform.position   = new Vector3(bx + 2.8f, 0.3f, bz - 1.2f);
            anvil.transform.localScale = new Vector3(0.55f, 0.3f, 0.40f);
            ApplyMaterial(anvil, new Color(0.22f, 0.22f, 0.26f), _texStone, 1f, 1f);
            RemoveCollider(anvil);

            // Loja de armas — leste do ferreiro
            SpawnBuilding(container, "ArmorShop",
                TOWN_X + 4f, TOWN_Z + 4f, 3.5f, 2.5f, 3.5f,
                new Color(0.65f, 0.62f, 0.55f), new Color(0.35f, 0.20f, 0.08f));
        }

        private static void BuildTrainerBuilding(GameObject root)
        {
            var container = GameObject.Find("Buildings")?.transform ?? root.transform;

            // Área de treinamento
            SpawnBuilding(container.gameObject, "TrainerHall",
                TRAINER_X - 1f, TRAINER_Z + 3f, 4f, 2.5f, 3.5f,
                WallColor, new Color(0.35f, 0.22f, 0.08f));

            // Postes de treinamento (manequins)
            for (int i = 0; i < 3; i++)
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = $"TrainingPost_{i}";
                post.transform.SetParent(container.gameObject.transform);
                post.transform.position   = new Vector3(TRAINER_X - 2.5f + i * 2f, 0.8f, TRAINER_Z - 1f);
                post.transform.localScale = new Vector3(0.16f, 0.8f, 0.16f);
                ApplyMaterial(post, WoodColor, _texBark, 1f, 2f);
                RemoveCollider(post);

                // "Cabeça" do manequim
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Head";
                head.transform.SetParent(post.transform, false);
                head.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                head.transform.localScale    = Vector3.one * 0.6f;
                ApplyMaterial(head, new Color(0.65f, 0.50f, 0.35f), null, 1f, 1f);
                RemoveCollider(head);
            }
        }

        /// <summary>Cabanas espalhadas — dão aparência de vilarejo além da cidade central.</summary>
        private static void BuildCabins(GameObject root, System.Random rng)
        {
            var container = GameObject.Find("Buildings")?.transform ?? root.transform;

            (float x, float z)[] cabinPos =
            {
                (TOWN_X + 8f,  TOWN_Z + 6f),
                (TOWN_X - 7f,  TOWN_Z - 4f),
                (TOWN_X + 6f,  TOWN_Z - 6f),
                (8f,  28f),
                (38f, 24f),
                (14f, 8f),
            };

            for (int i = 0; i < cabinPos.Length; i++)
            {
                Color roofVar = (i % 2 == 0)
                    ? new Color(0.48f, 0.28f, 0.10f)
                    : new Color(0.35f, 0.22f, 0.30f);
                SpawnBuilding(container.gameObject, $"Cabin_{i}",
                    cabinPos[i].x, cabinPos[i].z,
                    3f + (float)(rng.NextDouble() * 1f),
                    2f + (float)(rng.NextDouble() * 0.5f),
                    3f + (float)(rng.NextDouble() * 1f),
                    new Color(0.60f, 0.56f, 0.48f), roofVar);
            }
        }

        private static void SpawnBuilding(GameObject parent, string name,
                                          float x, float z, float w, float h, float d,
                                          Color wallColor, Color roofColor)
        {
            var building = new GameObject(name);
            building.transform.SetParent(parent.transform);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Walls";
            body.transform.SetParent(building.transform, false);
            body.transform.position   = new Vector3(x, h * 0.5f, z);
            body.transform.localScale = new Vector3(w, h, d);
            ApplyMaterial(body, wallColor, _texStone, 2f, 1.5f);

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(building.transform, false);
            roof.transform.position   = new Vector3(x, h + 0.45f, z);
            roof.transform.localScale = new Vector3(w + 0.4f, 0.9f, d + 0.4f);
            roof.transform.rotation   = Quaternion.Euler(18f, 0f, 0f);
            ApplyMaterial(roof, roofColor, _texWood, 2f, 2f);
            RemoveCollider(roof);

            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";
            door.transform.SetParent(building.transform, false);
            door.transform.position   = new Vector3(x, 0.65f, z - d * 0.5f - 0.01f);
            door.transform.localScale = new Vector3(0.80f, 1.3f, 0.06f);
            ApplyMaterial(door, new Color(0.22f, 0.14f, 0.07f), _texWood, 1f, 1f);
            RemoveCollider(door);
        }

        // ─── Props decorativos ────────────────────────────────────────────────────

        private static void BuildProps(GameObject root, System.Random rng)
        {
            var container = new GameObject("Props");
            container.transform.SetParent(root.transform);

            (float x, float z)[] bonfiresPos =
            {
                (TOWN_X + 1.5f, TOWN_Z - 2f),
                (TRAINER_X + 1f, TRAINER_Z - 2.5f),
                (10f, 25f),
            };
            foreach (var (bx, bz) in bonfiresPos)
                SpawnBonfire(container, bx, bz);

            for (int i = 0; i < 3; i++)
            {
                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = $"Barrel_{i}";
                barrel.transform.SetParent(container.transform);
                barrel.transform.position   = new Vector3(TOWN_X - 3f + i * 0.7f, 0.4f, TOWN_Z + 1f);
                barrel.transform.localScale = new Vector3(0.30f, 0.4f, 0.30f);
                ApplyMaterial(barrel, WoodColor, _texWood, 1f, 1f);
                RemoveCollider(barrel);
            }

            (float x, float z)[] lampPos =
            {
                (TOWN_X - 2f, TOWN_Z),
                (TOWN_X + 2f, TOWN_Z),
                (TOWN_X,      TOWN_Z - 2f),
                (TOWN_X,      TOWN_Z + 2f),
            };
            foreach (var (lx, lz) in lampPos)
                SpawnLampPost(container, lx, lz);
        }

        private static void SpawnFountain(GameObject parent, float x, float z)
        {
            var fountain = new GameObject("Fountain");
            fountain.transform.SetParent(parent.transform);
            fountain.transform.position = new Vector3(x, 0f, z);

            var base1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            base1.name = "FountainBase";
            base1.transform.SetParent(fountain.transform, false);
            base1.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            base1.transform.localScale    = new Vector3(2.0f, 0.12f, 2.0f);
            ApplyMaterial(base1, StoneColor, _texStone, 1f, 1f);
            RemoveCollider(base1);

            var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "FountainWater";
            water.transform.SetParent(fountain.transform, false);
            water.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            water.transform.localScale    = new Vector3(1.6f, 0.01f, 1.6f);
            ApplyMaterial(water, WaterColor, _texWater, 1f, 1f);
            RemoveCollider(water);

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "FountainPillar";
            pillar.transform.SetParent(fountain.transform, false);
            pillar.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            pillar.transform.localScale    = new Vector3(0.2f, 0.5f, 0.2f);
            ApplyMaterial(pillar, StoneColor, _texStone, 1f, 1f);
            RemoveCollider(pillar);
        }

        private static void SpawnBonfire(GameObject parent, float x, float z)
        {
            // Tenta fogueira Kenney (campfire_stones ou campfire_logs)
            string model = KenneyAssetLoader.HasModel("NatureKit/props/campfire_stones")
                         ? "campfire_stones" : "campfire_logs";
            var go = KenneyAssetLoader.SpawnProp(model, parent, new Vector3(x, 0f, z),
                                                  yRot: 0f, scale: 0.8f);
            if (go != null) { go.name = "Bonfire"; return; }

            // Fallback: primitivos
            var bf = new GameObject("Bonfire");
            bf.transform.SetParent(parent.transform);
            bf.transform.position = new Vector3(x, 0f, z);

            var log1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log1.name = "Log1";
            log1.transform.SetParent(bf.transform, false);
            log1.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            log1.transform.localScale    = new Vector3(0.08f, 0.3f, 0.08f);
            log1.transform.rotation      = Quaternion.Euler(0f, 0f, 90f);
            ApplyMaterial(log1, WoodColor, _texBark, 1f, 1f);
            RemoveCollider(log1);

            var log2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log2.name = "Log2";
            log2.transform.SetParent(bf.transform, false);
            log2.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            log2.transform.localScale    = new Vector3(0.08f, 0.3f, 0.08f);
            log2.transform.rotation      = Quaternion.Euler(90f, 45f, 0f);
            ApplyMaterial(log2, new Color(0.30f, 0.18f, 0.06f), _texBark, 1f, 1f);
            RemoveCollider(log2);

            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Flame";
            flame.transform.SetParent(bf.transform, false);
            flame.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            flame.transform.localScale    = new Vector3(0.18f, 0.30f, 0.18f);
            ApplyMaterial(flame, new Color(1.0f, 0.55f, 0.05f), null, 1f, 1f, emissive: true);
            RemoveCollider(flame);
        }

        private static void SpawnLampPost(GameObject parent, float x, float z)
        {
            var lamp = new GameObject("LampPost");
            lamp.transform.SetParent(parent.transform);
            lamp.transform.position = new Vector3(x, 0f, z);

            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "PostPole";
            post.transform.SetParent(lamp.transform, false);
            post.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            post.transform.localScale    = new Vector3(0.06f, 1.2f, 0.06f);
            ApplyMaterial(post, new Color(0.30f, 0.28f, 0.25f), null, 1f, 1f);
            RemoveCollider(post);

            var globe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            globe.name = "LampGlobe";
            globe.transform.SetParent(lamp.transform, false);
            globe.transform.localPosition = new Vector3(0f, 2.55f, 0f);
            globe.transform.localScale    = Vector3.one * 0.18f;
            ApplyMaterial(globe, new Color(1.0f, 0.9f, 0.6f), null, 1f, 1f, emissive: true);
            RemoveCollider(globe);
        }

        // ─── Muros de borda ───────────────────────────────────────────────────────

        private static void BuildBoundary(GameObject root)
        {
            var container = new GameObject("Boundary");
            container.transform.SetParent(root.transform);

            const float wallH = 1.5f;
            const float wallT = 0.6f;

            CreateWall(container, "WallS", MAP_W * 0.5f, wallH * 0.5f, -wallT * 0.5f, MAP_W + wallT, wallH, wallT);
            CreateWall(container, "WallN", MAP_W * 0.5f, wallH * 0.5f, MAP_H + wallT * 0.5f, MAP_W + wallT, wallH, wallT);
            CreateWall(container, "WallW", -wallT * 0.5f, wallH * 0.5f, MAP_H * 0.5f, wallT, wallH, MAP_H + wallT);
            CreateWall(container, "WallE", MAP_W + wallT * 0.5f, wallH * 0.5f, MAP_H * 0.5f, wallT, wallH, MAP_H + wallT);
        }

        private static void CreateWall(GameObject parent, string name,
                                       float x, float y, float z, float w, float h, float d)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent.transform);
            wall.transform.position   = new Vector3(x, y, z);
            wall.transform.localScale = new Vector3(w, h, d);
            ApplyMaterial(wall, BoundaryColor, _texStone, 6f, 1f);
        }

        // ─── Iluminação e atmosfera ───────────────────────────────────────────────

        private static void SetupLightingAndAtmosphere()
        {
            Light dirLight = null;
            var existingLight = Object.FindAnyObjectByType<Light>();
            if (existingLight != null && existingLight.type == LightType.Directional)
                dirLight = existingLight;
            else
            {
                var lightGO = new GameObject("Directional Light");
                dirLight = lightGO.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }

            dirLight.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            dirLight.color              = new Color(1.00f, 0.95f, 0.85f);
            dirLight.intensity          = 1.3f;
            dirLight.shadows            = LightShadows.Soft;
            dirLight.shadowStrength     = 0.75f;

            RenderSettings.ambientMode         = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.55f, 0.70f, 0.90f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.52f, 0.45f);
            RenderSettings.ambientGroundColor  = new Color(0.15f, 0.18f, 0.12f);

            RenderSettings.fog              = true;
            RenderSettings.fogMode          = FogMode.Linear;
            RenderSettings.fogColor         = new Color(0.65f, 0.75f, 0.70f);
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance   = 80f;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static GameObject CreateFlatRect(GameObject parent, string name,
                                                  Vector3 pos, float w, float d,
                                                  Color color, Texture2D tex,
                                                  float tilingX, float tilingY)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(w / 10f, 1f, d / 10f);
            ApplyMaterial(go, color, tex, tilingX, tilingY);
            return go;
        }

        private static void ApplyMaterial(GameObject go, Color color, Texture2D tex = null,
                                          float tilingX = 1f, float tilingY = 1f,
                                          bool emissive = false)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;

            if (tex != null)
            {
                mat.mainTexture        = tex;
                mat.mainTextureScale   = new Vector2(tilingX, tilingY);
            }

            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.8f);
            }

            mat.SetFloat("_Smoothness", 0.15f);
            mat.SetFloat("_Metallic",   0f);
            rend.material = mat;
        }

        private static void RemoveCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

    }
}
