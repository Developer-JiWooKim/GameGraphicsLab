using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.GameGraphics.Scripts.Entities
{
    // [ECS 4단계 - Authoring & Baker]
    // Entity는 인스펙터에서 직접 만들 수 없습니다.
    // 그래서 "에디터에서 편집할 GameObject(Authoring)"를 만들고,
    // Baker가 그걸 빌드/플레이 시점에 Entity + Component 로 변환(Baking)합니다.
    //
    // 사용법: SubScene 안의 GameObject에 이 스크립트를 붙이면 됩니다.
    public class RotationSpeedAuthoring : MonoBehaviour
    {
        [Tooltip("초당 회전 각도(도). 인스펙터에서는 도 단위가 편해서 여기서 받고, 베이킹할 때 라디안으로 바꿉니다.")]
        public float DegreesPerSecond = 90f;

        // Baker<T> : T 타입 Authoring 컴포넌트를 Entity 데이터로 변환하는 클래스.
        private class Baker : Baker<RotationSpeedAuthoring>
        {
            public override void Bake(RotationSpeedAuthoring authoring)
            {
                // TransformUsageFlags.Dynamic : 런타임에 움직일 물체 -> LocalTransform 등이 함께 추가됩니다.
                // (움직이지 않는 물체는 Renderable, 아예 트랜스폼이 필요 없으면 None)
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new RotationSpeed
                {
                    RadiansPerSecond = math.radians(authoring.DegreesPerSecond)
                });
            }
        }
    }
}
