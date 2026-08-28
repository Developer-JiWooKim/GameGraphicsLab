# Unity Entities (DOTS / ECS) 실습

Unity 6000.5.9f1 + `com.unity.entities` 6.5.0 기준으로 작성한 첫 ECS 실습 예제입니다.

## ECS가 뭔가요

기존 MonoBehaviour 방식은 **오브젝트 하나 = 데이터 + 로직**이 한 클래스에 묶여 있고,
객체들이 힙 여기저기에 흩어져 있어서 수천 개가 넘어가면 캐시 미스 때문에 느려집니다.

ECS는 이걸 셋으로 쪼갭니다.

| 개념 | 역할 | 이 폴더의 예 |
|---|---|---|
| **Entity** | 그냥 ID 하나. 데이터도 로직도 없음 | 큐브 하나하나 |
| **Component** | 순수 데이터 (`struct` + `IComponentData`) | `RotationSpeed`, `OrbitMovement` |
| **System** | 특정 컴포넌트 조합을 가진 엔티티를 매 프레임 처리하는 로직 | `RotationSystem`, `OrbitMovementSystem` |

같은 컴포넌트 조합(= **Archetype**)을 가진 엔티티들은 **Chunk**라는 연속된 메모리 블록에 모여 있습니다.
그래서 시스템이 순회할 때 캐시 적중률이 높고, Burst 컴파일 + Job으로 멀티스레드까지 태울 수 있습니다.

## 파일 구성

| 파일 | 내용 |
|---|---|
| `PracticeComponents.cs` | `IComponentData` 컴포넌트들 |
| `RotationSystem.cs` | `ISystem` + `SystemAPI.Query` 로 메인 스레드 순회 |
| `OrbitMovementSystem.cs` | `IJobEntity` + `ScheduleParallel()` 로 멀티스레드 순회 |
| `RotationSpeedAuthoring.cs` | Authoring MonoBehaviour + `Baker` (GameObject → Entity 변환) |
| `CubeSpawnerAuthoring.cs` | 프리팹을 엔티티로 베이킹하는 스포너 설정 |
| `CubeSpawnerSystem.cs` | 런타임에 프리팹 엔티티를 N개 복제 |
| `EntitiesPractice.cs` | **SubScene 없이** 코드로만 엔티티를 만들어 바로 돌려보는 데모 |
| `LemniscateMovementSystem.cs` | ∞ 곡선 위 이동 + 비틀기. 접선/프레임 계산 예제 |
| `RibbonColorSystem.cs` | Material Property Override — 엔티티별 색을 배칭 안 깨고 GPU로 |
| `BulletSystem.cs` | 이동 + 수명 + `EntityCommandBuffer` 로 대량 삭제 |
| `BulletShooterPractice.cs` | GameObject 플레이어 ↔ 총알 엔티티 하이브리드 시뮬레이터 |
| `MobiusRibbonPractice.cs` | ∞ 모양 뫼비우스 띠 데모. **Entities Graphics로 직접 렌더링** |

### 어떤 파일을 GameObject에 붙일 수 있나

MonoBehaviour만 붙일 수 있습니다. 나머지를 드래그하면
`Can't add script behaviour ... The script needs to derive from MonoBehaviour!` 가 뜹니다.

| 파일 | 붙이기 | 비고 |
|---|---|---|
| `EntitiesPractice.cs` | ⭕ | 아무 GameObject |
| `MobiusRibbonPractice.cs` | ⭕ | 아무 GameObject |
| `BulletShooterPractice.cs` | ⭕ | 아무 GameObject (캡슐은 자동 생성) |
| `RotationSpeedAuthoring.cs` | ⭕ | **SubScene 안에서만** 의미 있음 |
| `CubeSpawnerAuthoring.cs` | ⭕ | **SubScene 안에서만** 의미 있음 |
| `PracticeComponents.cs` | ❌ | `IComponentData` struct — 코드로 Entity에 붙임 |
| `*System.cs` | ❌ | `ISystem` — **어디에도 붙이지 않음** |

마지막 줄이 MonoBehaviour와 가장 다른 지점입니다. 시스템은 씬에 배치하는 게 아니라,
재생하는 순간 Unity가 어셈블리를 스캔해서 기본 World에 자동 등록하고 매 프레임 돌립니다.
조건에 맞는 엔티티가 없으면 (`RequireForUpdate` 덕분에) 그냥 쉬고 있을 뿐입니다.

## 1) 제일 빠른 확인 방법 — `EntitiesPractice.cs`

