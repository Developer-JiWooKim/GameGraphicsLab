using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Assets.GameGraphics.Scripts.Entities
{
    // [ECS 5단계 - 런타임에 엔티티 복제하기]
    // 한 번만 실행되고 스스로 꺼지는 스포너 시스템입니다.
    [BurstCompile]
    public partial struct CubeSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // CubeSpawner가 베이킹되어 월드에 등장할 때까지 OnUpdate를 호출하지 않습니다.
            state.RequireForUpdate<CubeSpawner>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 이 시스템은 한 프레임만 실행하면 되므로 바로 자기 자신을 끕니다.
            state.Enabled = false;

            // 월드에 딱 하나만 존재하는 컴포넌트를 가져올 때 GetSingleton을 씁니다.
            var spawner = SystemAPI.GetSingleton<CubeSpawner>();
            if (spawner.Prefab == Entity.Null || spawner.Count <= 0)
            {
                return;
            }

            // 프리팹 엔티티를 Count개 복제. Allocator.Temp는 이 프레임 안에서만 쓰는 임시 메모리입니다.
            var instances = state.EntityManager.Instantiate(
                spawner.Prefab, spawner.Count, Allocator.Temp);

            float radiansPerSecond = math.radians(spawner.DegreesPerSecond);
            float angleStep = math.PI * 2f / spawner.Count;

            for (int i = 0; i < instances.Length; i++)
            {
                var entity = instances[i];
                float phase = angleStep * i;

                // SetComponent : 이미 붙어 있는 컴포넌트의 값을 바꿉니다. (Dynamic 베이킹이라 LocalTransform 존재)
                state.EntityManager.SetComponentData(entity, LocalTransform.FromPositionRotationScale(
                    new float3(math.cos(phase), 0f, math.sin(phase)) * spawner.Radius,
                    quaternion.identity,
                    1f));

                // AddComponent : 없던 컴포넌트를 새로 붙입니다.
                state.EntityManager.AddComponentData(entity, new RotationSpeed
                {
                    RadiansPerSecond = radiansPerSecond
                });

                state.EntityManager.AddComponentData(entity, new OrbitMovement
                {
                    Center = float3.zero,
                    Radius = spawner.Radius,
                    RadiansPerSecond = radiansPerSecond * 0.25f,
                    Phase = phase
                });
            }

            instances.Dispose();
        }
    }
}
