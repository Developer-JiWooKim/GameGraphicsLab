using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace Assets.GameGraphics.Scripts.Entities
{
    /// <summary>
    /// ∞(무한대) 모양 곡선을 따라 도는 뫼비우스의 띠를 엔티티로 만듭니다.
    ///
    /// EntitiesPractice와 달리 <b>GameObject를 하나도 만들지 않습니다.</b>
    /// Entities Graphics가 엔티티를 직접 렌더링하기 때문에
    /// 매 프레임 값을 옮겨 담는 코드(LateUpdate)도 필요 없습니다.
    ///
    /// 움직임 계산은 전부 LemniscateMovementSystem이 합니다.
    /// 이 스크립트가 하는 일은 "엔티티를 만들고 초기값을 넣는 것"뿐입니다.
    ///
    /// 사용법: 빈 GameObject에 붙이고 재생.
    /// 필요 조건: URP 렌더링 패스가 Forward+ 여야 하고, 머티리얼 셰이더가
    ///            DOTS Instancing을 지원해야 합니다(URP Lit/Unlit은 지원).
    /// </summary>
    public class MobiusRibbonPractice : MonoBehaviour
    {
        [Header("렌더링")]
        [Tooltip("비워두면 기본 큐브의 메시/머티리얼을 빌려 씁니다.")]
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;

        [Tooltip("켜면 RibbonColorSystem이 엔티티마다 다른 색을 GPU로 보냅니다. " +
                 "꺼서 비교해 보세요. 색이 전부 달라져도 드로우콜 수는 같습니다.")]
        [SerializeField] private bool colorPerEntity = true;

        [Header("띠 구성")]
        [Tooltip("곡선을 따라 몇 칸으로 쪼갤지. Segments × Rows 가 총 엔티티 수입니다.")]
        [SerializeField] private int segments = 2500;

        [Tooltip("띠의 폭 방향으로 몇 줄을 놓을지.")]
        [SerializeField] private int rows = 20;

        [Header("모양")]
        [SerializeField] private float size = 20f;
        [SerializeField] private float bandWidth = 5f;

        [Tooltip("한 바퀴당 비트는 횟수. 0.5 = 뫼비우스, 0 = 평평한 띠, 1 = 원통형")]
        [SerializeField] private float twistTurns = 0.5f;

        [Header("판때기 크기")]
        [Tooltip("켜면 Segments / Rows / 곡선 크기에 맞춰 판때기 크기를 자동 계산합니다. " +
                 "개수를 바꿔도 띠 모양이 유지됩니다.")]
        [SerializeField] private bool autoPlateSize = true;

        [Tooltip("자동 계산일 때의 두께(로컬 Y).")]
        [SerializeField] private float plateThickness = 0.04f;

        [Tooltip("Auto Plate Size를 끄면 이 값을 그대로 씁니다. 로컬 Z가 진행 방향, X가 띠 폭 방향.")]
        [SerializeField] private Vector3 plateSize = new Vector3(0.45f, 0.05f, 0.5f);

        [Header("속도")]
        [SerializeField] private float degreesPerSecond = 30f;

        private World _world;
        private EntityManager _entityManager;
        private NativeArray<Entity> _entities;

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

            if (!ResolveMeshAndMaterial(out var renderMesh, out var renderMaterial))
            {
                Debug.LogError("[MobiusRibbonPractice] 사용할 Mesh/Material을 찾지 못했습니다.");
                enabled = false;
                return;
            }

            segments = Mathf.Max(1, segments);
            rows = Mathf.Max(1, rows);
            int total = segments * rows;
            Vector3 plate = ResolvePlateSize();

            // ── 1단계: 프로토타입 엔티티 만들기 ───────────────────────────
            // RenderMeshUtility.AddComponents는 메인 스레드 전용이고 구조 변경을 일으켜서
            // 엔티티마다 호출하면 느립니다. 그래서 "원본" 하나에만 붙이고 복제하는 게 정석입니다.
            var prototype = _entityManager.CreateEntity();

            RenderMeshUtility.AddComponents(
                prototype,
                _entityManager,
                // 그림자 설정 등 렌더러 옵션. MeshRenderer 인스펙터에 있던 값들에 해당합니다.
                new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true),
                // 이 엔티티들이 공유할 메시/머티리얼 목록 (SharedComponent로 들어갑니다).
                new RenderMeshArray(new[] { renderMaterial }, new[] { renderMesh }),
                // 위 목록에서 몇 번째 머티리얼 / 몇 번째 메시를 쓸지 가리키는 인덱스.
                MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

            // AddComponents가 LocalToWorld / RenderBounds / MaterialMeshInfo 등은 붙여주지만
            // LocalTransform은 붙여주지 않습니다. 우리가 움직일 것이므로 직접 추가합니다.
            _entityManager.AddComponentData(prototype, LocalTransform.Identity);

            // LocalTransform.Scale은 float 하나뿐이라 납작한 판때기를 만들 수 없습니다.
            // 비균등 스케일이 필요하면 PostTransformMatrix를 씁니다.
            // 최종 행렬 = LocalTransform 행렬 × PostTransformMatrix 순서로 곱해집니다.
            _entityManager.AddComponentData(prototype, new PostTransformMatrix
            {
                Value = float4x4.Scale(plate.x, plate.y, plate.z)
            });

            _entityManager.AddComponent<LemniscateMovement>(prototype);

            if (colorPerEntity)
            {
                // Entities Graphics가 기본 제공하는 머티리얼 프로퍼티 컴포넌트입니다.
                // 붙여두기만 하면 RibbonColorSystem이 매 프레임 값을 채우고,
                // 그 값이 셰이더의 _BaseColor 로 엔티티마다 따로 전달됩니다.
                _entityManager.AddComponentData(prototype, new URPMaterialPropertyBaseColor
                {
                    Value = new float4(1f, 1f, 1f, 1f)
                });
            }

            // ── 2단계: 프로토타입 복제 ────────────────────────────────────
            // Instantiate는 컴포넌트 구성이 이미 완성된 엔티티를 통째로 복사하므로
            // 하나씩 AddComponent하는 것보다 훨씬 빠릅니다.
            _entities = _entityManager.Instantiate(prototype, total, Allocator.Persistent);

            // ── 3단계: 엔티티별 초기값 채우기 ─────────────────────────────
            // SetComponentData는 구조 변경이 아니라서 저렴합니다.
            float radiansPerSecond = math.radians(degreesPerSecond);
            var p = transform.position;
            float3 center = new float3(p.x, p.y, p.z);

            for (int s = 0; s < segments; s++)
            {
                // 곡선 한 바퀴(2π)를 Segments 등분해서 시작 위치를 나눠 줍니다.
                float phase = math.PI * 2f * s / segments;

                for (int r = 0; r < rows; r++)
                {
                    // 띠 폭 방향으로 -bandWidth/2 ~ +bandWidth/2 에 고르게 배치.
                    float bandOffset = rows == 1
                        ? 0f
                        : math.lerp(-bandWidth * 0.5f, bandWidth * 0.5f, r / (float)(rows - 1));

                    _entityManager.SetComponentData(_entities[s * rows + r], new LemniscateMovement
                    {
                        Center = center,
                        Size = size,
                        RadiansPerSecond = radiansPerSecond,
                        Phase = phase,
                        BandOffset = bandOffset,
                        TwistTurns = twistTurns
                    });
                }
            }

            // 프로토타입은 복제용 원본일 뿐이라 그대로 두면 화면에 하나 더 그려집니다. 지웁니다.
            _entityManager.DestroyEntity(prototype);

            Debug.Log($"[MobiusRibbonPractice] 엔티티 {total:N0}개 생성 " +
                      $"(곡선 {segments}칸 × 폭 {rows}줄, 판때기 {plate.x:F3}×{plate.y:F3}×{plate.z:F3}). " +
                      "GameObject는 하나도 만들지 않았습니다.");
        }

        /// <summary>
        /// 판때기 하나가 차지해야 할 크기를 개수에 맞춰 계산합니다.
        /// 이게 없으면 Segments를 크게 올렸을 때 판때기끼리 겹쳐서 덩어리로 보입니다.
        /// </summary>
        private Vector3 ResolvePlateSize()
        {
            if (!autoPlateSize)
            {
                return plateSize;
            }

            // 제로노 렘니스케이트의 둘레는 Size의 약 6.097배입니다.
            // (∮|C'(t)|dt 를 수치적분한 값. 딱 떨어지는 닫힌 식이 없어서 상수로 둡니다.)
            const float circumferencePerSize = 6.0972f;

            float alongCurve = circumferencePerSize * size / segments; // 진행 방향 간격
            float acrossBand = bandWidth / rows;                       // 폭 방향 간격

            // 0.9를 곱해 살짝 틈을 남깁니다. 1.0으로 하면 판때기끼리 딱 붙습니다.
            return new Vector3(acrossBand * 0.9f, plateThickness, alongCurve * 0.9f);
        }

        private bool ResolveMeshAndMaterial(out Mesh resolvedMesh, out Material resolvedMaterial)
        {
            resolvedMesh = mesh;
            resolvedMaterial = material;

            if (resolvedMesh != null && resolvedMaterial != null)
            {
                return true;
            }

            // 인스펙터를 비워둔 채 바로 실행해볼 수 있도록, 기본 큐브에서 에셋을 빌려옵니다.
            // sharedMesh / sharedMaterial 은 에셋 참조라 임시 GameObject를 지워도 살아 있습니다.
            var temporary = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // 참고: UnityEngine.Object는 == null 을 오버로드해서 "파괴된 객체"도 null로 봅니다.
            // ??= 같은 C# 문법은 그 오버로드를 무시하므로, Unity 객체에는 == null 을 써야 합니다.
            if (resolvedMesh == null)
            {
                resolvedMesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            }

            if (resolvedMaterial == null)
            {
                resolvedMaterial = temporary.GetComponent<MeshRenderer>().sharedMaterial;
            }

            Destroy(temporary);

            Debug.LogWarning("[MobiusRibbonPractice] Mesh/Material이 비어 있어 기본 큐브 것을 사용합니다. " +
                             "인스펙터에서 직접 지정하는 것을 권장합니다.");

            return resolvedMesh != null && resolvedMaterial != null;
        }

        private void OnDestroy()
        {
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
