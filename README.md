# Jellyfish Dancers

- 웹캠 및 센서 네트워크 입력에 실시간으로 반응하는 Unity 기반 인터랙티브 미디어 아트 프로젝트입니다.
- 관객의 실루엣과 외부 센서 신호를 활용하여 해파리 군집, 파티클 이펙트 등 다채로운 화면 연출을 만들어냅니다.

README 업데이트: 2026-07-01

> 📷 **영상 및 이미지**
> *(여기에 영상 또는 이미지 추가 예정)*

### Test Video

<video src="The-Living-Frame_Unity-VFX-Media-Art/Builds/Windows/Jellyfish%20Dancers%20Windows%20Build%20Test.mp4" controls></video>

### 프로젝트 정보

| 항목 | 내용 |
| --- | --- |
| 개발 기간 | 2026-06-16 - 2026-06-30 |
| 리팩터링 이력 | 1차: 2026-06-22 - 2026-06-29 |
| 인원 | 1인 |
| 엔진 | Unity 6000.0.62f1 |
| 렌더링 | URP 17.0.4, Visual Effect Graph |

### 기술 스택
<p>
  <img src="https://img.shields.io/badge/Unity-000000?style=flat-square&logo=unity&logoColor=white"/>
  <img src="https://img.shields.io/badge/C%23-239120?style=flat-square&logo=c-sharp&logoColor=white"/>
  <img src="https://img.shields.io/badge/URP-000000?style=flat-square"/>
  <img src="https://img.shields.io/badge/Visual Effect Graph-000000?style=flat-square"/>
  <img src="https://img.shields.io/badge/Barracuda-000000?style=flat-square"/>
  <img src="https://img.shields.io/badge/TCP%2FUDP%2FOSC-0055FF?style=flat-square"/>
</p>

### 외부 라이브러리 연동
- [SelfieSegmentationBarracuda](https://github.com/creativeIKEP/SelfieSegmentationBarracuda) 미디어파이프 연동
- [Unity Logs Viewer](https://assetstore.unity.com/packages/tools/integration/unity-logs-viewer-12047) 활용

### 프로젝트 구조
```text
(프로젝트 구조도 추가 예정)
```

### 플레이 및 구동 방법
*(멀티 디스플레이 설정, 웹캠 및 센서 활성화 방법 등 작성 예정)*

### 핵심 구현

- 웹캠 세그멘테이션 결과를 셰이더와 VFX 입력으로 전달
- TCP 및 UDP/OSC 센서 입력을 공통 이벤트 흐름으로 변환
- 센서, 웹캠, 테스트 입력을 한 단계에서 조합하는 입력 파이프라인
- 해파리 군집, 관객 실루엣, 음악 신호를 VFX 파라미터로 연결
- 전시 환경을 위한 스테이지 모드와 멀티 디스플레이 초기화

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
- 검증 산출물과 로컬 도구 흔적은 public 에서 제외

### 업데이트 계획

- 사용한 에셋 출처 표기 예정
