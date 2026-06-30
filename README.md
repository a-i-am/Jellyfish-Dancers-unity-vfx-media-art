# Jellyfish Dancers

[한국어](#한국어) | [English](#english)

## 한국어

웹캠과 네트워크 센서 입력에 반응하는 Unity 기반 인터랙티브 미디어 아트 프로젝트입니다. 관객의 실루엣과 센서 신호를 해파리 군집, 파티클, 화면 연출로 변환합니다.

### 프로젝트 정보

| 항목 | 내용 |
| --- | --- |
| 개발 기간 | 2026-06-16 - 2026-06-30 |
| 리팩터링 기간 | 2026-06-22 - 2026-06-29 |
| 인원 | 1인 |
| 엔진 | Unity 6000.0.62f1 |
| 렌더링 | URP 17.0.4, Visual Effect Graph |

### 핵심 구현

- 웹캠 세그멘테이션 결과를 셰이더와 VFX 입력으로 전달
- TCP 및 UDP/OSC 센서 입력을 공통 이벤트 흐름으로 변환
- 센서, 웹캠, 테스트 입력을 한 단계에서 조합하는 입력 파이프라인
- 해파리 군집, 관객 실루엣, 음악 신호를 VFX 파라미터로 연결
- 전시 환경을 위한 스테이지 모드와 멀티 디스플레이 초기화

### 구현 이유

입력 장치마다 게임 로직이 달라지는 문제를 막기 위해 센서 수신, 메시지 해석, 이벤트 전달, VFX 반영을 분리했습니다. 실제 하드웨어 편차는 설정값으로 조정할 수 있게 유지했습니다.

### 빌드 사양

| Target | 상태 | 비고 |
| --- | --- | --- |
| Windows x86_64 | 주 빌드 | 웹캠, TCP/OSC 센서, 멀티 디스플레이 전시 환경 |
| WebGL | 실험 빌드 | 브라우저 카메라 권한과 네트워크 제약 확인 필요 |
| Android ARM64 | 실험 빌드 | 카메라 권한과 VFX 성능 확인 필요 |

세 플랫폼은 `Project.Editor.BuildPlatforms.Build` 하나로 빌드하며 `-buildTarget`, `-buildOutput`, `-buildReport` 인자로 대상을 구분합니다.

### 리팩터링

- 중복 해파리 스포너와 사용하지 않는 에셋 제거
- 센서 입력과 TV Universe VFX 계약 통합
- `JellyfishAgent` 중심으로 군집 동작 파이프라인 정리
- 공개 저장소에서 로컬 도구와 검증 산출물 제거

### 에셋 및 외부 코드

- [SelfieSegmentationBarracuda](https://github.com/creativeIKEP/SelfieSegmentationBarracuda)
- [Unity Logs Viewer](https://assetstore.unity.com/packages/tools/integration/unity-logs-viewer-12047)
- 각 포함 패키지의 라이선스 파일을 우선 적용

### 영상 및 업데이트

- 전시 전체 흐름, 웹캠 실루엣, 센서 반응 영상을 추가할 예정입니다.
- Windows 빌드를 기준으로 검증한 뒤 WebGL과 Android 상태를 갱신합니다.

## English

An interactive Unity media-art project that transforms webcam silhouettes and network sensor signals into jellyfish crowds, particles, and multi-display visuals.

### Project

- Development: 2026-06-16 - 2026-06-30
- Refactoring: 2026-06-22 - 2026-06-29
- Team: Solo
- Engine: Unity 6000.0.62f1, URP 17.0.4, Visual Effect Graph

### Highlights

- Webcam segmentation drives shaders and VFX parameters.
- TCP and UDP/OSC inputs share one event pipeline.
- Sensor, webcam, and test inputs are aggregated before presentation logic.
- Stage modes and multi-display startup support an installation environment.
- One build entry point targets Windows, WebGL, and Android; Windows is the primary verified target.

### Why This Structure

Transport, message decoding, event routing, and VFX binding are separate so device-specific changes do not spread into presentation code. Hardware differences remain adjustable through calibration settings.

### Build Targets

| Target | Status | Notes |
| --- | --- | --- |
| Windows x86_64 | Primary | Webcam, TCP/OSC sensors, and multi-display installation |
| WebGL | Experimental | Browser camera permissions and network limits require validation |
| Android ARM64 | Experimental | Camera permissions and VFX performance require validation |

All targets use `Project.Editor.BuildPlatforms.Build` with `-buildTarget`, `-buildOutput`, and `-buildReport` arguments.

### Refactoring

- Removed duplicate jellyfish spawners and unused assets.
- Unified sensor input and TV Universe VFX contracts.
- Reorganized crowd behavior around `JellyfishAgent`.
- Removed local tooling and verification artifacts from the public repository.

### Stack and Assets

`Unity 6` `C#` `URP` `Visual Effect Graph` `Barracuda` `TCP` `UDP/OSC`

- [SelfieSegmentationBarracuda](https://github.com/creativeIKEP/SelfieSegmentationBarracuda)
- [Unity Logs Viewer](https://assetstore.unity.com/packages/tools/integration/unity-logs-viewer-12047)
- Original licenses included with each package take precedence.

### Lessons

Separating transport, decoding, event routing, and VFX binding kept hardware-specific changes out of the presentation logic while preserving calibration controls for real sensors.

### Video and Updates

An installation-flow video covering webcam silhouettes and sensor reactions will be added after Windows validation. WebGL and Android status will then be updated.
