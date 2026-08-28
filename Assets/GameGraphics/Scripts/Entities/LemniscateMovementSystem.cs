using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Assets.GameGraphics.Scripts.Entities
{
    // [응용 - 곡선 위를 따라 움직이기]
    // 엔티티를 ∞ 모양 곡선 위에 배치하고, 진행 방향에 맞춰 회전까지 시킵니다.
    // 위치와 회전을 모두 이 시스템이 덮어쓰므로, 여기 쓰이는 엔티티에는
    // RotationSpeed / OrbitMovement 를 같이 붙이지 마세요. (서로 결과를 덮어씁니다.)
    [BurstCompile]
    public partial struct LemniscateMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LemniscateMovement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new LemniscateJob
            {
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime
            };

            job.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct LemniscateJob : IJobEntity
    {
        public float ElapsedTime;

        private void Execute(ref LocalTransform transform, in LemniscateMovement path)
        {
            // t = 곡선 위의 매개변수. 0 ~ 2π 가 한 바퀴입니다.
            float t = path.Phase + ElapsedTime * path.RadiansPerSecond;

            // sin과 cos를 한 번에 구합니다. 따로 부르는 것보다 빠릅니다.
            math.sincos(t, out float sinT, out float cosT);

            // ── 1. 곡선 위의 기준점 ──────────────────────────────
            // 제로노 렘니스케이트: ( cos t , sin t · cos t )
            // t = 0, π 에서 양 끝, t = π/2, 3π/2 에서 원점을 지나며 교차 -> ∞ 모양.
            float3 curve = new float3(cosT, 0f, sinT * cosT) * path.Size;

            // ── 2. 진행 방향(접선) ───────────────────────────────
            // 위 식을 t로 미분: ( -sin t , cos 2t )
            // 곡선이 XZ 평면 위에 있으므로 접선의 y는 항상 0이고, 길이도 0이 되지 않습니다.
            float3 tangent = math.normalize(new float3(-sinT, 0f, math.cos(2f * t)));

            // ── 3. 접선을 축으로 하는 좌표계(프레임) ──────────────
            // 접선에 수직인 축 두 개를 만듭니다. 이 두 축이 만드는 평면이 띠의 단면입니다.
            float3 flat = math.normalize(math.cross(tangent, math.up())); // 수평 방향
            float3 vertical = math.cross(tangent, flat);                  // 수직 방향

            // ── 4. 비틀기 ────────────────────────────────────────
            // 진행할수록 단면을 회전시킵니다.
            // TwistTurns = 0.5 이면 한 바퀴(2π) 돌았을 때 정확히 180도 비틀려서,
            // 띠의 앞면이 뒷면과 이어집니다. 이게 뫼비우스의 띠입니다.
            float twist = path.TwistTurns * t;
            math.sincos(twist, out float sinW, out float cosW);
            float3 across = flat * cosW + vertical * sinW;

            // ── 5. 최종 위치와 방향 ──────────────────────────────
            transform.Position = path.Center + curve + across * path.BandOffset;

            // LookRotationSafe(앞 방향, 위 방향).
            // 앞 = 진행 방향, 옆(로컬 X) = 띠 폭 방향이 되도록 위 방향을 계산합니다.
            transform.Rotation = quaternion.LookRotationSafe(tangent, math.cross(tangent, across));
        }
    }
}
