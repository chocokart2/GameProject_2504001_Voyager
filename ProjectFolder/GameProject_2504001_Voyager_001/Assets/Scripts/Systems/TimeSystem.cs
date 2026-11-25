using System;
using UnityEngine;

namespace Voyager
{
    public enum TimeOfDay
    {
        Day,
        Night,
    }

    /// <summary>
    /// 03_06_시간 시스템. 항해 씬에서 낮/밤을 번갈아 진행한다.
    /// 길이(초)는 Inspector에서 조정.
    /// </summary>
    public class TimeSystem : MonoBehaviour
    {
        [SerializeField] float dayDuration = 60f;
        [SerializeField] float nightDuration = 60f;
        [SerializeField] bool startAtDay = true;

        public TimeOfDay Current { get; private set; }
        public bool IsNight => Current == TimeOfDay.Night;
        public float Elapsed { get; private set; }
        public float PhaseDuration => IsNight ? nightDuration : dayDuration;
        public int DayCount { get; private set; } = 1;

        public event Action<TimeOfDay> PhaseChanged;

        void Awake()
        {
            Current = startAtDay ? TimeOfDay.Day : TimeOfDay.Night;
        }

        void Update()
        {
            Elapsed += Time.deltaTime;
            if (Elapsed >= PhaseDuration) Advance();
        }

        /// <summary>다음 단계로 즉시 넘긴다. (디버그에서도 사용)</summary>
        public void Advance()
        {
            Elapsed = 0f;
            Current = IsNight ? TimeOfDay.Day : TimeOfDay.Night;
            if (Current == TimeOfDay.Day) DayCount++;
            PhaseChanged?.Invoke(Current);
        }
    }
}
