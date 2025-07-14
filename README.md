# SumTube - YouTube 영상 AI 요약 프로그램

SumTube는 YouTube 영상의 자막/스크립트를 추출하고 Ollama의 AI 모델을 사용하여 한국어로 상세한 요약을 생성하는 포터블 애플리케이션입니다.

## ✨ 주요 기능

- 🎬 **YouTube 스크립트 추출**: yt-dlp를 사용하여 자막/스크립트 자동 추출
- 🤖 **AI 기반 요약**: Ollama AI 모델로 상세한 한국어 요약 생성
- 📦 **완전한 포터블**: 시스템 종속성 없이 독립 실행 가능
- 🔄 **자동 업데이트**: 실행 시마다 yt-dlp와 Ollama 최신 버전 확인 및 업데이트
- 🌐 **다국어 자막 지원**: 한국어, 영어, 자동 생성 자막 지원
- 🎯 **상세한 요약**: 단순 요약이 아닌 구조화된 상세 분석
- ⚙️ **설정 파일 지원**: 모든 설정을 JSON 파일로 쉽게 커스터마이징
- 🚀 **향상된 다운로드 진행률**: 속도, ETA, 진행바가 포함된 실시간 진행률 표시
- 🎯 **명령줄 모델 선택**: 원하는 AI 모델을 명령줄에서 직접 지정 가능
- 🔐 **고급 모델 검증**: 모델 무결성 검증 및 자동 재다운로드 기능
- 🐛 **디버그 모드**: 상세한 로그 출력으로 문제 진단 지원

## 🚀 사용법

### 기본 사용법SumTube.exe --url "https://www.youtube.com/watch?v=VIDEO_ID"
### 커스텀 모델 사용SumTube.exe --url "https://www.youtube.com/watch?v=VIDEO_ID" --model "llama3.1:8b"
### 디버그 모드SumTube.exe --url "https://www.youtube.com/watch?v=VIDEO_ID" --debug
### 모든 옵션 조합SumTube.exe --url "https://www.youtube.com/watch?v=VIDEO_ID" --model "gemma2:9b" --debug
### 단축 옵션SumTube.exe -u "https://www.youtube.com/watch?v=VIDEO_ID" -m "gemma2:9b" -d
### 배치 파일 사용# 기본 모델로 실행
SumTube.bat "https://www.youtube.com/watch?v=VIDEO_ID"

# 커스텀 모델로 실행
SumTube.bat "https://www.youtube.com/watch?v=VIDEO_ID" "llama3.1:8b"
### 명령줄 옵션

| 옵션 | 단축 | 설명 | 필수 |
|------|------|------|------|
| `--url` | `-u` | YouTube 영상 URL | ✅ |
| `--model` | `-m` | 사용할 AI 모델 이름 | ❌ |
| `--debug` | `-d` | 디버그 모드 활성화 | ❌ |

### 사용 가능한 모델 예시
- `exaone3.5:7.8b` (기본값)
- `llama3.1:8b`
- `llama3.1:70b`
- `gemma2:9b`
- `qwen2:7b`
- `codellama:7b`

## 🐛 디버그 모드

디버그 모드를 활성화하면 상세한 내부 작업 로그를 확인할 수 있습니다.

### 디버그 모드 활성화SumTube.exe --url "URL" --debug
### 디버그 로그 정보
- **타임스탬프**: 각 작업의 정확한 시간
- **카테고리별 로그**: STARTUP, RUNTIME, OLLAMA, MODEL, API, YOUTUBE, AI 등
- **성능 메트릭**: 각 작업의 소요 시간 및 상세 정보
- **HTTP 요청/응답**: API 호출 및 응답 상세 정보
- **프로세스 추적**: 외부 프로세스 실행 및 종료 상태
- **파일 I/O**: 파일 읽기/쓰기 작업 추적
- **설정 정보**: 로딩된 구성 설정 (민감한 정보 마스킹)

