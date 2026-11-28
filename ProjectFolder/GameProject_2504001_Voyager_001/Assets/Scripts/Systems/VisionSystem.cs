using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// 03_10_시야 시스템을 항해 씬에 연결한다.
    /// - 수치(Pn, Ln)를 들고, 함선의 발광석 위치와 낮/밤을 읽어 VisionRules로 판정한다.
    /// - 렌더링: 플레이어 시야 안의 대상만 그린다. 대상은 발판 / 건물(엔진 포함) / 캐릭터(플레이어 자신 포함).
    ///   밤에 발광석 근처가 아니면 플레이어 자신도 보이지 않는다 (의도한 것).
    /// </summary>
    public class VisionSystem : MonoBehaviour
    {
        [SerializeField] float playerSightRange = 6f; // Pn (임시 값), 05_00_밸런스 값
        [SerializeField] float lightstoneRange = 3f;  // Ln (임시 값), 05_00_밸런스 값
        [Tooltip("끄면 시야와 상관없이 모두 그린다 (디버그용)")]
        [SerializeField] bool hideOutOfSight = true;

        ShipController ship;
        TimeSystem timeSystem;
        readonly List<Vector3> lightstoneBuffer = new List<Vector3>();

        public float PlayerSightRange => playerSightRange;
        public float LightstoneRange => lightstoneRange;

        /// <summary>시야의 주인. 없으면 (플레이어 사망 등) 렌더링 제한을 하지 않는다.</summary>
        public PlayerController Viewer { get; set; }

        public bool HideOutOfSight
        {
            get => hideOutOfSight;
            set => hideOutOfSight = value;
        }

        void Awake()
        {
            ship = GetComponent<ShipController>();
            timeSystem = GetComponent<TimeSystem>();
        }

        /// <summary>viewerCell에 있는 플레이어가 targetCell을 볼 수 있는지.</summary>
        public bool PlayerCanSee(Vector2Int viewerCell, Vector2Int targetCell)
        {
            return CanSee(viewerCell, targetCell, CollectLightstones());
        }

        void LateUpdate()
        {
            if (ship == null) return;

            bool limit = hideOutOfSight && Viewer != null;
            var lights = CollectLightstones();
            var viewerCell = Viewer != null ? Viewer.Cell : default;

            // 발판 / 건물
            ship.ApplyVisibility(cell => !limit || CanSee(viewerCell, cell, lights));

            // 캐릭터 (플레이어 자신 포함)
            foreach (var occupant in FindObjectsByType<CellOccupant>())
            {
                var cell = occupant.TryGetComponent<PlayerController>(out var player) ? player.Cell : occupant.Cell;
                bool visible = !limit || CanSee(viewerCell, cell, lights);
                foreach (var renderer in occupant.GetComponentsInChildren<Renderer>())
                    renderer.enabled = visible;
            }
        }

        bool CanSee(Vector2Int viewerCell, Vector2Int targetCell, List<Vector3> lights)
        {
            return VisionRules.CanSee(
                GridMath.CellToWorld(viewerCell),
                GridMath.CellToWorld(targetCell),
                timeSystem != null && timeSystem.IsNight,
                playerSightRange,
                lightstoneRange,
                lights);
        }

        List<Vector3> CollectLightstones()
        {
            lightstoneBuffer.Clear();
            if (ship == null) return lightstoneBuffer;
            foreach (var building in ship.Buildings.All)
            {
                if (building.Type == BuildingType.Lightstone)
                    lightstoneBuffer.Add(GridMath.CellToWorld(building.Cell));
            }
            return lightstoneBuffer;
        }
    }
}
