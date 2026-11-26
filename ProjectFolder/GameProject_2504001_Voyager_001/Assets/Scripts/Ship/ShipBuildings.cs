using System;
using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>위층에 설치된 건물 하나.</summary>
    public class Building
    {
        public BuildingType Type { get; }
        public Vector2Int Cell { get; }
        public int Durability { get; internal set; }
        /// <summary>문 전용. 열린 문은 캐릭터가 통과할 수 있다.</summary>
        public bool IsOpen { get; internal set; }

        public BuildingDefinition Definition => BuildingCatalog.Get(Type);
        public bool IsPassable => Definition.Passable || (Type == BuildingType.Door && IsOpen);

        internal Building(BuildingType type, Vector2Int cell)
        {
            Type = type;
            Cell = cell;
            Durability = BuildingCatalog.Get(type).MaxDurability;
        }
    }

    /// <summary>
    /// 함선의 위층 건물 배치. MonoBehaviour가 아닌 순수 데이터 + 규칙 클래스.
    /// 바닥(발판 / 파괴 가능한 발판)은 ShipGrid가 맡고, 여기는 그 위에 올라가는 건물만 다룬다.
    /// 03_03_건물 시스템 공통 규칙: 1x1, 한 칸에 위층 건물 하나.
    /// </summary>
    public class ShipBuildings
    {
        readonly Dictionary<Vector2Int, Building> buildings = new Dictionary<Vector2Int, Building>();

        public event Action<Building> Placed;
        /// <summary>건물이 사라졌을 때 (내구도 0 / 발판 파괴 / 직접 제거). 파괴된 건물은 아이템으로 변하지 않는다.</summary>
        public event Action<Building> Removed;
        /// <summary>문이 열리거나 닫혔을 때.</summary>
        public event Action<Building> StateChanged;

        public IEnumerable<Building> All => buildings.Values;
        public int Count => buildings.Count;

        public bool Has(Vector2Int cell) => buildings.ContainsKey(cell);

        public bool TryGet(Vector2Int cell, out Building building) => buildings.TryGetValue(cell, out building);

        /// <summary>건물이 없거나, 있어도 통과 가능한 건물이면 true. (바닥 유무는 검사하지 않는다)</summary>
        public bool IsPassable(Vector2Int cell)
        {
            return !buildings.TryGetValue(cell, out var building) || building.IsPassable;
        }

        /// <summary>위층 건물이고, 그 칸에 바닥이 있고, 위층이 비어 있어야 한다.</summary>
        public bool CanPlace(ShipGrid grid, Vector2Int cell, BuildingType type)
        {
            if (BuildingCatalog.Get(type).Layer != BuildingLayer.Upper) return false;
            if (!grid.Has(cell)) return false;
            return !Has(cell);
        }

        public bool TryPlace(ShipGrid grid, Vector2Int cell, BuildingType type, out Building building)
        {
            building = null;
            if (!CanPlace(grid, cell, type)) return false;
            building = new Building(type, cell);
            buildings[cell] = building;
            Placed?.Invoke(building);
            return true;
        }

        /// <summary>내구도를 깎는다. 0 이하가 되면 파괴. 반환값: 파괴되었는지.</summary>
        public bool Damage(Vector2Int cell, int amount)
        {
            if (amount <= 0 || !buildings.TryGetValue(cell, out var building)) return false;
            building.Durability -= amount;
            if (building.Durability > 0) return false;
            building.Durability = 0;
            Remove(cell);
            return true;
        }

        public bool Remove(Vector2Int cell)
        {
            if (!buildings.TryGetValue(cell, out var building)) return false;
            buildings.Remove(cell);
            Removed?.Invoke(building);
            return true;
        }

        /// <summary>문을 열거나 닫는다. 문이 아니면 false.</summary>
        public bool SetOpen(Vector2Int cell, bool open)
        {
            if (!buildings.TryGetValue(cell, out var building) || building.Type != BuildingType.Door) return false;
            if (building.IsOpen == open) return true;
            building.IsOpen = open;
            StateChanged?.Invoke(building);
            return true;
        }
    }
}