### 디버그 로그 예시🐛 [14:23:45.123] [STARTUP] Command line arguments parsed successfully
🐛 [14:23:45.125] [STARTUP] Arguments: --url https://youtube.com/watch?v=abc123 --debug
🐛 [14:23:45.127] [RUNTIME] Initializing runtime setup service
🐛 [14:23:45.890] [OLLAMA] Starting Ollama process from path: C:\SumTube\runtime\ollama\ollama.exe
🐛 [14:23:46.123] [MODEL] Starting model validation for: exaone3.5:7.8b
🐛 [14:23:47.456] [API] Testing Ollama API connection
🐛 [14:23:47.567] [YOUTUBE] Starting transcript extraction for URL: https://youtube.com/watch?v=abc123
🐛 [14:23:52.123] [AI] Starting AI summary generation
🐛 [14:24:15.789] [PERF] AI Summary Generation: 23666ms
### 디버그 모드 사용 시나리오
1. **문제 진단**: 오류 발생 시 상세한 로그로 원인 파악
2. **성능 분석**: 각 단계별 소요 시간 확인
3. **개발 및 테스트**: 내부 동작 과정 모니터링
4. **네트워크 이슈**: API 호출 및 다운로드 과정 추적

## 📁 디렉토리 구조

프로그램 실행 후 다음과 같은 구조가 생성됩니다:SumTube/
├── SumTube.exe
├── SumTube.bat                # 간편 실행 스크립트
├── appsettings.json           # 설정 파일 (사용자 커스터마이징 가능)
├── runtime/                   # 포터블 런타임 환경
│   ├── python/               # 임베디드 Python 환경
│   │   ├── python.exe
│   │   ├── Scripts/
│   │   │   └── yt-dlp.exe
│   │   └── ...
│   ├── ollama/               # 포터블 Ollama 환경
│   │   ├── ollama.exe
│   │   └── models/
│   │       └── [다운로드된 모델들]
│   └── versions.json         # 버전 정보 캐시
└── ...
## 🔐 고급 모델 검증 시스템

SumTube는 AI 모델의 신뢰성을 보장하기 위해 다단계 검증 시스템을 사용합니다:

### 검증 단계:
1. **📋 모델 존재 확인**: Ollama에서 모델 등록 여부 확인
2. **🔍 무결성 검증**: 모델 정보 조회를 통한 손상 여부 확인
3. **🧪 기능 테스트**: 실제 프롬프트를 통한 응답 생성 테스트
4. **🔄 자동 복구**: 검증 실패 시 모델 재다운로드

### 검증 결과 표시:🔍 exaone3.5:7.8b 모델을 확인하고 있습니다...
🔐 exaone3.5:7.8b 모델 무결성을 검증하고 있습니다...
🧪 exaone3.5:7.8b 모델 기능을 테스트하고 있습니다...
✅ exaone3.5:7.8b 모델이 검증되었습니다. (소요시간: 2.3초)

📊 모델 검증 완료:
   • 모델명: exaone3.5:7.8b
   • 검증 시간: 2.3초
   • 모델 정보: exaone (7.8B)
### 자동 복구 과정:⚠️ exaone3.5:7.8b 모델 무결성 검증 실패. 재다운로드합니다...
🗑️ 손상된 exaone3.5:7.8b 모델을 제거하고 있습니다...
✅ exaone3.5:7.8b 모델이 제거되었습니다.
📥 exaone3.5:7.8b 모델을 다운로드하고 있습니다...
[████████████████████] 100% | 4.3 GB/4.3 GB | 2.1 MB/s | ETA: 00:00
✅ exaone3.5:7.8b 모델이 재다운로드 후 검증되었습니다.
## 🎯 향상된 다운로드 진행률

