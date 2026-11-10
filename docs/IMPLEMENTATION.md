# 구현 상태 및 Phase 기록

## Phase 1 — 초기화

파일: Directory.Build.props, MarketingAIAgent.slnx, src/*/*.csproj, Domain/Models.cs·Interfaces.cs, Application/AgentWorkflow.cs. 기존 폴더가 비어 신규 생성했습니다. .NET 10.0.300 SDK / net10.0-windows. 초기에는 붙여넣은 요구사항으로 시작했으며 후속 제공된 원본 PDF 전체를 대조했습니다 (PDF_REQUIREMENTS_REVIEW.md). 이후 전체 Release 빌드에서 검증했습니다.

## Phase 2 — 대시보드

파일: App/App.xaml·App.xaml.cs, MainWindow.xaml·cs, MainViewModel.cs, Controls/VideoCard.xaml·cs, Styles/Dark.xaml. 다크 테마·좌측 탭·3열/구분선 크기 조절·검색/분석/게시 패널·설정·활동 이력. 빌드 통과. 실제 화면·좁은 창·고DPI QA는 앱 제어 권한 미승인으로 미검증입니다. 완전한 Fluent 컨트롤 라이브러리 도입 및 좁은 창 단일 열 재배치는 미구현입니다.

## Phase 3 — YouTube

파일: Infrastructure/ApiServices.cs의 YouTubeService. 검색 date/viewCount, pageToken, 상세/조회수, 인기 댓글, URL/Shorts, HTML 제목 디코딩, 중복·댓글 비활성화·할당량 등 처리. HTTP 모의 테스트 통과. 제공된 실제 키로 검색 호출이 통과했습니다.

## Phase 4 — Gemini

파일: ApiServices.cs의 GeminiAnalysisService. 설정 모델, responseJsonSchema, JSON 응답 파싱, 근거·한국어·길이·홍보 표시·링크 검증. 자막 미확보 명시. HTTP 모의/응답 검증 테스트 통과. 실패 대체 데이터 없음. 실제 gemini-3.1-flash-lite 호출과 구조화·근거·길이 검증이 통과했습니다. 원문 근거 enum과 최대 2회 검증 재생성을 추가했습니다. 자막 획득 어댑터는 미구현입니다.

## Phase 5 — SQLite

파일: Infrastructure/Persistence.cs·Security.cs. 요구 테이블 7개 + ViewSnapshots. 영상/분석/편집 초안/프로필/설정/이력 영속성. DPAPI 키 저장, AppData 경로. 실제 SQLite 저장/재조회 및 재시작 테스트 통과. EF 마이그레이션 배포는 미구현입니다.

## Phase 6 — Chrome

파일: Infrastructure/BrowserService.cs. 유튜브1~3, 독립 persistent context, headed Chrome, 핸들 기반 계정 확인, 조건 대기, 한국어 순차 입력, 최종 승인, 링크 검증, 실패 캡처. 빌드 통과. 독립 Chrome persistent context와 로컬 한국어 순차 입력 테스트는 통과했습니다. 실제 로그인·YouTube 셀렉터·게시 검증은 미실행입니다. Chrome 버전에 따른 Playwright 동작 확인이 필요합니다.

## Phase 7 — 게시 제한

파일: Domain/Rules, Application/AgentWorkflow.cs, Persistence.cs. 서울 날짜·하루 10회 기본 상한·최대 10회, 시도 이력, 영상 보호 유일 인덱스, BEGIN IMMEDIATE, 클릭 전/후 취소 처리, 재시작 후 보호 유지. SQLite 동시 예약·상한·실패 복구 테스트 통과. 수동 보호 해제 기능은 의도적으로 제공하지 않습니다.

## Phase 8 — 추가 기능

URL 즉석 분석·3개 프로필 이름/경로 관리·실제 조회수 스냅샷·측정 간격과 증가율/후보 여부 표시. 성장률 계산 테스트 통과. 성장 후보 탐색 버튼에서 최신 20개 영상의 실측 조건을 필터링하고 게시를 비활성화합니다. 정기 샘플링·전체 페이지 자동 순회·사용자 자막 가져오기는 미구현입니다.

## Phase 9 — 테스트

파일: tests/MarketingAIAgent.Tests/{RulesTests,StoreTests,ApiTests,WorkflowTests,LiveTests}. 단위·HTTP 모의·실제 SQLite 통합. 실서비스 테스트는 명시적으로 켜며 실제 게시를 자동 실행하지 않습니다. 최종 숫자와 결과는 TEST_REPORT.md에 기록합니다.

## Phase 10 — 배포

파일: scripts/publish.ps1, installer.iss, App/app.manifest. 자체 포함 win-x64 publish와 Inno Setup 사용자별 설치. Release 산출물은 artifacts에 생성합니다. 설치/제거 검증 및 화면 실행 상태는 TEST_REPORT.md 참조. 코드 서명·다른 깨끗한 PC 검증은 미실행입니다.


