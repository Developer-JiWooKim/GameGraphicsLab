using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace Assets.GameGraphics.Scripts.Entities
{
    // [Material Property Override]
    // 시뮬레이션이 계산한 값을 엔티티마다 다른 셰이더 프로퍼티 값으로 GPU에 직접 보냅니다.
    //
    // GameObject였다면 MaterialPropertyBlock을 써야 했고, 그러면 렌더러마다 상태가 달라져서
    // 배칭이 쪼개졌습니다. 여기서는 색이 전부 달라도 드로우콜 수는 그대로입니다.
    // 색상값이 LocalToWorld 행렬과 똑같이 "엔티티별 인스턴스 데이터"로 업로드되기 때문입니다.
    //
    // 여기서 쓰는 URPMaterialPropertyBaseColor 는 Entities Graphics 패키지가 기본 제공하는
    // 컴포넌트로, 이렇게 선언되어 있습니다.
    //
    //     [MaterialProperty("_BaseColor")]
    //     public struct URPMaterialPropertyBaseColor : IComponentData { public float4 Value; }
    //
    // 직접 만든 셰이더의 프로퍼티를 쓰고 싶다면 똑같은 방식으로 선언하면 됩니다.
    // 이름은 셰이더 프로퍼티 이름과 같아야 하고, 구조체 크기도 맞아야 합니다.
    // (URP는 _EmissionColor, _Metallic, _Smoothness 등도 미리 제공합니다.)
    [BurstCompile]
    [UpdateAfter(typeof(LemniscateMovementSystem))] // 위치/회전이 확정된 뒤에 색을 정합니다.
    public partial struct RibbonColorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<LemniscateMovement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var job = new RibbonColorJob
            {
                ElapsedTime = (float)SystemAPI.Time.ElapsedTime
            };

            job.ScheduleParallel();
        }
    }

    [BurstCompile]
    public partial struct RibbonColorJob : IJobEntity
    {
        public float ElapsedTime;

        // URPMaterialPropertyBaseColor 가 없는 엔티티는 이 쿼리에 아예 걸리지 않습니다.
        // 그래서 색상 옵션을 꺼둔 경우엔 이 Job이 아무 일도 하지 않습니다.
        private void Execute(
            in LocalTransform transform,
            in LemniscateMovement path,
            ref URPMaterialPropertyBaseColor color)
        {
            float t = path.Phase + ElapsedTime * path.RadiansPerSecond;

            // 곡선 위 진행도(0~1) 를 색상환 한 바퀴에 대응시킵니다.
            float hue = math.frac(t / (2f * math.PI));

            // 판때기의 로컬 +Y 축이 월드에서 어느 쪽을 보고 있는지.
            // 띠가 비틀려 뒤집히면 y가 음수가 되고, 그만큼 어두워집니다.
            // -> 뫼비우스의 "앞면이 뒷면으로 이어지는" 성질이 밝기로 눈에 보입니다.
            float3 plateUp = math.mul(transform.Rotation, math.up());
            float shade = math.lerp(0.25f, 1f, plateUp.y * 0.5f + 0.5f);

            color.Value = new float4(HsvToRgb(hue, 0.85f, shade), 1f);
        }

        // Color.HSVToRGB 는 관리 코드라 Burst 안에서 못 씁니다. 직접 계산합니다.
        // 셰이더에서 흔히 쓰는 공식을 그대로 옮긴 것입니다.
        private static float3 HsvToRgb(float h, float s, float v)
        {
            float3 k = new float3(1f, 2f / 3f, 1f / 3f);
            float3 p = math.abs(math.frac(h + k) * 6f - 3f);
            return v * math.lerp(new float3(1f), math.saturate(p - 1f), s);
        }
    }
}