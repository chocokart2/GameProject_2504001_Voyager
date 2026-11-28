using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// ShipGrid(바닥)와 ShipBuildings(위층)를 Unity 씬에 연결한다.
    /// - 바닥과 건물을 도형으로 표시
    /// - 설치 규칙 (03_03_건물 시스템, 03_06_시간 시스템: 밤에는 설치 금지)
    /// - 규칙 3: 파괴된 발판 위의 건물과 CellOccupant를 파괴 (03_09_발판 격리 시스템)
    /// - 엔진이 사라지면 게임 오버 (03_12_게임 오버 시스템)
    /// </summary>
    [RequireComponent(typeof(TimeSystem))]
    public class ShipController : MonoBehaviour
    {
        public ShipGrid Grid { get; } = new ShipGrid();
        public ShipBuildings Buildings { get; } = new ShipBuildings();
        public bool IsGameOver { get; private set; }
        public event Action GameOver;

        TimeSystem timeSystem;
        VisionSystem vision;
        readonly Dictionary<Vector2Int, GameObject> platformViews = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<Vector2Int, GameObject> buildingViews = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<BuildingType, Material> materials = new Dictionary<BuildingType, Material>();

        void Awake()
        {
            timeSystem = GetComponent<TimeSystem>();
            // 이미 씬에 있는 오브젝트에는 RequireComponent가 자동으로 붙지 않으므로 직접 보장한다
            vision = GetComponent<VisionSystem>();
            if (vision == null) vision = gameObject.AddComponent<VisionSystem>();

            Grid.PlatformsDestroyed += OnPlatformsDestroyed;
            Grid.EngineDestroyed += TriggerGameOver;
            Buildings.Placed += CreateBuildingView;
            Buildings.Removed += OnBuildingRemoved;
            Buildings.StateChanged += UpdateBuildingView;
        }

        void OnDestroy()
        {
            Grid.PlatformsDestroyed -= OnPlatformsDestroyed;
            Grid.EngineDestroyed -= TriggerGameOver;
            Buildings.Placed -= CreateBuildingView;
            Buildings.Removed -= OnBuildingRemoved;
            Buildings.StateChanged -= UpdateBuildingView;
        }

        /// <summary>테스트용 초기 함선. 일반 발판 5x3, 오른쪽에 파괴 가능한 발판 팔, 엔진은 (1,1).</summary>
        public void BuildDemoLayout()
        {
            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 3; z++)
                    AddPlatform(new Vector2Int(x, z), PlatformType.Normal);

            for (int x = 5; x < 9; x++)
                AddPlatform(new Vector2Int(x, 1), PlatformType.Destructible);

            var engineCell = new Vector2Int(1, 1);
            Grid.SetEngine(engineCell); // 규칙 2의 기준점
            Buildings.TryPlace(Grid, engineCell, BuildingType.Engine, out _);
        }

        // ───────────── 설치 ─────────────

        /// <summary>
        /// builderCell에 있는 플레이어가 아이템으로 cell에 건물을 설치할 수 있는지. 안 되면 reason에 이유.
        /// 설치는 플레이어 시야 안의 칸에만 가능하다 (03_03 공통 규칙, 03_10_시야 시스템).
        /// </summary>
        public bool CanBuild(Vector2Int builderCell, Vector2Int cell, BuildingType type, out string reason)
        {
            var definition = BuildingCatalog.Get(type);

            if (IsGameOver) { reason = "게임 오버"; return false; }
            if (timeSystem.IsNight && !definition.BuildableAtNight) { reason = $"밤에는 {definition.DisplayName}을(를) 설치할 수 없음"; return false; }
            if (!definition.HasItem) { reason = $"{definition.DisplayName}은(는) 설치용 아이템이 없음"; return false; }
            if (!vision.PlayerCanSee(builderCell, cell)) { reason = "시야 밖의 칸에는 설치할 수 없음"; return false; }

            if (definition.Layer == BuildingLayer.Floor)
            {
                if (!Grid.CanPlace(cell)) { reason = "바닥은 기존 바닥 옆 빈 칸에만 설치 가능"; return false; }
            }
            else
            {
                if (!Grid.Has(cell)) { reason = "바닥 위에만 설치 가능"; return false; }
                if (Buildings.Has(cell)) { reason = "이미 건물이 있음"; return false; }
                if (!definition.Passable && HasCharacterAt(cell)) { reason = "캐릭터가 있는 칸에는 설치 불가"; return false; }
            }

            reason = null;
            return true;
        }

        public bool TryBuild(Vector2Int builderCell, Vector2Int cell, BuildingType type) => TryBuild(builderCell, cell, type, out _);

        public bool TryBuild(Vector2Int builderCell, Vector2Int cell, BuildingType type, out string reason)
        {
            if (!CanBuild(builderCell, cell, type, out reason)) return false;

            if (BuildingCatalog.TryGetPlatformType(type, out var platformType))
            {
                Grid.TryPlace(cell, platformType);
                CreatePlatformView(cell, platformType);
                return true;
            }
            return Buildings.TryPlace(Grid, cell, type, out _);
        }

        // ───────────── 파괴 / 피해 ─────────────

        /// <summary>바닥을 즉시 파괴한다. 파괴는 낮/밤 상관없이 가능. (03_06)</summary>
        public bool DestroyCell(Vector2Int cell)
        {
            if (IsGameOver) return false;
            return Grid.Destroy(cell);
        }

        /// <summary>바닥의 내구도를 깎는다. 0이 되면 파괴 (규칙 1, 2, 3 적용).</summary>
        public bool DamagePlatform(Vector2Int cell, int amount)
        {
            if (IsGameOver) return false;
            return Grid.Damage(cell, amount);
        }

        /// <summary>위층 건물의 내구도를 깎는다. 0이 되면 파괴 (아이템으로 변하지 않음).</summary>
        public bool DamageBuilding(Vector2Int cell, int amount)
        {
            if (IsGameOver) return false;
            return Buildings.Damage(cell, amount);
        }

        // ───────────── 문 / 이동 ─────────────

        /// <summary>문을 열거나 닫는다. 캐릭터가 서 있는 열린 문은 닫을 수 없다.</summary>
        public bool SetDoorOpen(Vector2Int cell, bool open)
        {
            if (IsGameOver) return false;
            if (!Buildings.TryGet(cell, out var building) || building.Type != BuildingType.Door) return false;
            if (!open && HasCharacterAt(cell)) return false;
            return Buildings.SetOpen(cell, open);
        }

        public bool ToggleDoor(Vector2Int cell)
        {
            return Buildings.TryGet(cell, out var building) && SetDoorOpen(cell, !building.IsOpen);
        }

        /// <summary>캐릭터가 이 칸에 설 수 있는지: 바닥이 있고, 위층 건물이 없거나 통과 가능해야 한다.</summary>
        public bool IsWalkable(Vector2Int cell) => Grid.Has(cell) && Buildings.IsPassable(cell);

        static bool HasCharacterAt(Vector2Int cell)
        {
            foreach (var occupant in FindObjectsByType<CellOccupant>())
            {
                if (occupant.Cell == cell) return true;
                // 이동 중인 플레이어는 위치상 이전 칸에 있어도 목적지 칸을 이미 차지한 것으로 본다
                if (occupant.TryGetComponent<PlayerController>(out var player) && player.Cell == cell) return true;
            }
            return false;
        }

        // ───────────── 이벤트 처리 ─────────────

        void OnPlatformsDestroyed(IReadOnlyList<Vector2Int> cells)
        {
            var destroyedSet = new HashSet<Vector2Int>(cells);

            foreach (var cell in cells)
            {
                if (platformViews.TryGetValue(cell, out var view))
                {
                    Destroy(view);
                    platformViews.Remove(cell);
                }

                // 규칙 3: 파괴된 발판 위의 건물도 파괴
                Buildings.Remove(cell);
            }

            // 규칙 3: 파괴된 발판 위의 캐릭터도 파괴
            foreach (var occupant in FindObjectsByType<CellOccupant>())
            {
                if (destroyedSet.Contains(occupant.Cell)) Destroy(occupant.gameObject);
            }
        }

        void OnBuildingRemoved(Building building)
        {
            if (buildingViews.TryGetValue(building.Cell, out var view))
            {
                Destroy(view);
                buildingViews.Remove(building.Cell);
            }

            if (building.Type == BuildingType.Engine) TriggerGameOver();
        }

        void TriggerGameOver()
        {
            if (IsGameOver) return;
            IsGameOver = true;
            UnityEngine.Debug.Log("[Voyager] 엔진이 파괴되었습니다. 게임 오버");
            GameOver?.Invoke();
        }

        // ───────────── 표시 (임시 도형) ─────────────

        /// <summary>발판 / 건물의 표시 여부를 칸 단위로 정한다. (시야 시스템이 매 프레임 호출)</summary>
        public void ApplyVisibility(Func<Vector2Int, bool> isVisible)
        {
            foreach (var pair in platformViews) SetVisible(pair.Value, isVisible(pair.Key));
            foreach (var pair in buildingViews) SetVisible(pair.Value, isVisible(pair.Key));
        }

        static void SetVisible(GameObject view, bool visible)
        {
            var renderer = view.GetComponent<Renderer>();
            if (renderer != null && renderer.enabled != visible) renderer.enabled = visible;
        }

        void AddPlatform(Vector2Int cell, PlatformType type)
        {
            Grid.AddUnchecked(cell, type);
            CreatePlatformView(cell, type);
        }

        void CreatePlatformView(Vector2Int cell, PlatformType type)
        {
            var buildingType = BuildingCatalog.ToBuildingType(type);
            var go = CreatePrimitive(PrimitiveType.Cube, $"{buildingType} {cell.x},{cell.y}", buildingType);
            go.transform.position = GridMath.CellToWorld(cell) + Vector3.down * 0.1f;
            go.transform.localScale = new Vector3(0.95f, 0.2f, 0.95f);
            platformViews[cell] = go;
        }

        void CreateBuildingView(Building building)
        {
            var go = CreatePrimitive(ShapeOf(building.Type), $"{building.Type} {building.Cell.x},{building.Cell.y}", building.Type);
            buildingViews[building.Cell] = go;
            UpdateBuildingView(building);
        }

        void UpdateBuildingView(Building building)
        {
            if (!buildingViews.TryGetValue(building.Cell, out var go)) return;

            var size = SizeOf(building);
            go.transform.localScale = size;
            // 바닥 윗면(y=0) 위에 놓는다. 구/원기둥도 높이 기준으로 맞춘다.
            float height = building.Type == BuildingType.Launcher ? size.y * 2f : size.y;
            go.transform.position = GridMath.CellToWorld(building.Cell) + Vector3.up * (height * 0.5f);
        }

        static PrimitiveType ShapeOf(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Launcher: return PrimitiveType.Cylinder;
                case BuildingType.Detonator:
                case BuildingType.Lightstone: return PrimitiveType.Sphere;
                default: return PrimitiveType.Cube;
            }
        }

        static Vector3 SizeOf(Building building)
        {
            switch (building.Type)
            {
                case BuildingType.Wall: return new Vector3(1f, 1f, 1f);
                case BuildingType.Door: return building.IsOpen ? new Vector3(0.9f, 0.08f, 0.9f) : new Vector3(0.9f, 0.9f, 0.9f);
                case BuildingType.Launcher: return new Vector3(0.6f, 0.15f, 0.6f); // 원기둥 높이는 scale.y의 2배
                case BuildingType.Detonator: return new Vector3(0.4f, 0.4f, 0.4f);
                case BuildingType.Lightstone: return new Vector3(0.45f, 0.45f, 0.45f);
                case BuildingType.Engine: return new Vector3(0.6f, 0.6f, 0.6f);
                case BuildingType.Workshop: return new Vector3(0.8f, 0.5f, 0.8f);
                case BuildingType.Storage: return new Vector3(0.6f, 0.4f, 0.6f);
                default: return Vector3.one * 0.5f;
            }
        }

        static Color ColorOf(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.Platform: return new Color(0.55f, 0.55f, 0.6f);
                case BuildingType.DestructiblePlatform: return new Color(0.85f, 0.55f, 0.2f);
                case BuildingType.Wall: return new Color(0.35f, 0.3f, 0.25f);
                case BuildingType.Door: return new Color(0.55f, 0.35f, 0.15f);
                case BuildingType.Launcher: return new Color(0.25f, 0.25f, 0.3f);
                case BuildingType.Detonator: return new Color(0.9f, 0.2f, 0.6f);
                case BuildingType.Lightstone: return new Color(1f, 0.95f, 0.4f);
                case BuildingType.Engine: return new Color(0.8f, 0.15f, 0.15f);
                case BuildingType.Workshop: return new Color(0.3f, 0.6f, 0.3f);
                case BuildingType.Storage: return new Color(0.3f, 0.45f, 0.7f);
                default: return Color.white;
            }
        }

        GameObject CreatePrimitive(PrimitiveType shape, string objectName, BuildingType type)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = objectName;
            go.transform.SetParent(transform, false);
            go.GetComponent<Renderer>().sharedMaterial = MaterialOf(type);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return go;
        }

        Material MaterialOf(BuildingType type)
        {
            if (materials.TryGetValue(type, out var material)) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader);
            var color = ColorOf(type);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            materials[type] = material;
            return material;
        }
    }
}
