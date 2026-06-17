// GroundSampler.cs
// Utilitário estático para encontrar a altura do terreno em qualquer posição (x, z).
//
// Por que raycast e não Terrain.SampleHeight()?
//   Terrain.SampleHeight() só funciona com o componente Unity Terrain.
//   Raycast funciona com qualquer geometria: Terrain, meshes customizados, tiles 3D —
//   permitindo mudar o tipo de terreno no futuro sem alterar este script.
//
// Layer "Ground":
//   Crie uma Layer chamada "Ground" no Unity e aplique a todos os objetos de terreno.
//   Isso impede que o raycast acerte personagens, projéteis, etc.
//   Se a layer não existir, o sampler usa todas as layers como fallback (menos eficiente).
//
// Uso:
//   float y = GroundSampler.GetHeight(10f, 5f);
//   Vector3 pos = GroundSampler.Snap(new Vector3(10f, 0f, 5f));

using UnityEngine;

namespace MMORPG.World
{
    public static class GroundSampler
    {
        // Altura a partir da qual o raycast começa (bem acima do terreno mais alto possível)
        // Se seu terreno tiver picos acima de 200u Unity, aumente este valor
        private const float RAY_ORIGIN_Y = 500f;

        // Distância máxima do raycast. 510 = 500 de offset + 10 de margem abaixo do zero
        private const float RAY_DISTANCE = 510f;

        // Cache da layer mask para não recalcular a cada frame
        private static int? _groundLayerMask;

        private static int GroundLayerMask
        {
            get
            {
                if (_groundLayerMask.HasValue) return _groundLayerMask.Value;

                int layer = LayerMask.NameToLayer("Ground");

                if (layer == -1)
                {
                    // Layer "Ground" não existe — avisa uma vez e usa tudo (menos ideal)
                    Debug.LogWarning("[GroundSampler] Layer 'Ground' não encontrada. " +
                                     "Crie uma Layer chamada 'Ground' e aplique ao terreno. " +
                                     "Usando todas as layers como fallback.");
                    _groundLayerMask = Physics.AllLayers;
                }
                else
                {
                    _groundLayerMask = 1 << layer;
                }

                return _groundLayerMask.Value;
            }
        }

        /// <summary>
        /// Retorna a altura do terreno na posição (x, z) em unidades Unity.
        /// Se não houver terreno abaixo, retorna 0 (chão padrão).
        /// </summary>
        public static float GetHeight(float x, float z)
        {
            var ray = new Ray(new Vector3(x, RAY_ORIGIN_Y, z), Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit hit, RAY_DISTANCE, GroundLayerMask))
                return hit.point.y;

            // Fallback: sem terreno detectado — assume nível do mar (y=0)
            // Isso pode acontecer fora dos limites do mapa ou antes do terreno ser carregado
            return 0f;
        }

        /// <summary>
        /// Retorna o vetor com Y ajustado para a altura do terreno na posição (x, z).
        /// Mantém X e Z intactos.
        /// </summary>
        public static Vector3 Snap(Vector3 position)
            => new Vector3(position.x, GetHeight(position.x, position.z), position.z);

        /// <summary>
        /// Versão com offset vertical — útil para colocar o pivô do personagem
        /// ligeiramente acima do chão e evitar z-fighting visual.
        /// offset típico: 0.05f unidades Unity
        /// </summary>
        public static Vector3 SnapWithOffset(Vector3 position, float verticalOffset = 0.05f)
            => new Vector3(position.x, GetHeight(position.x, position.z) + verticalOffset, position.z);

        /// <summary>
        /// Converte coordenadas do servidor (pixels) para Unity (unidades) com altura do terreno.
        /// serverX, serverY: posição em pixels (sistema do servidor)
        /// scale: divisor de conversão (padrão 50 — 2400px / 50 = 48u Unity)
        /// </summary>
        public static Vector3 ServerToUnity(float serverX, float serverY, float scale = 50f)
        {
            float x = serverX / scale;
            float z = serverY / scale;
            float y = GetHeight(x, z);
            return new Vector3(x, y, z);
        }
    }
}
