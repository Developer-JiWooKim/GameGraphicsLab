using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Assets.GameGraphics.Scripts.Entities
{
    /// <summary>
    /// GameObject 플레이어(캡슐)가 바라보는 방향으로 총알 엔티티를 무한 발사하는 시뮬레이터입니다.
    ///
    /// 실무에서 흔한 하이브리드 구조를 그대로 보여줍니다.
    ///   - 하나뿐인 플레이어 = MonoBehaviour (입력, 이동 처리가 편함)
    ///   - 수천~수만 개인 총알 = Entity (ECS의 이득이 나오는 지점)
    ///
    /// 이 스크립트가 하는 일은 "총알 엔티티를 만들어 초기값을 넣는 것"까지입니다.
    /// 이동 / 수명 / 삭제 / 색 변화는 전부 BulletSystem이 병렬로 처리합니다.
    ///
    /// 조작: W/S 전진·후진, A/D 회전, Space 발사 토글
    /// 사용법: 빈 GameObject에 붙이고 재생. 캡슐과 총구 표시는 자동으로 만들어집니다.
    /// </summary>
    public class BulletShooterPractice : MonoBehaviour
    {
        [Header("총알 렌더링")]
        [Tooltip("비워두면 기본 Sphere의 메시/머티리얼을 빌려 씁니다.")]
        [SerializeField] private Mesh bulletMesh;
        [SerializeField] private Material bulletMaterial;

        [Header("발사")]
        [SerializeField] private float bulletsPerSecond = 500f;
        [SerializeField] private float bulletSpeed = 25f;

        [Tooltip("총알이 살아있는 시간(초). 이 값 × 초당 발사수 ≈ 화면에 상주하는 총알 수입니다.")]
        [SerializeField] private float bulletLifetime = 4f;

        [SerializeField] private float bulletScale = 0.15f;

        [Tooltip("총구 위치(플레이어 로컬 좌표).")]
        [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 1f, 0.7f);

        [Tooltip("탄 퍼짐(도). 0이면 일직선으로 나갑니다.")]
        [SerializeField] private float spreadDegrees = 4f;

        [Header("플레이어 조작")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float turnDegreesPerSecond = 120f;

        [Tooltip("켜면 캡슐 몸통과 총구 표시를 자동으로 만듭니다.")]
        [SerializeField] private bool createPlayerVisual = true;

        private World _world;
        private EntityManager _entityManager;
        private Entity _bulletPrototype;
        private EntityQuery _bulletQuery;
        private Unity.Mathematics.Random _random;
        private float _fireAccumulator;
        private bool _firing = true;
        private bool _initialized;
        private GUIStyle _hudStyle;

        private void Start()
        {
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                Debug.LogError("기본 Entity World가 없습니다. Entities 패키지 설치 상태를 확인하세요.");
                enabled = false;
                return;
            }

            _entityManager = _world.EntityManager;

            // Unity.Mathematics.Random 은 UnityEngine.Random 과 이름이 겹칩니다.
            // 두 네임스페이스를 같이 using 했으므로 반드시 전체 이름으로 씁니다.
            _random = new Unity.Mathematics.Random(0x6E624EB7u);

            if (!ResolveMeshAndMaterial(out var mesh, out var material))
            {
                Debug.LogError("[BulletShooterPractice] 사용할 Mesh/Material을 찾지 못했습니다.");
                enabled = false;
                return;
            }

            if (createPlayerVisual)
            {
                BuildPlayerVisual();
            }

            // ── 총알 프로토타입 ───────────────────────────────────────────
            // 매번 AddComponents를 부르면 느리므로, 원본 하나만 만들어두고 계속 복제합니다.
            _bulletPrototype = _entityManager.CreateEntity();

            RenderMeshUtility.AddComponents(
                _bulletPrototype,
                _entityManager,
                // 총알 그림자는 끕니다. 수만 개면 그림자 패스가 그대로 두 배 부담입니다.
                new RenderMeshDescription(ShadowCastingMode.Off),
                new RenderMeshArray(new[] { material }, new[] { mesh }),
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

            _entityManager.AddComponentData(_bulletPrototype, LocalTransform.Identity);
            _entityManager.AddComponent<Bullet>(_bulletPrototype);
            _entityManager.AddComponentData(_bulletPrototype, new URPMaterialPropertyBaseColor
            {
                Value = new float4(1f, 1f, 1f, 1f)
            });

            // Prefab 태그를 붙이면 이 엔티티는 모든 쿼리에서 제외됩니다.
            // 안 붙이면 원본이 원점에 하나 그려지고, BulletSystem도 이걸 처리하려 듭니다.
            // Instantiate로 만든 사본에서는 이 태그가 자동으로 빠집니다.
            _entityManager.AddComponent<Prefab>(_bulletPrototype);

            // 살아있는 총알 수를 세거나 한꺼번에 지울 때 쓸 쿼리.
            _bulletQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<Bullet>());
            _initialized = true;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            HandleInput(deltaTime);
            Fire(deltaTime);
        }

        private void HandleInput(float deltaTime)
        {
            // 이 프로젝트는 Active Input Handling이 "Input System Package (New)" 라서
            // 예전 Input.GetAxis 는 예외를 던집니다. 새 API를 씁니다.
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float turn = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) turn -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) turn += 1f;

            float move = 0f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move -= 1f;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                _firing = !_firing;
            }

            transform.Rotate(Vector3.up, turn * turnDegreesPerSecond * deltaTime);
            transform.position += transform.forward * (move * moveSpeed * deltaTime);
        }

        private void Fire(float deltaTime)
        {
            if (!_firing || bulletsPerSecond <= 0f)
            {
                return;
            }

            // 발사 간격이 프레임 시간보다 짧을 수 있으므로 소수점을 누적해서
            // 이번 프레임에 몇 발을 쏠지 정합니다. 프레임레이트가 달라도 초당 발사수는 유지됩니다.
            _fireAccumulator += bulletsPerSecond * deltaTime;
            int count = (int)_fireAccumulator;
            if (count <= 0)
            {
                return;
            }

            _fireAccumulator -= count;

            var muzzleWorld = transform.TransformPoint(muzzleOffset);
            var forwardWorld = transform.forward;
            float3 muzzle = new float3(muzzleWorld.x, muzzleWorld.y, muzzleWorld.z);
            float3 forward = new float3(forwardWorld.x, forwardWorld.y, forwardWorld.z);
            float spread = math.tan(math.radians(spreadDegrees));

            // 프로토타입을 count개 복제. 컴포넌트 구성이 이미 완성돼 있어 훨씬 저렴합니다.
            var bullets = _entityManager.Instantiate(_bulletPrototype, count, Allocator.Temp);

            for (int i = 0; i < bullets.Length; i++)
            {
                float3 direction = forward;
                if (spread > 0f)
                {
                    direction = math.normalize(forward + _random.NextFloat3Direction() * spread);
                }

                // 한 프레임에 여러 발을 쏠 때 전부 총구에 겹치면 뭉텅이로 보입니다.
                // 이번 프레임 동안 날아갔을 거리만큼 미리 밀어서 흐름처럼 보이게 합니다.
                float subFrame = (i + 1) / (float)count;
                float3 position = muzzle + direction * (bulletSpeed * deltaTime * (1f - subFrame));

                _entityManager.SetComponentData(bullets[i], LocalTransform.FromPositionRotationScale(
                    position,
                    quaternion.LookRotationSafe(direction, math.up()),
                    bulletScale));

                _entityManager.SetComponentData(bullets[i], new Bullet
                {
                    Velocity = direction * bulletSpeed,
                    RemainingLife = bulletLifetime,
                    TotalLife = bulletLifetime
                });
            }

            bullets.Dispose();
        }

        private void BuildPlayerVisual()
        {
            if (GetComponentInChildren<Renderer>() != null)
            {
                return; // 이미 보이는 몸통이 있으면 만들지 않습니다.
            }

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "PlayerBody";
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Destroy(body.GetComponent<CapsuleCollider>());

            // 어느 쪽을 보고 있는지 알 수 있게 총구 표시를 붙입니다.
            var muzzle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            muzzle.name = "Muzzle";
            muzzle.transform.SetParent(transform, false);
            muzzle.transform.localPosition = muzzleOffset;
            muzzle.transform.localScale = new Vector3(0.15f, 0.15f, 0.6f);
            Destroy(muzzle.GetComponent<BoxCollider>());
        }

        private bool ResolveMeshAndMaterial(out Mesh resolvedMesh, out Material resolvedMaterial)
        {
            resolvedMesh = bulletMesh;
            resolvedMaterial = bulletMaterial;

            if (resolvedMesh != null && resolvedMaterial != null)
            {
                return true;
            }

            var temporary = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            if (resolvedMesh == null)
            {
                resolvedMesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            }

            if (resolvedMaterial == null)
            {
                resolvedMaterial = temporary.GetComponent<MeshRenderer>().sharedMaterial;
            }

            Destroy(temporary);
            return resolvedMesh != null && resolvedMaterial != null;
        }

        private void OnGUI()
        {
            if (!_initialized || _world == null || !_world.IsCreated)
            {
                return;
            }

            if (_hudStyle == null)
            {
                _hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            }

            int alive = _bulletQuery.CalculateEntityCount();

            GUI.Label(new Rect(14f, 12f, 520f, 110f),
                $"살아있는 총알 엔티티: {alive:N0}\n" +
                $"초당 발사: {bulletsPerSecond:N0}   발사 중: {(_firing ? "ON" : "OFF")}\n" +
                "W/S 전진·후진   A/D 회전   Space 발사 토글",
                _hudStyle);
        }

        private void OnDestroy()
        {
            // Start가 도중에 실패했으면 정리할 것도 없습니다.
            if (!_initialized || _world == null || !_world.IsCreated)
            {
                return;
            }

            // 남아있는 총알과 프로토타입을 정리합니다.
            _entityManager.DestroyEntity(_bulletQuery);

            if (_entityManager.Exists(_bulletPrototype))
            {
                _entityManager.DestroyEntity(_bulletPrototype);
            }

            _bulletQuery.Dispose();
        }
    }
}
