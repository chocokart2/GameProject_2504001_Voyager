using UnityEngine;
using UnityEngine.InputSystem;

namespace Voyager
{
    /// <summary>
    /// 규칙 확인용 디버그 입력.
    ///  1 / 2      : 설치할 발판 종류 선택 (일반 / 파괴 가능)
    ///  좌클릭     : 선택한 발판 설치 (낮에만, 기존 발판 옆에만)
    ///  우클릭     : 그 칸의 발판 파괴
    ///  P          : 마우스 위치에 테스트용 캡슐(CellOccupant) 생성
    ///  N          : 낮/밤 즉시 전환
    ///  WASD       : 플레이어 이동 (한 칸씩)
    /// </summary>
    [RequireComponent(typeof(ShipController), typeof(TimeSystem))]
    public class ShipDebugInput : MonoBehaviour
    {
        [SerializeField] bool setupCamera = true;
        [SerializeField] float cameraHeight = 18f;

        ShipController ship;
        TimeSystem timeSystem;
        Camera cam;
        PlatformType selected = PlatformType.Normal;
        Vector2Int hoveredCell;
        bool hasHover;

        void Start()
        {
            ship = GetComponent<ShipController>();
            timeSystem = GetComponent<TimeSystem>();
            cam = Camera.main;

            ship.BuildDemoLayout();
            SpawnPlayer(new Vector2Int(2, 1));

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

            if (keyboard.digit1Key.wasPressedThisFrame) selected = PlatformType.Normal;
            if (keyboard.digit2Key.wasPressedThisFrame) selected = PlatformType.Destructible;
            if (keyboard.nKey.wasPressedThisFrame) timeSystem.Advance();

            hasHover = TryGetMouseCell(mouse.position.ReadValue(), out hoveredCell);
            if (!hasHover) return;

            if (mouse.leftButton.wasPressedThisFrame) ship.TryBuild(hoveredCell, selected);
            if (mouse.rightButton.wasPressedThisFrame) ship.DestroyCell(hoveredCell);
            if (keyboard.pKey.wasPressedThisFrame) SpawnTestOccupant(hoveredCell);
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

        void SpawnPlayer(Vector2Int cell)
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
            var player = go.AddComponent<PlayerController>();
            player.Init(ship, cell);
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
            GUI.Label(new Rect(10, 10, 700, 25),
                $"{timeSystem.DayCount}일차 {time}  ({timeSystem.Elapsed:0}/{timeSystem.PhaseDuration:0}s){state}");
            GUI.Label(new Rect(10, 35, 700, 25),
                $"선택: {(selected == PlatformType.Normal ? "일반 발판" : "파괴 가능한 발판")}  |  1/2 선택, 좌클릭 설치(낮만), 우클릭 파괴, P 캡슐, N 낮/밤 전환");
            if (hasHover)
                GUI.Label(new Rect(10, 60, 700, 25), $"칸 좌표: ({hoveredCell.x}, {hoveredCell.y})");
        }
    }
}
