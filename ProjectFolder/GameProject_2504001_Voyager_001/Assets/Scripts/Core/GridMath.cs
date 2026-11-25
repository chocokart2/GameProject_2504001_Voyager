using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// 용어집의 "칸 좌표", "8칸 기준 인접", "4칸 기준 인접"을 코드로 옮긴 것.
    /// 칸 좌표 (x, z)는 Vector2Int의 (x, y)에 저장한다. (y가 월드의 Z축)
    /// </summary>
    public static class GridMath
    {
        /// <summary>4칸 기준 인접: 상하좌우.</summary>
        public static readonly Vector2Int[] Neighbors4 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };

        /// <summary>8칸 기준 인접: 상하좌우 + 대각선.</summary>
        public static readonly Vector2Int[] Neighbors8 =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1),
            new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        /// <summary>월드 좌표 → 칸 좌표. (floor(x), floor(z))</summary>
        public static Vector2Int WorldToCell(Vector3 world)
        {
            return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z));
        }

        /// <summary>칸 좌표 → 그 칸 중심의 월드 좌표. (칸 크기 1)</summary>
        public static Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(cell.x + 0.5f, 0f, cell.y + 0.5f);
        }

        /// <summary>4칸 기준 인접: dx + dz = 1</summary>
        public static bool IsAdjacent4(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dz = Mathf.Abs(a.y - b.y);
            return dx + dz == 1;
        }

        /// <summary>8칸 기준 인접: max(dx, dz) = 1 (자기 자신 제외)</summary>
        public static bool IsAdjacent8(Vector2Int a, Vector2Int b)
        {
            int dx = Mathf.Abs(a.x - b.x);
            int dz = Mathf.Abs(a.y - b.y);
            return Mathf.Max(dx, dz) == 1;
        }
    }
}
