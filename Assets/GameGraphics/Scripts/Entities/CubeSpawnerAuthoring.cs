using Unity.Entities;
using UnityEngine;

namespace Assets.GameGraphics.Scripts.Entities
{
    // SubScene 안에 빈 GameObject를 만들고 이 스크립트를 붙인 뒤,
    // Prefab 칸에 아무 프리팹(예: Cube 프리팹)을 넣으면
    // 재생할 때 Count 개수만큼 엔티티가 원형으로 생성됩니다.
    public class CubeSpawnerAuthoring : MonoBehaviour
    {
        public GameObject Prefab;
        public int Count = 100;
        public float Radius = 10f;
        public float DegreesPerSecond = 45f;

        private class Baker : Baker<CubeSpawnerAuthoring>
        {
            public override void Bake(CubeSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new CubeSpawner
                {
                    // GetEntity(프리팹) 을 호출하면 그 프리팹이 "프리팹 Entity"로 함께 베이킹되고,
                    // 런타임에 Instantiate로 복제할 수 있는 참조를 얻습니다.
                    Prefab = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
                    Count = authoring.Count,
                    Radius = authoring.Radius,
                    DegreesPerSecond = authoring.DegreesPerSecond
                });
            }
        }
    }
}
