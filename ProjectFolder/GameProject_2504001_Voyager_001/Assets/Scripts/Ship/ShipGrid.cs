using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    public enum PlatformType
    {
        Normal,       // 발판
        Destructible, // 파괴 가능한 발판
    }

    /// <summary>
    /// 함선의 발판 격자. MonoBehaviour가 아닌 순수 데이터 + 규칙 클래스.
    /// 03_09_발판 격리 시스템의 규칙 1, 2를 여기서 처리한다.
    /// (규칙 3 "발판 위 대상 파괴"는 PlatformsDestroyed 이벤트를 받는 쪽이 처리한다.)
    /// </summary>
    public class ShipGrid
    {
        readonly Dictionary<Vector2Int, PlatformType> platforms = new Dictionary<Vector2Int, PlatformType>();

        public bool HasEngine { get; private set; }
        public Vector2Int EngineCell { get; private set; }
        public int Count => platforms.Count;
        public IEnumerable<KeyValuePair<Vector2Int, PlatformType>> All => platforms;

        /// <summary>한 번의 파괴로 사라진 모든 칸 (직접 파괴 + 규칙 1 + 규칙 2). 상태는 이미 갱신된 뒤에 호출된다.</summary>
        public event Action<IReadOnlyList<Vector2Int>> PlatformsDestroyed;

        /// <summary>엔진이 놓인 발판이 파괴되었을 때. (게임 오버)</summary>
        public event Action EngineDestroyed;

        public bool Has(Vector2Int cell) => platforms.ContainsKey(cell);

        public bool TryGetType(Vector2Int cell, out PlatformType type) => platforms.TryGetValue(cell, out type);

        /// <summary>초기 배치용. 설치 규칙을 검사하지 않는다.</summary>
        public void AddUnchecked(Vector2Int cell, PlatformType type)
        {
            platforms[cell] = type;
        }

        /// <summary>엔진을 발판 위에 놓는다. 그 칸에 발판이 있어야 한다.</summary>
        public bool SetEngine(Vector2Int cell)
        {
            if (!Has(cell)) return false;
            HasEngine = true;
            EngineCell = cell;
            return true;
        }

        /// <summary>
        /// 설치 가능 조건: 빈 칸이고, 4칸 기준으로 인접한 칸에 발판이 하나라도 있어야 한다.
        /// (건물 시스템의 "발판 옆에 설치" - 옆을 4칸 인접으로 해석)
        /// </summary>
        public bool CanPlace(Vector2Int cell)
        {
            if (Has(cell)) return false;
            foreach (var d in GridMath.Neighbors4)
            {
                if (Has(cell + d)) return true;
            }
            return false;
        }

        public bool TryPlace(Vector2Int cell, PlatformType type)
        {
            if (!CanPlace(cell)) return false;
            platforms[cell] = type;
            return true;
        }

        /// <summary>
        /// 발판을 파괴한다. 처리 순서: 직접 파괴 → 규칙 1(연쇄) → 규칙 2(엔진과 끊긴 발판) → 이벤트 발행.
        /// 반환값: 파괴가 일어났는지.
        /// </summary>
        public bool Destroy(Vector2Int cell)
        {
            if (!platforms.TryGetValue(cell, out var type)) return false;

            var destroyed = new List<Vector2Int>();
            Remove(cell, destroyed);

            // 규칙 1: 파괴 가능한 발판이 파괴되면, 4칸 기준 인접한 파괴 가능한 발판도 파괴 (연쇄)
            if (type == PlatformType.Destructible)
            {
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(cell);
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var d in GridMath.Neighbors4)
                    {
                        var next = current + d;
                        if (platforms.TryGetValue(next, out var nextType) && nextType == PlatformType.Destructible)
                        {
                            Remove(next, destroyed);
                            queue.Enqueue(next);
                        }
                    }
                }
            }

            bool engineLost = HasEngine && destroyed.Contains(EngineCell);

            if (engineLost)
            {
                // 엔진이 파괴되면 규칙 2가 아니라 게임 오버로 처리
                HasEngine = false;
            }
            else if (HasEngine)
            {
                // 규칙 2: 엔진과 (4칸 인접 경로로) 이어지지 않은 발판은 전부 파괴
                var reachable = FindConnectedToEngine();
                var orphans = new List<Vector2Int>();
                foreach (var key in platforms.Keys)
                {
                    if (!reachable.Contains(key)) orphans.Add(key);
                }
                foreach (var orphan in orphans) Remove(orphan, destroyed);
            }

            PlatformsDestroyed?.Invoke(destroyed);
            if (engineLost) EngineDestroyed?.Invoke();
            return true;
        }

        void Remove(Vector2Int cell, List<Vector2Int> destroyed)
        {
            if (platforms.Remove(cell)) destroyed.Add(cell);
        }

        /// <summary>엔진 칸에서 4칸 인접 경로로 도달 가능한 모든 발판 (BFS).</summary>
        HashSet<Vector2Int> FindConnectedToEngine()
        {
            var visited = new HashSet<Vector2Int>();
            if (!Has(EngineCell)) return visited;

            var queue = new Queue<Vector2Int>();
            visited.Add(EngineCell);
            queue.Enqueue(EngineCell);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var d in GridMath.Neighbors4)
                {
                    var next = current + d;
                    if (platforms.ContainsKey(next) && visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited;
        }
    }
}
