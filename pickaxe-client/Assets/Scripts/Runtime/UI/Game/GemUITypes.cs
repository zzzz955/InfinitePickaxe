namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 보석 선택 역할 (합성/전환 UI용)
    /// </summary>
    public enum GemSelectionRole
    {
        None = 0,
        Base = 1,      // 기준 보석
        Material = 2,  // 재료 보석 (합성)
        Convert = 3    // 전환 대상 보석
    }

    /// <summary>
    /// 클라이언트 전용 보석 데이터 (UI 표시용)
    /// 실제 서버 데이터는 Infinitepickaxe.GemInfo 사용
    /// </summary>
    public sealed class GemUIData
    {
        public string GemInstanceId;           // 보석 인스턴스 ID (서버 식별자)
        public uint GemId;                     // 보석 메타 ID
        public Infinitepickaxe.GemGrade Grade; // 보석 등급
        public Infinitepickaxe.GemType Type;   // 보석 타입
        public string Name;                    // 보석 이름
        public string IconName;                // 아이콘 이름
        public uint StatMultiplier;            // 스탯 배율 (%)
        public ulong AcquiredAt;               // 획득 시간

        /// <summary>
        /// 프로토콜 GemInfo에서 UI 데이터로 변환
        /// </summary>
        public static GemUIData FromProtocol(Infinitepickaxe.GemInfo gemInfo)
        {
            if (gemInfo == null) return null;

            return new GemUIData
            {
                GemInstanceId = gemInfo.GemInstanceId,
                GemId = gemInfo.GemId,
                Grade = gemInfo.Grade,
                Type = gemInfo.Type,
                Name = gemInfo.Name,
                IconName = gemInfo.Icon,
                StatMultiplier = gemInfo.StatMultiplier,
                AcquiredAt = gemInfo.AcquiredAt
            };
        }
    }
}
