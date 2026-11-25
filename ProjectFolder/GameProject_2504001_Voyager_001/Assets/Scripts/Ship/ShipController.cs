using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// ShipGrid를 Unity 씬에 연결한다.
    /// - 발판을 큐브로 표시
    /// - 밤에는 설치 금지 (03_06_시간 시스템)
    /// - 규칙 3: 파괴된 발판 위의 CellOccupant를 파괴 (03_09_발판 격리 시스템)
    /// - 엔진 발판이 파괴되면 게임 오버
    /// </summary>
    [RequireComponent(typeof(TimeSystem))]
    public class ShipController : MonoBehaviour
    {
        public ShipGrid Grid { get; } = new ShipGrid();
        public bool IsGameOver { get; private set; }
        public event Action GameOver;

        TimeSystem timeSystem;
        readonly Dictionary<Vector2Int, GameObject> platformViews = new Dictionary<Vector2Int, GameObject>();
        GameObject engineView;
        Material normalMaterial;
        Material destructibleMaterial;
        Material engineMaterial;

        void Awake()
        {
            timeSystem = GetComponent<TimeSystem>();
            normalMaterial = MakeMaterial(new Color(0.55f, 0.55f, 0.6f));
            destructibleMaterial = MakeMaterial(new Color(0.85f, 0.55f, 0.2f));
            engineMaterial = MakeMaterial(new Color(0.8f, 0.15f, 0.15f));

            Grid.PlatformsDestroyed += OnPlatformsDestroyed;
            Grid.EngineDestroyed += OnEngineDestroyed;
        }

        void OnDestroy()
        {
            Grid.PlatformsDestroyed -= OnPlatformsDestroyed;
            Grid.EngineDestroyed -= OnEngineDestroyed;
        }

        /// <summary>테스트용 초기 함선. 일반 발판 5x3, 오른쪽에 파괴 가능한 발판 팔, 엔진은 (1,1).</summary>
        public void BuildDemoLayout()
        {
            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 3; z++)
                    AddPlatform(new Vector2Int(x, z), PlatformType.Normal);

            for (int x = 5; x < 9; x++)
                AddPlatform(new Vector2Int(x, 1), PlatformType.Destructible);

            Grid.SetEngine(new Vector2Int(1, 1));
            CreateEngineView(new Vector2Int(1, 1));
        }

        /// <summary>플레이어 설치. 밤에는 불가, 설치 규칙(발판 옆)도 만족해야 한다.</summary>
        public bool TryBuild(Vector2Int cell, PlatformType type)
        {
            if (IsGameOver) return false;
            if (timeSystem.IsNight) return false;
            if (!Grid.TryPlace(cell, type)) return false;
            CreateView(cell, type);
            return true;
        }

        /// <summary>파괴는 낮/밤 상관없이 가능. (03_06)</summary>
        public bool DestroyCell(Vector2Int cell)
        {
            if (IsGameOver) return false;
            return Grid.Destroy(cell);
        }

        void AddPlatform(Vector2Int cell, PlatformType type)
        {
            Grid.AddUnchecked(cell, type);
            CreateView(cell, type);
        }

        void OnPlatformsDestroyed(IReadOnlyList<Vector2Int> cells)
        {
            var destroyedSet = new HashSet<Vector2Int>(cells);

            // 화면 갱신
            foreach (var cell in cells)
            {
                if (platformViews.TryGetValue(cell, out var view))
                {
                    Destroy(view);
                    platformViews.Remove(cell);
                }
            }
            if (engineView != null && destroyedSet.Contains(GridMath.WorldToCell(engineView.transform.position)))
            {
                Destroy(engineView);
                engineView = null;
            }

            // 규칙 3: 파괴된 발판 위의 캐릭터 / 건물도 전부 파괴
            var occupants = FindObjectsByType<CellOccupant>(FindObjectsSortMode.None);
            foreach (var occupant in occupants)
            {
                if (destroyedSet.Contains(occupant.Cell)) Destroy(occupant.gameObject);
            }
        }

        void OnEngineDestroyed()
        {
            IsGameOver = true;
            UnityEngine.Debug.Log("[Voyager] 엔진이 파괴되었습니다. 게임 오버");
            GameOver?.Invoke();
        }

        void CreateView(Vector2Int cell, PlatformType type)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"{type} {cell.x},{cell.y}";
            go.transform.SetParent(transform, false);
            go.transform.position = GridMath.CellToWorld(cell) + Vector3.down * 0.1f;
            go.transform.localScale = new Vector3(0.95f, 0.2f, 0.95f);
            go.GetComponent<Renderer>().sharedMaterial = type == PlatformType.Normal ? normalMaterial : destructibleMaterial;
            RemoveCollider(go);
            platformViews[cell] = go;
        }

        void CreateEngineView(Vector2Int cell)
        {
            engineView = GameObject.CreatePrimitive(PrimitiveType.Cube);
            engineView.name = "Engine";
            engineView.transform.SetParent(transform, false);
            engineView.transform.position = GridMath.CellToWorld(cell) + Vector3.up * 0.3f;
            engineView.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            engineView.GetComponent<Renderer>().sharedMaterial = engineMaterial;
            RemoveCollider(engineView);
        }

        static void RemoveCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            return material;
        }
    }
}
