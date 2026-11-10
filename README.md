# Marketing AI Agent

Windows x64용 WPF 마케팅 댓글 검토 도구입니다. 실제 YouTube Data API로 영상을 탐색하고 Gemini의 JSON Schema 구조화 출력으로 근거가 있는 한국어 초안을 생성합니다. 사용자가 수정·검토한 댓글만 독립 Chrome 프로필에서 최종 승인 후 게시합니다. 허위 이용 경험 대신 **운명원 홍보** 목적을 표시합니다.

## 실행 및 설치

* 설치 파일: `artifacts/installer/MarketingAIAgent-1.0.1-win-x64-Setup.exe`
* 포터블 실행 파일: `artifacts/publish/win-x64/MarketingAIAgent.exe` (폴더 전체를 함께 보관)
* Windows x64, Google Chrome 필요. 자체 포함 배포본에는 .NET 10 런타임이 포함됩니다.
* 설치는 현재 Windows 사용자에게 적용됩니다. 사용자 데이터는 설치 폴더와 분리하여 `%LOCALAPPDATA%\MarketingAIAgent`에 저장합니다.

## API 설정

설정 화면에서 YouTube API Key, Gemini API Key와 사용 가능한 Gemini 모델을 입력하고 저장합니다. YouTube 프로젝트에는 Data API v3가 활성화되어 있어야 합니다. Gemini 키는 해당 모델에 접근할 수 있어야 합니다. 모델 기본값은 설정에서 변경 가능합니다. 키는 PasswordBox로 입력하며 저장된 키를 화면에 다시 표시하지 않습니다. 빈 입력은 기존 키를 유지합니다.

현재 Windows 사용자에는 제공된 키를 이미 가져왔습니다. 다른 계정에서 설정할 때는 화면에서 입력하거나 `./scripts/import-api-keys.ps1`로 로컬 JSON 형식 키 파일을 가져올 수 있습니다. 원본 키 파일은 패키지에 넣지 않으며 Git에서 제외합니다.

키는 Windows DPAPI CurrentUser로 암호화한 `.protected` 파일입니다. 다른 Windows 계정에서는 재입력이 필요합니다. HTTP 키는 헤더로 전달하고 HttpClient 자동 로깅을 제거했습니다. 로그에는 예외 원문·API 응답·키를 기록하지 않습니다. DB와 브라우저 프로필은 암호화되지 않으므로 사용자 계정에서 보호해 관리하세요.

## Chrome 프로필

유튜브1·유튜브2·유튜브3의 독립 프로필이 처음 실행 시 만들어집니다. 설정에서 이름과 절대 데이터 경로를 수정할 수 있습니다. 기본 Chrome User Data 경로는 차단합니다. 자동 프로필 순환은 없습니다.

1. 프로필을 선택하고 **Chrome 열기 · 로그인 확인**을 누릅니다.
2. 열린 Chrome에서 사용자가 직접 로그인합니다. CAPTCHA/추가 인증은 직접 처리합니다.
3. 앱에서 로그인 확인을 다시 눌러 채널 핸들을 확인합니다.
4. 같은 프로필을 다른 Chrome/프로그램에서 동시에 사용하지 마세요.

Playwright는 설치된 Chrome 채널을 사용하므로 별도 Chromium 다운로드는 필요하지 않습니다. `.playwright` 폴더와 모든 배포 파일을 유지하세요. 로그인 흐름이 자동화 Chrome을 허용하지 않거나 UI가 바뀌면 앱은 우회하지 않고 중단합니다.

## 사용 방법

1. 연애 관련 키워드와 최신순/조회수순으로 검색하거나 YouTube/Shorts URL을 입력합니다.
2. 영상을 선택하고 AI 분석을 실행합니다. URL 입력은 즉시 분석합니다.
3. 제목·설명에서 확인된 근거, 인기 댓글, 자막 미확보 안내를 검토합니다.
4. 40~120자 초안을 수정하고 저장합니다. 근거 구절·운명원·홍보 표기를 유지하고 링크/해시태그를 제거합니다.
5. 로그인 계정을 확인하고 게시 검토 완료를 체크합니다.
6. 게시 버튼을 누르면 Chrome 입력 후 **영상·현재 계정·정확한 댓글** 최종 승인 창이 표시됩니다.
7. 성공 링크 또는 실패/결과 미확인 상태와 캡처 경로를 기록에서 확인합니다.

검색 페이지 토큰은 현재 검색 세션에 유지됩니다. 검색 영상과 분석·초안은 DB에 저장되어 재시작 시 복원됩니다. 변경한 댓글을 유지하려면 초안 저장을 누르세요. 서로 다른 영상에서 동일한 성공 댓글 재사용도 차단합니다.

## 게시 제한과 복구

영상 전체에 대해 성공·진행 중·결과 미확인 기록이 있으면 프로필과 무관하게 자동 재게시하지 않습니다. 일일 기본 상한은 서울 날짜 기준 10회이고 1~10회로 낮출 수 있습니다. 안전상 실패·취소를 포함한 모든 예약된 게시 시도를 상한에 포함합니다. 최종 승인을 취소해도 시도 이력은 남습니다.

SQLite `BEGIN IMMEDIATE` 트랜잭션과 nullable 유일 인덱스로 동시 실행에서도 예약·상한을 직렬화합니다. 클릭 직전 Posting을 디스크에 저장하고 이후 오류·취소는 VerificationUnknown으로 유지합니다. 강제 종료로 남은 진행 중 상태 역시 차단을 유지하며 자동 해제하지 않습니다. YouTube에서 직접 확인하고 이력을 보존하세요. 앱 내 수동 재게시 해제 기능은 제공하지 않습니다.

## 데이터와 경로

