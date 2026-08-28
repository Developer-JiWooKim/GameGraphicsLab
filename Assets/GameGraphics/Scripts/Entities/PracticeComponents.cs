using Unity.Entities;
using Unity.Mathematics;

namespace Assets.GameGraphics.Scripts.Entities
{
    // [ECS 1단계 - Component]
    // ECS에서 "데이터"에 해당하는 부분입니다.
    // MonoBehaviour와 달리 로직(메서드)은 넣지 않고 순수 데이터만 담습니다.
    // struct + IComponentData 로 만들면 Entity에 붙일 수 있는 컴포넌트가 됩니다.
    // struct라서 값 타입이고, 메모리에 촘촘히(연속적으로) 배치되어 캐시 효율이 좋습니다.
    public struct RotationSpeed : IComponentData
    {
        // 초당 회전량(라디안). 도(degree)가 아니라 라디안인 점에 주의.
        public float RadiansPerSecond;
    }

    // 원 궤도를 그리며 도는 데 필요한 데이터.
    // float3 는 Unity.Mathematics의 타입으로, Vector3 대신 사용합니다.
    // (Burst 컴파일러가 SIMD로 최적화하기 좋은 형태입니다.)
    public struct OrbitMovement : IComponentData
    {
        public float3 Center;
        public float Radius;
        public float RadiansPerSecond;
        public float Phase; // 시작 각도. 엔티티마다 다르게 주면 흩어져서 돕니다.
    }

    // ∞(무한대) 모양 곡선 위를 도는 띠(band) 한 조각의 데이터.
    // 곡선 자체는 "제로노 렘니스케이트(Lemniscate of Gerono)" 이고,
    // 한 바퀴 도는 동안 띠를 반 바퀴 비틀면 뫼비우스의 띠가 됩니다.
    public struct LemniscateMovement : IComponentData
    {
        public float3 Center;           // ∞ 곡선의 중심 위치
        public float Size;              // ∞ 의 크기(가로 반지름)
        public float RadiansPerSecond;  // 곡선 위를 진행하는 속도
        public float Phase;             // 곡선 위에서의 시작 위치 (0 ~ 2π)
        public float BandOffset;        // 띠 폭 방향 오프셋. 같은 Phase에 여러 값을 주면 띠가 됩니다.
        public float TwistTurns;        // 한 바퀴당 비트는 횟수. 0.5 = 반 바퀴 = 뫼비우스, 0 = 안 비틀림
    }

    // 총알 하나의 상태. 위치는 LocalTransform이 들고 있으므로 여기엔 없습니다.
    // "무엇을 컴포넌트로 쪼갤 것인가"가 ECS 설계의 대부분입니다.
    public struct Bullet : IComponentData
    {
        public float3 Velocity;      // 초당 이동 벡터 (방향 × 속력)
        public float RemainingLife;  // 남은 수명(초). 0이 되면 삭제됩니다.
        public float TotalLife;      // 발사 시점의 수명. 색을 계산할 때 비율로 씁니다.
    }

    // 스포너 설정값. Prefab 필드에는 GameObject가 아니라 "프리팹 Entity"가 들어갑니다.
    public struct CubeSpawner : IComponentData
    {
        public Entity Prefab;
        public int Count;
        public float Radius;
        public float DegreesPerSecond;
    }
}