SumTube는 대용량 파일 다운로드 시 다음과 같은 상세한 진행률을 표시합니다:📥 Python 임베디드 패키지 다운로드를 시작합니다...
[████████████████████] 100% | 24.1 MB/24.1 MB | 1.2 MB/s | ETA: 00:00
✅ Python 임베디드 패키지 다운로드 완료 (1.1 MB/s 평균 속도)
### 진행률 표시 정보:
- **시각적 진행바**: 현재 진행 상황을 한눈에 파악
- **퍼센티지**: 정확한 완료율 표시
- **파일 크기**: 현재/전체 다운로드 크기
- **실시간 속도**: 현재 다운로드 속도
- **예상 완료 시간 (ETA)**: 남은 시간 추정
- **평균 속도**: 전체 다운로드의 평균 속도

## ⚙️ 설정 파일 (appsettings.json)

SumTube는 `appsettings.json` 파일을 통해 모든 설정을 커스터마이징할 수 있습니다:

### AI 모델 설정{
  "Ollama": {
    "Port": 11435,
    "DefaultModel": "exaone3.5:7.8b",
    "ModelValidation": {
      "EnableIntegrityCheck": true,
      "EnableFunctionalTest": true,
      "TestPrompt": "안녕하세요",
      "ExpectedResponseLength": 5,
      "ValidationTimeoutSeconds": 30,
      "RetryAttempts": 2
    },
    "ApiOptions": {
      "Temperature": 0.3,
      "TopP": 0.9,
      "MaxTokens": 4096
    }
  }
}
### 다운로드 URL 설정{
  "Downloads": {
    "PythonEmbeddedUrl": "https://www.python.org/ftp/python/3.12.0/python-3.12.0-embed-amd64.zip",
    "OllamaUrl": "https://github.com/ollama/ollama/releases/latest/download/ollama-windows-amd64.zip"
  }
}
### 업데이트 설정{
  "Updates": {
    "CheckIntervalHours": 24,
    "RetryAttempts": 3
  }
}
### YouTube 처리 설정{
  "YouTube": {
    "SubtitleLanguagePriority": ["ko", "en", "en.*"],
    "MaxTranscriptLength": 15000
  }
}
## 🔧 시스템 요구사항

- **운영체제**: Windows 10/11 (64-bit)
- **프레임워크**: .NET 10 (자동 설치됨)
- **메모리**: 최소 4GB RAM (AI 모델 로딩용)
- **디스크**: 최소 10GB 여유 공간 (Python, Ollama, 모델 다운로드용)
- **네트워크**: 인터넷 연결 (초기 설정 및 업데이트용)

## 🎯 첫 실행 시 자동 구성

첫 실행 시 다음이 자동으로 다운로드되고 구성됩니다:

1. **Python 3.12 임베디드 버전** (~25MB)
2. **yt-dlp 최신 버전** (~3MB)
3. **Ollama 최신 버전** (~500MB)
4. **지정된 AI 모델** (크기는 모델에 따라 다름)
   - exaone3.5:7.8b (~4.3GB)
   - llama3.1:8b (~4.7GB)
   - gemma2:9b (~5.4GB)

> ⏱️ 첫 실행은 선택한 모델과 인터넷 속도에 따라 10-30분 정도 소요될 수 있습니다.

## 📋 요약 결과 형식

SumTube가 생성하는 요약은 다음과 같은 구조를 가집니다:## 📌 영상 요약

### 🎯 핵심 주제
[영상의 주요 주제와 목적]

### 📋 주요 내용
[영상의 핵심 내용을 체계적으로 정리]

### 💡 핵심 포인트
[기억해야 할 중요한 점들]

### 🎯 결론 및 시사점
[영상의 결론과 인사이트]
## 🛠️ 문제 해결

### 자막을 찾을 수 없는 경우⚠️ 자막을 찾을 수 없습니다.- 해당 영상에 자막이 없거나 비공개 상태일 수 있습니다
- 다른 영상을 시도해보세요

### Ollama 연결 실패❌ Ollama 서버에 연결할 수 없습니다.- 방화벽에서 설정된 포트를 허용하세요 (기본: 11435)
- 프로그램을 관리자 권한으로 실행해보세요
- **디버그 모드로 실행**하여 상세한 연결 과정을 확인하세요

