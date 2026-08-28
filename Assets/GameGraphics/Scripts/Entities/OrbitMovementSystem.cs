using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Assets.GameGraphics.Scripts.Entities
{
    // [ECS 3단계 - Job으로 멀티스레드 돌리기]
    // RotationSystem처럼 foreach로 도는 건 "메인 스레드 1개"로 처리합니다.
    // 엔티티가 수만 개가 되면 IJobEntity로 바꿔서 여러 코어에 나눠 처리할 수 있습니다.
    // 이게 DOTS를 쓰는 가장 큰 이유입니다.
    [BurstCompile]
    public partial struct OrbitMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<OrbitMovement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new OrbitJob
            {
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime
            };

            // ScheduleParallel() : 엔티티들을 청크 단위로 쪼개 여러 워커 스레드에서 동시에 실행.
            // 의존성(state.Dependency) 관리는 소스 제너레이터가 알아서 해줍니다.
            // 참고: Run() = 메인 스레드 즉시 실행, Schedule() = 단일 워커 스레드.
            job.ScheduleParallel();
        }
    }

    // IJobEntity도 partial struct 여야 합니다.
    // Execute의 파라미터가 곧 쿼리 조건이 됩니다.
    //   ref T  = 읽고 쓰기
    //   in  T  = 읽기 전용
    [BurstCompile]
    public partial struct OrbitJob : IJobEntity
    {
        public float ElapsedTime;

        private void Execute(ref LocalTransform transform, in OrbitMovement orbit)
        {
            float angle = orbit.Phase + ElapsedTime * orbit.RadiansPerSecond;

            // Mathf 대신 Unity.Mathematics의 math 클래스를 사용합니다 (Burst 최적화 대상).
            float3 offset = new float3(math.cos(angle), 0f, math.sin(angle)) * orbit.Radius;
            transform.Position = orbit.Center + offset;
        }
    }
}
