using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// 발판 위에 있는 대상(캐릭터, 건물)에 붙이는 표시용 컴포넌트.
    /// 03_09 규칙 3: 발판이 파괴되면 그 위의 대상도 파괴된다.
    /// 소속 칸은 위치에서 계산한다. (칸 좌표 = floor(x), floor(z))
    /// </summary>
    public class CellOccupant : MonoBehaviour
    {
        public Vector2Int Cell => GridMath.WorldToCell(transform.position);
    }
}