### 모델 다운로드 실패❌ 모델 다운로드에 실패했습니다.- 인터넷 연결을 확인하세요
- 디스크 공간이 충분한지 확인하세요 (최소 5GB)
- **디버그 모드로 실행**하여 다운로드 과정을 추적하세요

### 모델 검증 실패❌ 모델 검증 실패: 모델이 손상되었습니다.- 프로그램이 자동으로 모델을 재다운로드합니다
- 네트워크가 안정적인지 확인하세요
- 디스크 공간이 충분한지 확인하세요
- **디버그 모드로 실행**하여 검증 과정의 상세 정보를 확인하세요

### 다운로드 속도가 느린 경우
- 다운로드 진행률에서 실시간 속도와 ETA를 확인하세요
- 네트워크 상황에 따라 속도가 달라질 수 있습니다
- 큰 모델(70B 등)은 상당한 시간이 소요됩니다
- **디버그 모드로 실행**하여 네트워크 상태를 모니터링하세요

### 디버그 모드 활용
문제가 발생했을 때 디버그 모드를 사용하면:SumTube.exe --url "YOUR_URL" --debug- 상세한 오류 정보 확인
- 각 단계별 진행 상황 추적
- 성능 병목 지점 파악
- API 호출 및 응답 상태 확인

## 🔄 업데이트

SumTube는 설정에 따라 자동으로 업데이트를 확인합니다:

- **설정된 간격 내 확인한 경우**: 스킵 (기본: 24시간)
- **새 버전 발견 시**: 백그라운드에서 조용히 업데이트
- **업데이트 과정**: 모든 과정이 향상된 진행률 표시와 함께 진행

## 🎮 고급 설정

### 커스텀 AI 모델 사용
명령줄에서 직접 지정하거나 `appsettings.json`에서 변경:{
  "Ollama": {
    "DefaultModel": "llama3.1:8b"
  }
}
### 모델 검증 설정
검증 기능을 세밀하게 제어할 수 있습니다:{
  "Ollama": {
    "ModelValidation": {
      "EnableIntegrityCheck": false,
      "EnableFunctionalTest": true,
      "TestPrompt": "Hello",
      "ValidationTimeoutSeconds": 60,
      "RetryAttempts": 3
    }
  }
}
### 커스텀 포트
기본 포트 충돌 시 변경 가능:{
  "Ollama": {
    "Port": 11436
  }
}
### 자막 언어 우선순위
선호하는 언어 순서 설정:{
  "YouTube": {
    "SubtitleLanguagePriority": ["en", "ko", "ja"]
  }
}
### 다운로드 성능 튜닝{
  "Runtime": {
    "BufferSize": 16384,
    "MaxDownloadRetries": 5
  }
}
## 📞 지원

문제가 발생하면 다음을 확인하세요:

1. **인터넷 연결** 상태
2. **디스크 공간** (최소 10GB)
3. **방화벽 설정** (설정된 포트)
4. **관리자 권한** 실행
5. **appsettings.json** 파일 형식 확인
6. **다운로드 진행률** 확인 (중단되지 않았는지)
7. **모델 검증 결과** 확인
8. **디버그 모드 로그** 확인 (문제 진단용)

## 📝 라이선스

이 프로젝트는 개인 및 교육 목적으로 자유롭게 사용할 수 있습니다.

## 🙏 감사의 말

- **yt-dlp**: YouTube 영상 다운로드 및 정보 추출
- **Ollama**: 로컬 AI 모델 실행 환경
- **LG AI Research**: exaone3.5 모델 개발
- **Meta**: Llama 모델 개발
- **Google**: Gemma 모델 개발
- **Microsoft**: .NET 10 플랫폼

---

**SumTube** - YouTube 영상을 더 스마트하게 소비하세요! 🎬✨