using UnityEngine;
using UnityEngine.InputSystem;

namespace Voyager
{
    /// <summary>
    /// 규칙 확인용 디버그 입력.
    ///  1 ~ 9      : 설치할 건물 선택 (발판, 파괴 가능한 발판, 벽, 문, 발사기, 기폭 장치, 발광석, 작업장, 보관함)
    ///  좌클릭     : 선택한 건물 설치 (밤에는 발광석만)
    ///  우클릭     : 그 칸의 바닥 즉시 파괴 (규칙 1, 2, 3 확인용)
    ///  X          : 그 칸의 위층 건물에 피해 5
    ///  Z          : 그 칸의 바닥에 피해 5
    ///  F          : 그 칸의 문 열기 / 닫기 (상호작용 시스템 전 임시)
    ///  P          : 마우스 위치에 테스트용 캡슐(CellOccupant) 생성
    ///  N          : 낮/밤 즉시 전환
    ///  V          : 시야 렌더링 제한 켜기 / 끄기 (끄면 시야 밖도 보임)
    ///  WASD       : 플레이어 이동 (한 칸씩)
    /// </summary>
    [RequireComponent(typeof(ShipController), typeof(TimeSystem))]
    public class ShipDebugInput : MonoBehaviour
    {
        [SerializeField] bool setupCamera = true;
        [SerializeField] float cameraHeight = 18f;
        [SerializeField] int debugDamage = 5;

        static readonly BuildingType[] selectable =
        {
            BuildingType.Platform,
            BuildingType.DestructiblePlatform,
            BuildingType.Wall,
            BuildingType.Door,
            BuildingType.Launcher,
            BuildingType.Detonator,
            BuildingType.Lightstone,
            BuildingType.Workshop,
            BuildingType.Storage,
        };

        ShipController ship;
        TimeSystem timeSystem;
        VisionSystem vision;
        PlayerController player;
        Camera cam;
        BuildingType selected = BuildingType.Platform;
        Vector2Int hoveredCell;
        bool hasHover;
        string lastMessage = "";

        void Start()
        {
            ship = GetComponent<ShipController>();
            timeSystem = GetComponent<TimeSystem>();
            vision = GetComponent<VisionSystem>(); // ShipController.Awake에서 보장됨
            cam = Camera.main;

            ship.BuildDemoLayout();
            player = SpawnPlayer(new Vector2Int(2, 1));
            if (vision != null) vision.Viewer = player;

            if (setupCamera && cam != null)
            {
                cam.transform.position = new Vector3(4.5f, cameraHeight, 1.5f);
                cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null || cam == null) return;

            ReadSelection(keyboard);
            if (keyboard.nKey.wasPressedThisFrame) timeSystem.Advance();
            if (keyboard.vKey.wasPressedThisFrame && vision != null) vision.HideOutOfSight = !vision.HideOutOfSight;

            hasHover = TryGetMouseCell(mouse.position.ReadValue(), out hoveredCell);
            if (!hasHover) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (player == null) lastMessage = "설치 실패: 플레이어 없음";
                else lastMessage = ship.TryBuild(player.Cell, hoveredCell, selected, out var reason)
                    ? $"{BuildingCatalog.Get(selected).DisplayName} 설치"
                    : $"설치 실패: {reason}";
            }
            if (mouse.rightButton.wasPressedThisFrame) ship.DestroyCell(hoveredCell);
            if (keyboard.xKey.wasPressedThisFrame) ship.DamageBuilding(hoveredCell, debugDamage);
            if (keyboard.zKey.wasPressedThisFrame) ship.DamagePlatform(hoveredCell, debugDamage);
            if (keyboard.fKey.wasPressedThisFrame && !ship.ToggleDoor(hoveredCell)) lastMessage = "문을 열거나 닫을 수 없음";
            if (keyboard.pKey.wasPressedThisFrame) SpawnTestOccupant(hoveredCell);
        }

        void ReadSelection(Keyboard keyboard)
        {
            var keys = new[]
            {
                keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key,
                keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key,
                keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key,
            };
            for (int i = 0; i < keys.Length && i < selectable.Length; i++)
            {
                if (keys[i].wasPressedThisFrame) selected = selectable[i];
            }
        }

        bool TryGetMouseCell(Vector2 screenPosition, out Vector2Int cell)
        {
            var ray = cam.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance))
            {
                cell = GridMath.WorldToCell(ray.GetPoint(distance));
                return true;
            }
            cell = default;
            return false;
        }

        PlayerController SpawnPlayer(Vector2Int cell)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = go.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader);
            var color = new Color(0.2f, 0.5f, 0.95f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else material.color = color;
            renderer.sharedMaterial = material;

            // PlayerController는 CellOccupant를 요구하므로 자동으로 함께 붙는다 (규칙 3 적용 대상)
            var spawned = go.AddComponent<PlayerController>();
            spawned.Init(ship, cell);
            return spawned;
        }

        void SpawnTestOccupant(Vector2Int cell)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "TestOccupant";
            go.transform.position = GridMath.CellToWorld(cell) + Vector3.up * 0.6f;
            go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            go.AddComponent<CellOccupant>();
        }

        void OnGUI()
        {
            if (ship == null) return;

            string time = timeSystem.IsNight ? "밤" : "낮";
            string state = ship.IsGameOver ? "  [게임 오버]" : "";
            GUI.Label(new Rect(10, 10, 900, 25),
                $"{timeSystem.DayCount}일차 {time}  ({timeSystem.Elapsed:0}/{timeSystem.PhaseDuration:0}s){state}");
            GUI.Label(new Rect(10, 35, 900, 25),
                $"선택: {BuildingCatalog.Get(selected).DisplayName}  |  1~9 선택, 좌클릭 설치(밤엔 발광석만), 우클릭 바닥 파괴, X 건물 피해, Z 바닥 피해, F 문, P 캡슐, N 낮/밤, V 시야 렌더링 {(vision != null && vision.HideOutOfSight ? "켬" : "끔")}");

            if (hasHover)
            {
                string floor = ship.Grid.TryGetType(hoveredCell, out var platformType)
                    ? $"{BuildingCatalog.Get(BuildingCatalog.ToBuildingType(platformType)).DisplayName} {ship.Grid.GetDurability(hoveredCell)}"
                    : "없음";
                string upper = ship.Buildings.TryGet(hoveredCell, out var building)
                    ? $"{building.Definition.DisplayName} {building.Durability}/{building.Definition.MaxDurability}" +
                      (building.Type == BuildingType.Door ? (building.IsOpen ? " (열림)" : " (닫힘)") : "")
                    : "없음";
                string sight = player == null || vision == null ? "-"
                    : vision.PlayerCanSee(player.Cell, hoveredCell) ? "보임" : "안 보임";
                GUI.Label(new Rect(10, 60, 900, 25),
                    $"칸 ({hoveredCell.x}, {hoveredCell.y})  바닥: {floor}  위층: {upper}  시야: {sight}");
            }

            if (!string.IsNullOrEmpty(lastMessage))
                GUI.Label(new Rect(10, 85, 900, 25), lastMessage);
        }
    }
}