1. 아무 씬이나 열고 빈 GameObject를 만듭니다.
2. `EntitiesPractice` 컴포넌트를 붙입니다.
3. 재생(Play).

원형으로 배치된 큐브들이 스스로 자전하면서 중심을 공전합니다.
`Window > Entities > Hierarchy` 를 열면 실제로 만들어진 엔티티 목록이 보이고,
엔티티를 선택하면 `Window > Entities > Components`(인스펙터)에서 `RotationSpeed` 값도 볼 수 있습니다.

> 참고: 이 스크립트는 **일부러** 옛날 방식으로 그립니다.
> 엔티티의 `LocalTransform` 값을 매 프레임 일반 GameObject 큐브에 복사해서 보여줍니다.
> 데이터가 어떻게 흐르는지 코드에 다 드러나서 처음 이해할 때 좋지만,
> GameObject를 그대로 들고 있으므로 **DOTS의 성능 이점은 하나도 없습니다.**
>
> 실제 방식은 `MobiusRibbonPractice` 쪽을 보세요. 자세한 비교는 아래
> [두 데모의 렌더링 방식 차이](#두-데모의-렌더링-방식-차이-중요) 참고.

## 2) 정석 워크플로 — SubScene + Authoring + Baker

ECS의 실제 작업 흐름은 "에디터에서는 GameObject로 편집하고, 런타임에는 Entity로 변환"입니다.

1. Hierarchy 우클릭 → `New Sub Scene > Empty Scene...` 으로 SubScene 생성.
2. SubScene 안에 Cube를 하나 넣고 `RotationSpeedAuthoring` 을 붙인 뒤 `Degrees Per Second` 설정.
3. 재생하면 `RotationSystem` 이 그 엔티티를 회전시킵니다.

스포너까지 써보려면:

1. Cube를 프리팹으로 저장합니다.
2. SubScene 안에 빈 GameObject를 만들고 `CubeSpawnerAuthoring` 을 붙입니다.
3. `Prefab` 칸에 만든 프리팹, `Count` 에 100 정도를 넣고 재생합니다.

`CubeSpawnerSystem` 이 첫 프레임에 100개를 복제하고 스스로 꺼집니다.

프리팹에 `MeshRenderer` 가 있으면 베이킹할 때 Entities Graphics 렌더링 컴포넌트가
자동으로 붙기 때문에, 이쪽은 별도 코드 없이 화면에 그대로 보입니다.

## 3) ∞ 모양 뫼비우스 띠 — `MobiusRibbonPractice.cs`

빈 GameObject에 `MobiusRibbonPractice` 를 붙이고 재생하면,
납작한 판때기 엔티티들이 ∞ 곡선을 따라 흐르면서 띠를 이룹니다.

`Mesh` / `Material` 은 비워두면 기본 큐브 것을 빌려 씁니다.
(URP Renderer의 Rendering Path가 **Forward+** 여야 하고, 머티리얼 셰이더가
DOTS Instancing을 지원해야 합니다. URP Lit/Unlit은 지원합니다.)

인스펙터에서 만져볼 값:

| 항목 | 설명 |
|---|---|
| `Segments` × `Rows` | 곡선 방향 칸 수 × 띠 폭 방향 줄 수. 곱한 값이 총 엔티티 수 (기본 2500 × 20 = **50,000**) |
| `Size` | ∞ 의 가로 크기 |
| `Band Width` | 띠의 폭 |
| `Twist Turns` | **0.5 = 뫼비우스**(반 바퀴 비틀림), 0 = 평평한 띠, 1 = 원통, 1.5·2.5 = 더 꼬인 뫼비우스 |
| `Auto Plate Size` | 켜두면 개수·크기에 맞춰 판때기 크기를 자동 계산 (끄면 `Plate Size` 를 그대로 사용) |
| `Degrees Per Second` | 곡선 위를 흐르는 속도 |

기본값이 `Size = 20` 이라 띠가 가로로 40 유닛 정도 됩니다.
기본 카메라 위치에서는 화면을 벗어나니 카메라를 뒤로 빼세요 (예: `(0, 15, -45)`, 아래로 살짝 회전).

`Twist Turns` 를 0 과 0.5 로 번갈아 두고 비교해 보면 차이가 바로 보입니다.
0.5 일 때는 한 바퀴 돌아온 판때기가 **뒤집혀서** 제자리에 오기 때문에,
띠의 앞면과 뒷면이 하나로 이어집니다. 이게 뫼비우스의 띠입니다.

### 수학 부분

`LemniscateMovementSystem` 안에 주석으로 단계별 설명이 있습니다. 요약하면:

1. **곡선** — 제로노 렘니스케이트 `( cos t , sin t · cos t )`. `t = π/2, 3π/2` 에서 원점을 지나며 스스로 교차해 ∞ 모양이 됩니다.
2. **접선** — 위 식을 미분한 `( -sin t , cos 2t )`. 진행 방향입니다.
3. **프레임** — 접선에 수직인 축 두 개(`flat`, `vertical`)를 만듭니다. 이 평면이 띠의 단면입니다.
4. **비틀기** — 단면을 `TwistTurns × t` 만큼 회전시킨 방향으로 엔티티를 밀어냅니다.
5. **방향** — `quaternion.LookRotationSafe(접선, 단면법선)` 으로 판때기를 곡선에 눕힙니다.

곡선 식만 바꾸면 다른 궤도도 똑같이 만들 수 있습니다.
(예: `( cos t , sin t )` → 원, `( cos t , sin 2t )` → 리사주 곡선)

## 두 데모의 렌더링 방식 차이 (중요)

`EntitiesPractice` 와 `MobiusRibbonPractice` 는 **일부러 다른 방식**으로 그립니다.
시스템 코드는 그대로 두고 렌더링만 갈아끼웠을 때 뭐가 달라지는지 비교해 보세요.

| | `EntitiesPractice` (프록시) | `MobiusRibbonPractice` (Entities Graphics) |
|---|---|---|
| GameObject 수 | 엔티티 수만큼 생성 | **0개** |
| 매 프레임 메인 스레드 작업 | `LateUpdate`에서 엔티티 수만큼 값 복사 | 없음 |
| 그리는 주체 | `MeshRenderer` | `EntitiesGraphicsSystem` (GPU 인스턴싱) |
| Scene 뷰에서 클릭 선택 | 됨 | 안 됨 (Entities Hierarchy로 확인) |
| 코드 길이 | 김 (미러링 코드 필요) | 짧음 |
| 실제 DOTS 성능 이점 | **없음** | 있음 |

프록시 방식은 "엔티티의 `LocalTransform` 값이 실제로 이렇게 바뀌고 있다"를
코드로 눈에 보이게 하려고 남겨둔 학습용 구조입니다. 실무에서는 쓰지 않습니다.

`MobiusRibbonPractice` 는 기본값이 이미 **50,000개**입니다.
프록시 방식이었다면 GameObject 5만 개라 에디터가 버티지 못했을 규모입니다.

### 드로우콜은 왜 안 늘어나나

같은 메시 + 같은 머티리얼 + 같은 `RenderFilterSettings` 를 쓰는 엔티티들은
한 배치로 묶여서 **인스턴싱으로 한 번에** 그려집니다.
5만 개든 500개든 배치 수가 사실상 같습니다. Stats 창에서 직접 확인해 보세요.

늘어나는 조건은 개수가 아니라 **조합의 가짓수**입니다.

| 늘어나지 않는 것 | 늘어나는 것 |
|---|---|
| 엔티티 개수 | 서로 다른 메시/머티리얼 종류 |
| 엔티티마다 다른 위치·회전·스케일 | 다른 레이어 / 그림자 설정 |
| 엔티티마다 다른 머티리얼 프로퍼티 값(색 등) | 그림자 패스 (켜면 한 번 더 그림) |

다만 **CPU 쪽 비용은 개수에 비례합니다.** 매 프레임 5만 개의 `LocalToWorld` 를 계산해
GPU로 올려야 하니까요. 그 계산이 Burst + Job으로 병렬화되어 있다는 게 DOTS의 요점입니다.

### 런타임에 렌더링 컴포넌트 붙이는 정석 패턴

`MobiusRibbonPractice.Start()` 가 쓰는 3단계입니다.

1. **프로토타입 하나** 만들어 `RenderMeshUtility.AddComponents` 로 렌더링 컴포넌트를 붙인다.
   이 API는 메인 스레드 전용 + 구조 변경이라 엔티티마다 부르면 느립니다.
2. `EntityManager.Instantiate(prototype, count, ...)` 로 **통째로 복제**한다.
3. `SetComponentData` 로 개체별 값만 채운다. (구조 변경이 아니라서 저렴)

마지막에 프로토타입은 지웁니다. 안 지우면 원본이 화면에 하나 더 그려집니다.

## 4) Material Property Override — 시뮬레이션 값을 셰이더로

`MobiusRibbonPractice` 의 `Color Per Entity` 를 켜면 (기본값 켜짐)
`RibbonColorSystem` 이 판때기마다 다른 색을 계산해서 셰이더로 보냅니다.

- **색상(Hue)** = 곡선 위 진행도 → 띠를 따라 무지개가 흐릅니다.
- **밝기** = 판때기의 로컬 +Y가 월드에서 향한 방향 → **비틀려서 뒤집힌 부분이 어두워집니다.**
  뫼비우스의 "앞면이 뒷면으로 이어지는" 성질이 눈에 보이게 됩니다.

### 여기가 핵심입니다

`Color Per Entity` 를 껐다 켜면서 **Stats 창의 Batches 수를 보세요. 변하지 않습니다.**

GameObject였다면 색을 다르게 하려면 `MaterialPropertyBlock` 을 써야 하고,
그러면 렌더러마다 상태가 달라져 배칭이 쪼개집니다.
ECS에서는 색상값이 `LocalToWorld` 행렬과 **똑같이 엔티티별 인스턴스 데이터로** GPU에 올라가서,
전부 다른 색이어도 드로우콜은 그대로입니다.

시뮬레이션(체력 비율, 팀 색, 피격 플래시)이 계산한 값을 그대로 렌더링에 꽂을 수 있다는 뜻이고,
이게 "시뮬레이션과 렌더링이 같은 데이터 구조를 공유"해서 얻는 이득입니다.

### 쓰는 법

Entities Graphics가 URP용 프로퍼티 컴포넌트를 미리 제공합니다.
엔티티에 붙이고 값만 쓰면 끝입니다.

| 컴포넌트 | 셰이더 프로퍼티 |
|---|---|
| `URPMaterialPropertyBaseColor` | `_BaseColor` |
| `URPMaterialPropertyEmissionColor` | `_EmissionColor` |
| `URPMaterialPropertyMetallic` / `...Smoothness` | `_Metallic` / `_Smoothness` |
| `URPMaterialPropertyCutoff` / `...BumpScale` 등 | 이름 그대로 |

직접 만든 셰이더의 프로퍼티를 쓰고 싶으면 똑같이 선언하면 됩니다.
이름은 셰이더 프로퍼티와 같아야 하고, 구조체 크기도 맞아야 합니다.

```csharp
[MaterialProperty("_MyCustomValue")]
public struct MyCustomValue : IComponentData { public float Value; }
```

## 5) 총알 발사 시뮬레이터 — `BulletShooterPractice.cs`

빈 GameObject에 붙이고 재생하면 캡슐 플레이어가 생기고, 바라보는 방향으로 총알 엔티티가 쏟아집니다.

| 조작 | |
|---|---|
| `W` / `S` | 전진 · 후진 |
| `A` / `D` | 회전 (총알 방향이 바뀝니다) |
| `Space` | 발사 켜기/끄기 |

화면 좌측 상단에 살아있는 총알 엔티티 수가 표시됩니다.
기본값(초당 500발 × 수명 4초)이면 **약 2,000개**가 상시 유지됩니다.
`Bullets Per Second` 를 5000으로 올리면 2만 개가 됩니다.

### 이게 보여주는 것: 하이브리드 구조

실무에서 가장 흔한 형태입니다.

| | 담당 | 이유 |
|---|---|---|
| 플레이어 (1개) | **MonoBehaviour** | 입력·이동 처리가 편하고, 1개짜리는 ECS로 옮겨도 이득이 없음 |
| 총알 (수천~수만) | **Entity** | 같은 처리를 대량 반복 — ECS의 이득이 나오는 지점 |

`BulletShooterPractice` 가 하는 일은 **총알을 만들고 초기값을 넣는 것까지**입니다.
그 뒤의 이동 / 수명 / 삭제 / 색 변화는 전부 `BulletSystem` 이 Burst + 멀티스레드로 처리합니다.

### 핵심 개념 1: `EntityCommandBuffer`

수명이 끝난 총알을 지워야 하는데, **엔티티 삭제는 Job 안에서 직접 할 수 없습니다.**
지우는 순간 청크가 재배치되는데 다른 스레드가 그 청크를 읽고 있을 수 있으니까요.

그래서 Job에서는 ECB에 "이 엔티티 지워줘"라는 **명령만 기록**하고,
프레임 끝의 안전한 시점에 `EndSimulationEntityCommandBufferSystem` 이 한 번에 실행합니다.

```csharp
Ecb.DestroyEntity(chunkIndex, entity);   // 기록만. 실제 삭제는 나중에.
```

`chunkIndex`(`[ChunkIndexInQuery]`)는 **정렬 키**입니다. 여러 스레드가 동시에 명령을 기록하므로,
실행 순서를 결정론적으로 만들려면 키가 필요합니다. 멀티플레이어 예측/롤백이 성립하는 근거이기도 합니다.

### 핵심 개념 2: `Prefab` 태그

복제용 원본 엔티티에 `Prefab` 태그를 붙이면 **모든 쿼리에서 제외**됩니다.
안 붙이면 원본이 원점에 하나 그려지고, `BulletSystem` 도 이걸 처리하려 듭니다.
`Instantiate` 로 만든 사본에서는 이 태그가 자동으로 빠집니다.

(`MobiusRibbonPractice` 는 복제를 한 번만 하므로 원본을 그냥 지웠습니다.
반복해서 복제해야 하면 이렇게 `Prefab` 태그를 씁니다.)

### 아직 없는 것

**충돌 판정은 없습니다.** 총알은 그냥 날아가다 수명이 끝나면 사라집니다.
`Physics.Raycast()` 는 엔티티를 못 맞춥니다 — GameObject 물리(PhysX)와
엔티티 물리(Unity Physics)는 완전히 별개의 공간 자료구조를 씁니다.

충돌을 붙이려면 둘 중 하나입니다.

1. **거리 계산** — 표적 위치를 싱글톤 컴포넌트로 ECS에 넘기고 Job에서 직접 거리를 잼. 패키지 불필요.
2. **`com.unity.physics` 설치** — `PhysicsCollider` 를 붙이면 진짜 충돌·트리거 이벤트가 생김.

## 자주 걸리는 부분

- **`partial` 빠뜨림** — `ISystem`, `IJobEntity` 구현체는 반드시 `partial struct` 여야 합니다.
  소스 제너레이터가 나머지 코드를 만들어 붙이기 때문입니다.
- **`Vector3` / `Mathf` 대신 `float3` / `math`** — `Unity.Mathematics` 타입을 써야 Burst가 최적화합니다.
  둘 사이에 자동 변환은 없어서 직접 옮겨 담아야 합니다 (`EntitiesPractice.LateUpdate` 참고).
- **Burst 안에서 `Debug.Log` 금지** — 관리 객체를 못 씁니다. 로그가 필요하면 `[BurstCompile]` 을 떼거나
  `Unity.Logging` 을 쓰세요.
- **`NativeArray`는 직접 `Dispose()`** — GC가 회수하지 않는 언매니지드 메모리입니다.
  `Allocator.Temp`(한 프레임) / `TempJob`(4프레임) / `Persistent`(수동 해제) 를 구분해서 씁니다.
- **엔티티가 분홍색으로 보이면** 셰이더가 DOTS Instancing을 지원하지 않는 겁니다.
  URP Lit/Unlit을 쓰거나, Shader Graph라면 DOTS 인스턴싱 옵션을 켜세요.
- **엔티티가 아예 안 보이면** URP Renderer의 Rendering Path가 Forward+ 인지 확인하세요.
  Entities Graphics는 URP에서 Forward+ 만 지원합니다.
- **비균등 스케일은 `PostTransformMatrix`** — `LocalTransform.Scale` 은 `float` 하나뿐입니다.
- **Unity 객체에 `??=` / `?.` 쓰지 말 것** — `UnityEngine.Object` 는 `== null` 을 오버로드해서
  "파괴된 객체"도 null 취급하는데, C# 널 연산자는 그 오버로드를 무시합니다.
- **구조 변경(Structural Change)은 비쌉니다** — 엔티티 생성/삭제, 컴포넌트 추가/삭제는 청크를 재배치합니다.
  매 프레임 해야 한다면 `EntityCommandBuffer` 로 모아서 한 번에 처리하세요.
- **이 프로젝트는 새 Input System 전용입니다** (`activeInputHandler: 1`).
  `Input.GetAxis` / `Input.GetKey` 를 쓰면 예외가 납니다.
  `UnityEngine.InputSystem.Keyboard.current` 를 쓰세요.
- **`Unity.Mathematics.Random` 과 `UnityEngine.Random` 은 이름이 겹칩니다.**
  두 네임스페이스를 같이 `using` 했다면 전체 이름으로 써야 합니다.

## 다음에 해볼 것

- `EntityCommandBuffer` 로 총알 생성/삭제 구현하기
- `IEnableableComponent` 로 구조 변경 없이 on/off 하기
- 커스텀 셰이더 프로퍼티에 `[MaterialProperty]` 붙여서 직접 오버라이드해 보기
- `Unity.Physics` 로 ECS 물리 붙이기
