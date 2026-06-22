// ProceduralTextureGen.cs
// Gera texturas 2D em runtime usando ruído Perlin — sem assets externos.
// Usada pelo MapGenerator para dar aparência visual rica ao terreno e objetos.
//
// Texturas disponíveis:
//   Grass(seed)   → verde com variação natural (Perlin 2D)
//   Dirt(seed)    → terra batida para caminhos
//   Stone(seed)   → pedra cinza com manchas
//   Bark(seed)    → casca de árvore (linhas verticais)
//   Water(seed)   → água azul com reflexo simulado
//   Foliage(seed) → folhagem com manchas escuras/claras

using UnityEngine;

namespace MMORPG.World
{
    public static class ProceduralTextureGen
    {
        // ─── Tamanho padrão das texturas ──────────────────────────────────────────
        private const int TEX_SIZE = 128; // 128×128 é suficiente para primitivos

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>Textura de grama verde com variação Perlin.</summary>
        public static Texture2D Grass(int seed = 0)
        {
            Color baseColor = new Color(0.28f, 0.52f, 0.18f);
            Color darkColor = new Color(0.18f, 0.38f, 0.10f);
            Color lightColor = new Color(0.38f, 0.62f, 0.25f);
            return PerlinTex(TEX_SIZE, seed, baseColor, darkColor, lightColor,
                             freq1: 8f, freq2: 20f, blendWeight: 0.35f);
        }

        /// <summary>Textura de terra/caminho (marrom com pedrinhas).</summary>
        public static Texture2D Dirt(int seed = 1)
        {
            Color baseColor  = new Color(0.54f, 0.44f, 0.30f);
            Color darkColor  = new Color(0.42f, 0.33f, 0.22f);
            Color lightColor = new Color(0.64f, 0.54f, 0.40f);
            return PerlinTex(TEX_SIZE, seed, baseColor, darkColor, lightColor,
                             freq1: 10f, freq2: 30f, blendWeight: 0.25f);
        }

        /// <summary>Textura de pedra cinza com manchas.</summary>
        public static Texture2D Stone(int seed = 2)
        {
            Color baseColor  = new Color(0.52f, 0.50f, 0.47f);
            Color darkColor  = new Color(0.35f, 0.34f, 0.32f);
            Color lightColor = new Color(0.68f, 0.66f, 0.62f);
            return PerlinTex(TEX_SIZE, seed, baseColor, darkColor, lightColor,
                             freq1: 6f, freq2: 22f, blendWeight: 0.40f);
        }

        /// <summary>Textura de casca de árvore (linhas verticais marrom).</summary>
        public static Texture2D Bark(int seed = 3)
        {
            var tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGB24, false);
            Color dark  = new Color(0.28f, 0.16f, 0.06f);
            Color light = new Color(0.46f, 0.30f, 0.14f);

            float offsetX = seed * 7.39f;
            float offsetY = seed * 3.17f;

            for (int y = 0; y < TEX_SIZE; y++)
            {
                for (int x = 0; x < TEX_SIZE; x++)
                {
                    // Ruído dominante na direção X (cria linhas verticais)
                    float nx = Mathf.PerlinNoise(x * 0.15f + offsetX, y * 0.05f + offsetY);
                    float ny = Mathf.PerlinNoise(x * 0.05f + offsetX + 50f, y * 0.10f + offsetY);
                    float n  = nx * 0.7f + ny * 0.3f;
                    tex.SetPixel(x, y, Color.Lerp(dark, light, n));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        /// <summary>Textura de água azul com efeito de reflexo simulado.</summary>
        public static Texture2D Water(int seed = 4)
        {
            Color deepColor    = new Color(0.12f, 0.38f, 0.65f);
            Color shallowColor = new Color(0.28f, 0.58f, 0.85f);
            Color highlight    = new Color(0.60f, 0.80f, 1.00f);
            return PerlinTex(TEX_SIZE, seed, shallowColor, deepColor, highlight,
                             freq1: 5f, freq2: 12f, blendWeight: 0.50f);
        }

        /// <summary>Textura de folhagem verde com manchas escuras/claras.</summary>
        public static Texture2D Foliage(int seed = 5)
        {
            Color baseColor  = new Color(0.20f, 0.48f, 0.14f);
            Color darkColor  = new Color(0.10f, 0.30f, 0.06f);
            Color lightColor = new Color(0.30f, 0.60f, 0.20f);
            return PerlinTex(TEX_SIZE, seed, baseColor, darkColor, lightColor,
                             freq1: 12f, freq2: 28f, blendWeight: 0.45f);
        }

        /// <summary>Textura de telhado/madeira (marrom avermelhado com grão).</summary>
        public static Texture2D Wood(int seed = 6)
        {
            Color dark  = new Color(0.32f, 0.18f, 0.06f);
            Color light = new Color(0.55f, 0.35f, 0.16f);
            return PerlinTex(TEX_SIZE, seed, light, dark, light,
                             freq1: 4f, freq2: 18f, blendWeight: 0.30f);
        }

        // ─── Gerador base via Perlin ──────────────────────────────────────────────

        /// <summary>
        /// Gera textura com duas frequências de ruído Perlin misturadas.
        /// freq1: ruído de baixa frequência (variações largas de cor)
        /// freq2: ruído de alta frequência (detalhe/granularidade)
        /// blendWeight: quanto o detalhe (freq2) pesa no resultado final
        /// </summary>
        private static Texture2D PerlinTex(int size, int seed,
                                           Color baseColor, Color darkColor, Color lightColor,
                                           float freq1, float freq2, float blendWeight)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);

            float offsetX = seed * 13.71f;
            float offsetY = seed * 8.53f;
            float invSize = 1f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ux = x * invSize;
                    float uy = y * invSize;

                    // Ruído largo (determina tom geral)
                    float n1 = Mathf.PerlinNoise(ux * freq1 + offsetX, uy * freq1 + offsetY);

                    // Ruído fino (adiciona detalhe)
                    float n2 = Mathf.PerlinNoise(ux * freq2 + offsetX + 100f,
                                                  uy * freq2 + offsetY + 100f);

                    float combined = Mathf.Lerp(n1, n2, blendWeight);

                    // Mapeia 0–1 para dark→base→light
                    Color c;
                    if (combined < 0.45f)
                        c = Color.Lerp(darkColor, baseColor, combined / 0.45f);
                    else
                        c = Color.Lerp(baseColor, lightColor, (combined - 0.45f) / 0.55f);

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }
    }
}
