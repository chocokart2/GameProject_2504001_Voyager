using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Voyager
{
    /// <summary>
    /// 플레이어. 칸 단위로 한 칸씩 이동한다 (실시간, 턴제 아님).
    /// 03_04_캐릭터 시스템의 플레이어 항목 참조.
    ///  - 입력: WASD (4방향, 대각선 없음)
    ///  - 이동 간격: moveInterval초에 한 칸
    ///  - 이동하려는 칸에 바닥이 있고, 통과 불가 건물이 없어야 이동 가능 (03_03 표의 "캐릭터 통과")
    /// 발판이 파괴되면 CellOccupant 규칙(규칙 3)에 따라 이 오브젝트도 파괴된다.
    /// </summary>
    [RequireComponent(typeof(CellOccupant))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] float moveInterval = 0.2f; // 한 칸 이동에 걸리는 시간 (초), 05_00_밸런스 값
        [SerializeField] float maxHealth = 100f;    // 임시 값, 05_00_밸런스 값

        ShipController ship;
        Vector2Int logicalCell;   // 이동이 끝났거나 진행 중인 목적지 칸
        Vector3 moveFrom;
        Vector3 moveTo;
        float moveProgress;       // 0 ~ 1
        bool isMoving;

        public float Health { get; private set; }
        public Vector2Int Cell => logicalCell;
        public bool IsMoving => isMoving;
        public event Action Died;

        public void Init(ShipController shipController, Vector2Int startCell)
        {
            ship = shipController;
            logicalCell = startCell;
            transform.position = CellPosition(startCell);
            Health = maxHealth;
        }

        void Update()
        {
            if (ship == null) return;

            if (isMoving)
            {
                moveProgress += Time.deltaTime / moveInterval;
                if (moveProgress >= 1f)
                {
                    transform.position = moveTo;
                    isMoving = false;
                }
                else
                {
                    transform.position = Vector3.Lerp(moveFrom, moveTo, moveProgress);
                }
            }

            // 이동이 끝난 프레임에도 바로 다음 입력을 받아 끊김 없이 이어서 걷는다
            if (!isMoving && !ship.IsGameOver)
            {
                var direction = ReadDirection();
                if (direction != Vector2Int.zero) TryMove(direction);
            }
        }

        public void TakeDamage(float amount)
        {
            if (Health <= 0f) return;
            Health -= amount;
            if (Health <= 0f)
            {
                Health = 0f;
                Died?.Invoke();
                Destroy(gameObject);
            }
        }

        bool TryMove(Vector2Int direction)
        {
            var target = logicalCell + direction;
            if (!ship.IsWalkable(target)) return false; // 바닥이 없거나, 통과할 수 없는 건물이 있으면 이동 불가

            logicalCell = target;
            moveFrom = transform.position;
            moveTo = CellPosition(target);
            moveProgress = 0f;
            isMoving = true;
            return true;
        }

        /// <summary>눌린 방향 하나만 반환. 여러 키가 눌려 있으면 W, S, A, D 순서로 우선.</summary>
        static Vector2Int ReadDirection()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return Vector2Int.zero;

            if (keyboard.wKey.isPressed) return new Vector2Int(0, 1);
            if (keyboard.sKey.isPressed) return new Vector2Int(0, -1);
            if (keyboard.aKey.isPressed) return new Vector2Int(-1, 0);
            if (keyboard.dKey.isPressed) return new Vector2Int(1, 0);
            return Vector2Int.zero;
        }

        static Vector3 CellPosition(Vector2Int cell)
        {
            return GridMath.CellToWorld(cell) + Vector3.up * 0.6f;
        }
    }
}
