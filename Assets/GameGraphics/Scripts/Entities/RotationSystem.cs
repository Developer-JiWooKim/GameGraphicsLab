using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace Assets.GameGraphics.Scripts.Entities
{
    // [ECS 2단계 - System]
    // System은 "로직"입니다. 데이터를 가진 Entity들을 찾아서 매 프레임 처리합니다.
    //
    // ISystem은 struct 기반 시스템이라 Burst 컴파일이 가능해서 빠릅니다.
    // (class 기반인 SystemBase도 있지만, 지금은 ISystem만 알아도 충분합니다.)
    // partial 키워드는 필수입니다. Unity의 소스 제너레이터가 나머지 코드를 자동 생성합니다.
    [BurstCompile]
    public partial struct RotationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // RotationSpeed 컴포넌트를 가진 엔티티가 하나도 없으면 OnUpdate를 아예 호출하지 않습니다.
            state.RequireForUpdate<RotationSpeed>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // MonoBehaviour의 Time.deltaTime 대신 SystemAPI.Time.DeltaTime 을 씁니다.
            float deltaTime = SystemAPI.Time.DeltaTime;

            // SystemAPI.Query<...> : "이 컴포넌트들을 전부 가진 엔티티"만 골라서 순회합니다.
            //   RefRW<T> = 읽고 쓰기 (Read/Write)
            //   RefRO<T> = 읽기 전용 (Read Only)
            // LocalTransform은 Entities의 기본 위치/회전/스케일 컴포넌트입니다.
            // (GameObject의 Transform과 같은 역할이지만 struct입니다.)
            foreach (var (transform, rotationSpeed) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeed>>())
            {
                // LocalTransform은 불변(immutable) 스타일이라 결과를 다시 대입해줘야 합니다.
                transform.ValueRW = transform.ValueRO.RotateY(
                    rotationSpeed.ValueRO.RadiansPerSecond * deltaTime);
            }
        }
    }
}
