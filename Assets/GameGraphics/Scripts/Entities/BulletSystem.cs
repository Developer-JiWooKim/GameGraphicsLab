using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Assets.GameGraphics.Scripts.Entities
{
    // [실전 패턴 - 대량 생성/삭제]
    // 총알 하나당 하는 일:
    //   1. 속도만큼 이동
    //   2. 수명 감소
    //   3. 남은 수명에 따라 색 변경
    //   4. 수명이 다하면 삭제
    //
    // 1~3은 값만 바꾸는 일이라 Job 안에서 바로 할 수 있습니다.
    // 하지만 4번 "삭제"는 구조 변경(Structural Change)이라 Job 안에서 직접 못 합니다.
    // 엔티티를 지우면 청크가 재배치되는데, 다른 스레드가 그 청크를 읽고 있을 수 있으니까요.
    //
    // 그래서 EntityCommandBuffer(ECB)에 "이 엔티티 지워줘"라는 명령만 적어두고,
    // 프레임 끝의 안전한 시점에 메인 스레드가 한 번에 실행합니다.
    [BurstCompile]
    public partial struct BulletSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Bullet>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // EndSimulationEntityCommandBufferSystem : 시뮬레이션 그룹 맨 끝에서
            // 쌓인 명령을 실행해주는 내장 시스템입니다. 그 ECB를 빌려 씁니다.
            var ecb = SystemAPI
                .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            var job = new BulletJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                Ecb = ecb
            };

            job.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct BulletJob : IJobEntity
    {
        public float DeltaTime;
        public EntityCommandBuffer.ParallelWriter Ecb;

        // [ChunkIndexInQuery] : 이 엔티티가 속한 청크의 번호.
        // ECB.ParallelWriter는 여러 스레드가 동시에 쓰기 때문에, 명령 실행 순서를
        // 결정론적으로 만들 정렬 키(sortKey)가 필요합니다. 청크 번호가 그 역할을 합니다.
        // (결정론이 중요한 이유는 멀티플레이어 예측/롤백 때문입니다.)
        private void Execute(
            [ChunkIndexInQuery] int chunkIndex,
            Entity entity,
            ref LocalTransform transform,
            ref Bullet bullet,
            ref URPMaterialPropertyBaseColor color)
        {
            // 1. 이동
            transform.Position += bullet.Velocity * DeltaTime;

            // 2. 수명 감소
            bullet.RemainingLife -= DeltaTime;

            // 3. 남은 수명 비율로 색 변경 (Material Property Override).
            //    갓 발사된 총알은 밝은 노란색, 사라질 때가 되면 붉게 식습니다.
            float life = math.saturate(bullet.RemainingLife / bullet.TotalLife);
            float3 cold = new float3(1f, 0.15f, 0.05f);
            float3 hot = new float3(1f, 0.95f, 0.45f);
            color.Value = new float4(math.lerp(cold, hot, life), 1f);

            // 4. 수명이 다하면 삭제 명령을 기록. 실제 삭제는 프레임 끝에 일어납니다.
            if (bullet.RemainingLife <= 0f)
            {
                Ecb.DestroyEntity(chunkIndex, entity);
            }
        }
    }
}
