using System.Collections.Generic;
using UnityEngine;

namespace Voyager
{
    /// <summary>
    /// 03_10_시야 시스템의 판정 규칙. MonoBehaviour가 아닌 순수 계산.
    /// 거리는 월드 좌표 (x, z) 평면에서 잰다: X^2 >= (Mx-Tx)^2 + (Mz-Tz)^2
    /// </summary>
    public static class VisionRules
    {
        /// <summary>두 지점의 (x, z) 거리가 radius 이하인지.</summary>
        public static bool IsWithin(Vector3 from, Vector3 to, float radius)
        {
            float dx = from.x - to.x;
            float dz = from.z - to.z;
            return radius * radius >= dx * dx + dz * dz;
        }

        /// <summary>"발광석 근처에 있음": 발광석 중 하나라도 거리 Ln 이하이면 참.</summary>
        public static bool IsNearLightstone(Vector3 point, IEnumerable<Vector3> lightstones, float ln)
        {
            foreach (var light in lightstones)
            {
                if (IsWithin(point, light, ln)) return true;
            }
            return false;
        }

        /// <summary>
        /// 플레이어 / 승무원의 시야.
        /// 낮: 거리 Pn 이하. 밤: 거리 Pn 이하 ∩ 대상이 발광석 근처에 있음.
        /// </summary>
        public static bool CanSee(Vector3 viewer, Vector3 target, bool isNight, float pn, float ln, IEnumerable<Vector3> lightstones)
        {
            if (!IsWithin(viewer, target, pn)) return false;
            return !isNight || IsNearLightstone(target, lightstones, ln);
        }
    }
}
