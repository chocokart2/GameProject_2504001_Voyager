using System.Collections.Generic;

namespace Voyager
{
    public enum BuildingType
    {
        Platform,             // 발판
        DestructiblePlatform, // 파괴 가능한 발판
        Wall,                 // 벽
        Door,                 // 문
        Launcher,             // 발사기
        Detonator,            // 기폭 장치
        Lightstone,           // 발광석
        Engine,               // 엔진
        Workshop,             // 작업장
        Storage,              // 보관함
    }

    public enum BuildingCategory
    {
        Floor,     // 바닥
        Partition, // 칸막이
        Armament,  // 무장
        Essential, // 필수 장치
        Facility,  // 문서상 카테고리 이름은 "아이템" (작업장 / 보관함)
    }

    /// <summary>한 칸 = 바닥 1개 + 그 위(위층) 1개.</summary>
    public enum BuildingLayer
    {
        Floor,
        Upper,
    }

    public enum InteractionKind
    {
        None,
        InstantDestroy, // 즉시 파괴
        OpenClose,      // 열기 / 닫기
        Fire,           // 발사
        Signal,         // 신호
        Use,            // 사용
        Open,           // 열기
    }

    public sealed class BuildingDefinition
    {
        public BuildingType Type { get; }
        public string DisplayName { get; }
        public BuildingCategory Category { get; }
        public BuildingLayer Layer { get; }
        /// <summary>캐릭터가 이 건물과 겹쳐 서 있을 수 있는지. 문은 닫힌 상태 기준 (열린 문은 Building.IsPassable에서 처리).</summary>
        public bool Passable { get; }
        public int MaxDurability { get; }
        /// <summary>설치를 위한 아이템이 있는지. 없으면 플레이어가 설치할 수 없다 (엔진).</summary>
        public bool HasItem { get; }
        public InteractionKind Interaction { get; }
        /// <summary>밤에도 설치할 수 있는지. (03_06_시간 시스템: 밤에는 설치 금지, 이 값이 true인 건물만 예외)</summary>
        public bool BuildableAtNight { get; }

        public BuildingDefinition(BuildingType type, string displayName, BuildingCategory category, BuildingLayer layer,
            bool passable, int maxDurability, bool hasItem, InteractionKind interaction, bool buildableAtNight)
        {
            BuildableAtNight = buildableAtNight;
            Type = type;
            DisplayName = displayName;
            Category = category;
            Layer = layer;
            Passable = passable;
            MaxDurability = maxDurability;
            HasItem = hasItem;
            Interaction = interaction;
        }
    }

    /// <summary>
    /// 03_03_건물 시스템의 "공통된 형식에 의한 테이블"을 코드로 옮긴 것.
    /// 문서의 표를 고치면 여기도 같이 고친다. 내구도 값은 05_00_밸런스 값 문서에 있다.
    /// </summary>
    public static class BuildingCatalog
    {
        static readonly Dictionary<BuildingType, BuildingDefinition> table = new Dictionary<BuildingType, BuildingDefinition>();

        static BuildingCatalog()
        {
            //   건물                              이름                카테고리                     층                    통과    내구도 아이템  상호작용                         밤 설치
            Add(BuildingType.Platform,             "발판",             BuildingCategory.Floor,     BuildingLayer.Floor, true,  10, true,  InteractionKind.None,           false);
            Add(BuildingType.DestructiblePlatform, "파괴 가능한 발판", BuildingCategory.Floor,     BuildingLayer.Floor, true,  10, true,  InteractionKind.InstantDestroy, false);
            Add(BuildingType.Wall,                 "벽",               BuildingCategory.Partition, BuildingLayer.Upper, false, 30, true,  InteractionKind.None,           false);
            Add(BuildingType.Door,                 "문",               BuildingCategory.Partition, BuildingLayer.Upper, false, 20, true,  InteractionKind.OpenClose,      false);
            Add(BuildingType.Launcher,             "발사기",           BuildingCategory.Armament,  BuildingLayer.Upper, true,  10, true,  InteractionKind.Fire,           false);
            Add(BuildingType.Detonator,            "기폭 장치",        BuildingCategory.Armament,  BuildingLayer.Upper, false, 10, true,  InteractionKind.Signal,         false);
            Add(BuildingType.Lightstone,           "발광석",           BuildingCategory.Armament,  BuildingLayer.Upper, false, 1,  true,  InteractionKind.None,           true);
            Add(BuildingType.Engine,               "엔진",             BuildingCategory.Essential, BuildingLayer.Upper, false, 10, false, InteractionKind.None,           false);
            Add(BuildingType.Workshop,             "작업장",           BuildingCategory.Facility,  BuildingLayer.Upper, false, 1,  true,  InteractionKind.Use,            false);
            Add(BuildingType.Storage,              "보관함",           BuildingCategory.Facility,  BuildingLayer.Upper, false, 1,  true,  InteractionKind.Open,           false);
        }

        static void Add(BuildingType type, string name, BuildingCategory category, BuildingLayer layer,
            bool passable, int durability, bool hasItem, InteractionKind interaction, bool buildableAtNight)
        {
            table[type] = new BuildingDefinition(type, name, category, layer, passable, durability, hasItem, interaction, buildableAtNight);
        }

        public static BuildingDefinition Get(BuildingType type) => table[type];

        public static IEnumerable<BuildingDefinition> All => table.Values;

        /// <summary>바닥 건물 → ShipGrid의 PlatformType.</summary>
        public static bool TryGetPlatformType(BuildingType type, out PlatformType platformType)
        {
            switch (type)
            {
                case BuildingType.Platform: platformType = PlatformType.Normal; return true;
                case BuildingType.DestructiblePlatform: platformType = PlatformType.Destructible; return true;
                default: platformType = default; return false;
            }
        }

        public static BuildingType ToBuildingType(PlatformType platformType)
        {
            return platformType == PlatformType.Normal ? BuildingType.Platform : BuildingType.DestructiblePlatform;
        }
    }
}
