using System;
using System.Collections.Generic;
using UnityEngine;

namespace RougeLike.Battle
{
    public enum PieceCollider { None, Box, Convex }

    /// <summary>One prop placed on the field. Pieces with a collider are obstacles and cover.</summary>
    [Serializable]
    public class ArenaPiece
    {
        public GameObject prefab;
        [Tooltip("x and z on the field; y is added to the ground height there.")]
        public Vector3 position;
        public float yaw;
        public float scale = 1f;
        public PieceCollider collider = PieceCollider.Box;
        [Tooltip("Above 0 the piece is loose: units shove it and boulders knock it flying.")]
        public float mass;
    }

    /// <summary>A rounded hill. Height falls off smoothly from the flat top to the edge of the radius.</summary>
    [Serializable]
    public class TerrainMound
    {
        public Vector2 center;
        public Vector2 radius = new(3f, 2f);
        [Tooltip("Negative digs a hollow.")]
        public float height = 0.5f;
        [Range(0f, 0.9f)] public float flatTop = 0.25f;
    }

    /// <summary>
    /// A bottomless pit. A unit knocked into one is gone. Units steer round them while walking, so
    /// it takes a shove (a lunge, a spit, a boulder) to send someone over the edge.
    /// </summary>
    [Serializable]
    public class ArenaHole
    {
        public Vector2 center;
        public Vector2 radius = new(0.8f, 0.8f);
        [Tooltip("2 is an oval; higher gets boxier (a dug saw pit).")]
        [Range(2f, 8f)] public float squareness = 2f;

        /// <summary>Distance from the centre in hole radii: under 1 is inside.</summary>
        public float Distance(float x, float z)
        {
            float dx = Mathf.Abs(x - center.x) / Mathf.Max(0.01f, radius.x);
            float dz = Mathf.Abs(z - center.y) / Mathf.Max(0.01f, radius.y);
            return Mathf.Pow(Mathf.Pow(dx, squareness) + Mathf.Pow(dz, squareness), 1f / squareness);
        }
    }

    /// <summary>A shallow brook running across the field (along x). Wading through it is slow.</summary>
    [Serializable]
    public class ArenaStream
    {
        [Tooltip("Where it crosses the field, as z.")]
        public float z;
        public float halfWidth = 0.8f;
        [Tooltip("How far the banks slope down to the bed.")]
        public float bank = 0.7f;
        public float depth = 0.35f;
        public float meander = 0.3f, meanderFrequency = 0.45f;
        [Tooltip("Water surface height. Keep it above the forest floor so the brook shows past the board.")]
        public float waterLevel = -0.08f;
        [Tooltip("Walking speed while wading, as a share of normal.")]
        [Range(0.1f, 1f)] public float wadeSpeed = 0.4f;
        public Color waterColor = new(0.435f, 0.612f, 0.62f);
        [Tooltip("x of each crossing; the bed is filled in under it so units walk over flat.")]
        public List<float> bridges = new();
        public float bridgeHalfWidth = 0.6f;

        public float CenterZ(float x) => z + meander * Mathf.Sin(x * meanderFrequency);
    }

    /// <summary>
    /// A battlefield: the board's ground and terrain, obstacles on it, and the woodland around it.
    /// BattleManager picks one per battle and Arena builds it.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Arena")]
    public class ArenaDefinition : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Half-depth of the strip in the middle where nobody can be placed. Terrain belongs here.")]
        public float noMansLand = 1.2f;
        public bool centreLine = true;

        [Header("Look")]
        public Color boardColor = new(0.918f, 0.851f, 0.69f);
        public Color rimColor = new(0.804f, 0.71f, 0.51f);
        public Color floorColor = new(0.31f, 0.42f, 0.227f);

        [Header("Terrain")]
        public List<TerrainMound> mounds = new();
        public List<ArenaStream> streams = new();
        [Tooltip("Pits units can be knocked into. Keep them out of the placement zones.")]
        public List<ArenaHole> holes = new();
        public List<ArenaPiece> pieces = new();

        [Header("Woodland")]
        public List<GameObject> trees = new();
        public List<GameObject> smallProps = new();
        public int dressingSeed = 7;
    }
}
