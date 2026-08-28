using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Assets.GameGraphics.Scripts.Entities
{
    /// <summary>
    /// SubScene / Entities Graphics 없이도 ECS를 바로 체험해보기 위한 실습용 스크립트입니다.
    ///
    /// 하는 일:
    ///  1) 코드로 Entity를 직접 만들고 컴포넌트를 붙인다.
    ///  2) RotationSystem / OrbitMovementSystem 이 매 프레임 그 엔티티들을 움직인다.
    ///  3) 눈으로 확인할 수 있게, 엔티티의 LocalTransform을 일반 GameObject 큐브에 그대로 복사한다.
    ///
    /// (원래 DOTS에서는 3번 없이 Entities Graphics 패키지가 엔티티를 직접 렌더링합니다.
    ///  지금 프로젝트에는 com.unity.entities 만 설치되어 있어서 이렇게 흉내만 냅니다.)
    ///
    /// 사용법: 빈 GameObject에 이 스크립트를 붙이고 재생하면 됩니다.
    /// </summary>
    public class EntitiesPractice : MonoBehaviour
    {
        [SerializeField] private int entityCount = 200;
        [SerializeField] private float radius = 12f;
        [SerializeField] private float degreesPerSecond = 180f;
        [SerializeField] private float cubeScale = 0.4f;

        private World _world;
        private EntityManager _entityManager;
        private NativeArray<Entity> _entities;
        private Transform[] _proxies;

        private void Start()
        {
            // Entities 패키지는 재생 시 "기본 월드(Default World)"를 자동으로 하나 만들어 둡니다.
            // 그 안에 모든 ISystem/SystemBase가 자동 등록되어 돌아갑니다.
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                Debug.LogError("기본 Entity World가 없습니다. Entities 패키지 설치 상태를 확인하세요.");
                enabled = false;
                return;
            }

            // EntityManager : 엔티티 생성/삭제, 컴포넌트 추가/삭제 등 구조 변경을 담당합니다.
            _entityManager = _world.EntityManager;

            // Archetype = "이 엔티티는 어떤 컴포넌트들을 가지는가"의 조합.
            // 같은 아키타입끼리 메모리(청크)에 모여 있어서 순회가 빠릅니다.
            var archetype = _entityManager.CreateArchetype(
                typeof(LocalTransform),   // 위치/회전/스케일
                typeof(LocalToWorld),     // 최종 월드 행렬 (Transform 시스템이 채워줌)
                typeof(RotationSpeed),    // 우리가 만든 컴포넌트
                typeof(OrbitMovement));

            _entities = _entityManager.CreateEntity(archetype, entityCount, Allocator.Persistent);
            _proxies = new Transform[entityCount];

            float radiansPerSecond = math.radians(degreesPerSecond);
            float angleStep = math.PI * 2f / math.max(1, entityCount);

            for (int i = 0; i < _entities.Length; i++)
            {
                var entity = _entities[i];
                float phase = angleStep * i;
                float3 position = new float3(math.cos(phase), 0f, math.sin(phase)) * radius;

                // SetComponentData : 아키타입에 이미 포함된 컴포넌트의 값을 채웁니다.
                _entityManager.SetComponentData(entity,
                    LocalTransform.FromPositionRotationScale(position, quaternion.identity, cubeScale));

                _entityManager.SetComponentData(entity, new RotationSpeed
                {
                    RadiansPerSecond = radiansPerSecond
                });

                _entityManager.SetComponentData(entity, new OrbitMovement
                {
                    Center = float3.zero,
                    Radius = radius,
                    RadiansPerSecond = radiansPerSecond * 0.2f,
                    Phase = phase
                });

                // 엔티티를 눈으로 보기 위한 대역(proxy) 큐브.
                var proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                proxy.name = $"EntityProxy_{i}";
                proxy.transform.SetParent(transform, false);
                Destroy(proxy.GetComponent<BoxCollider>());
                _proxies[i] = proxy.transform;
            }

            Debug.Log($"[EntitiesPractice] 엔티티 {_entities.Length}개 생성 완료. " +
                      "Window > Entities > Hierarchy 창에서 실제 엔티티를 확인할 수 있습니다.");
        }

        private void LateUpdate()
        {
            if (!_entities.IsCreated || _world == null || !_world.IsCreated)
            {
                return;
            }

            // 시스템들이 이미 이번 프레임에 LocalTransform을 갱신해 놓았습니다.
            // 여기서는 그 값을 읽어서 GameObject에 옮겨 담기만 합니다.
            for (int i = 0; i < _entities.Length; i++)
            {
                var localTransform = _entityManager.GetComponentData<LocalTransform>(_entities[i]);

                // float3 / quaternion (Unity.Mathematics) 과 Vector3 / Quaternion (UnityEngine) 은
                // 서로 자동 변환되지 않으므로 직접 옮겨 담습니다.
                float3 p = localTransform.Position;
                float4 r = localTransform.Rotation.value;

                _proxies[i].SetPositionAndRotation(
                    new Vector3(p.x, p.y, p.z),
                    new Quaternion(r.x, r.y, r.z, r.w));
                _proxies[i].localScale = Vector3.one * localTransform.Scale;
            }
        }

        private void OnDestroy()
        {
            // NativeArray는 GC가 회수하지 않는 언매니지드 메모리입니다. 반드시 직접 해제해야 합니다.
            if (!_entities.IsCreated)
            {
                return;
            }

            if (_world != null && _world.IsCreated)
            {
                _entityManager.DestroyEntity(_entities);
            }

            _entities.Dispose();
        }
    }
}