기본 루트: `%LOCALAPPDATA%\MarketingAIAgent`

* `agent.db`: Videos, VideoAnalyses, CommentDrafts, ChromeProfiles, CommentPosts, AppSettings, ActivityLogs, ViewSnapshots
* `youtube.protected`, `gemini.protected`: DPAPI 키
* `profiles/`: 독립 Chrome 사용자 데이터
* `logs/`: 일별 Serilog 로그 (14개 파일 보관)
* `screenshots/`: 브라우저 실패 캡처
* `paths.json`: 재시작 시 적용할 비밀 없는 경로 설정

설정의 DB·로그 경로 변경은 재시작 후 적용하며 기존 파일을 자동 복사하지 않습니다. 프로필 루트는 새 DB를 처음 초기화할 때 기본 프로필 생성에 사용하고, 기존 프로필은 각각의 데이터 경로를 유지합니다. 데이터베이스는 현재 버전에서 EnsureCreated로 초기화하며 이후 스키마 업그레이드 마이그레이션은 아직 제공하지 않습니다. 제거 프로그램은 사용자 데이터를 삭제하지 않습니다.

## 기술 스택과 구조

.NET 10 LTS / C# / WPF / CommunityToolkit.Mvvm / EF Core SQLite / Playwright / Generic Host·DI·HttpClientFactory / Serilog / System.Text.Json / DPAPI / xUnit / Inno Setup.

`App → Application → Domain`, `Infrastructure → Application + Domain`. ViewModel은 서비스에 작업을 위임하며 SQL·HTTP·Playwright 코드를 포함하지 않습니다. 재사용 영상 카드, 다크 테마와 크기 조절 가능한 3열 패널을 제공합니다.

## 빌드·테스트

```powershell
dotnet restore MarketingAIAgent.slnx
dotnet build MarketingAIAgent.slnx -c Release --no-restore
dotnet test MarketingAIAgent.slnx -c Release --no-build --no-restore --logger trx --results-directory artifacts/test-results
dotnet publish src/MarketingAIAgent.App -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
./scripts/publish.ps1 -Installer
```

설치 파일 생성에는 Inno Setup 6가 필요합니다. NuGet 취약점 감사를 끄지 않습니다. SQLite 네이티브 번들은 전이 의존성의 취약 버전을 피하도록 3.0.5를 직접 지정했습니다.

실제 API 및 headed Chrome 로컬 입력 테스트는 Windows 사용자 설정을 준비한 후 다음처럼 명시적으로 실행합니다. API 과금/할당량이 소비될 수 있습니다. 실댓글 게시 테스트는 자동 실행하지 않습니다.

```powershell
$env:MARKETING_AGENT_LIVE_TESTS = '1'
dotnet test tests/MarketingAIAgent.Tests -c Release --filter FullyQualifiedName~LiveTests
```

## 오류 정책과 제한

인증·할당량·네트워크·댓글 비활성화·빈 결과를 구분합니다. Gemini 실패 시 성공 데이터를 대체 생성하지 않습니다. 구조화 출력이라도 로컬 검증 실패 시 이유를 전달해 최대 2회 다시 생성하고, 끝까지 실패하면 오류로 중단합니다. HTTP 인증/과부하/할당량 오류에는 자동 재시도하지 않습니다. 자동 게시 재시도는 없습니다. 셀렉터·계정 확인 실패 시 중단합니다. 앱 취소는 토큰과 브라우저 컨텍스트 종료로 처리합니다.

**자막 자동 수집은 미구현**입니다. 임의 영상의 자막 다운로드를 YouTube API 키만으로 보장하지 않으므로 메타데이터와 접근 가능한 댓글만 사용하고 UI에 표시합니다. 댓글을 영상 속 발언으로 취급하지 않습니다. 근거의 연속 구절과 실제 포함 여부는 검증하지만 전체 의미의 정확성·후기 진위는 사용자의 검토가 필요합니다.

조회수는 실제 스냅샷을 저장합니다. 선택 시 1분 이상 간격의 첫/마지막 관측값에서 시간당 증분을 계산하고 1,000~5,000회·150회/시간 이상 후보 여부를 표시합니다. 단일 조회수/업로드 시각의 평균 속도를 실측 증가율로 표시하지 않습니다. 성장 후보 탐색 버튼은 현재 검색어의 최신 20개 영상을 수집해 같은 조건의 후보만 표시합니다. 1분 이후 다시 탐색하여 실측값을 비교하세요. 이 모드는 탐색·초안 전용으로 게시가 비활성화됩니다. 정기 샘플링·전체 검색 결과 자동 순회는 제공하지 않습니다.

원본 `track-2-marketing-agent.pdf`를 대조했고 제공된 키로 실제 YouTube 검색·Gemini 구조화 분석·Chrome 로컬 입력 테스트가 통과했습니다. 현재 검증 모델은 `gemini-3.1-flash-lite`입니다. 원본의 `gemini-2.5-flash`는 신규 사용자 지원 중단, `gemini-3.8-flash`는 반복 과부하로 사용할 수 없었습니다. 실제 YouTube 게시 및 앱 화면 검증 여부는 [검증 보고서](docs/TEST_REPORT.md)를 확인하세요. 설치 파일은 코드 서명되지 않았습니다. 상업 배포 전 서명·실계정 수동 QA·스키마 마이그레이션·UI 접근성 검증이 필요합니다.

공식 참고: [YouTube 검색](https://developers.google.com/youtube/v3/docs/search/list), [인기 댓글](https://developers.google.com/youtube/v3/docs/commentThreads/list), [Gemini 구조화 출력](https://ai.google.dev/gemini-api/docs/structured-output), [Playwright persistent context](https://playwright.dev/dotnet/docs/api/class-browsertype#browser-type-launch-persistent-context).



