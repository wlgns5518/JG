# 검증 보고서

검증일: 2026-11-10 (Asia/Seoul). Windows x64 / .NET SDK 10.0.300 / Inno Setup 6.7.3.

| 항목 | 결과 | 실제 근거 |
|---|---|---|
| NuGet restore | PASS | 최종 release-build.log, 취약점 감사 활성 |
| Release build | PASS | 경고 0개·오류 0개 |
| 자동 테스트 | PASS | 43개 통과, 0개 실패, 기본 실행에서 실서비스 3개 건너뜀 (전체 46개). 실서비스 3개는 별도 실행해 모두 PASS |
| win-x64 자체 포함 publish | PASS | artifacts/publish/win-x64/MarketingAIAgent.exe 및 런타임 |
| 설치 파일 생성 | PASS | Inno Setup compile exit 0, 최종 38.234초 |
| 설치 | PASS | 격리된 artifacts/install-smoke 경로에 silent 설치 exit 0 |
| 설치 파일 무결성 | PASS | 배포본과 설치된 파일 821개 SHA256 전부 일치; .playwright 포함 |
| 제거 | PASS | uninstaller exit 0, 앱 실행 파일 삭제 확인 |
| 앱 실제 실행·시각 QA | 미검증 | Computer Use에서 앱 사용 미승인 |
| YouTube 실제 API | PASS | 제공된 키로 검색 호출, live-test-results의 최종 2개 통과 결과 |
| Gemini 실제 API | PASS | gemini-3.1-flash-lite 실제 영상 분석·JSON·근거·길이 로컬 검증 통과 |
| Chrome persistent context·한국어 입력 로컬 fixture | PASS | 독립 임시 프로필, headed Chrome, 실제 순차 입력 후 본문 일치 |
| Chrome 로그인·YouTube 댓글 입력·게시·링크 재접속 | 미검증 | 실제 계정/게시 대상 수동 QA 필요 |
| 별도 깨끗한 Windows PC | 미검증 | 현재 머신에서만 빌드·설치 검증 |
| 원본 PDF 대조 | PASS | 제공된 원본 7쪽 텍스트 전체 및 관련 페이지 렌더링 대조 |

## 자동 테스트 범위

* 지원 URL·Shorts·영상 ID, 악성/유사 호스트 URL 거절
* 한국어·40~120자·그래핌 글자 수·홍보 표기·링크/해시태그 검증
* 서울 날짜 경계, 실제 두 스냅샷 증가율, 역행 조회수·동일 시점 처리
* 게시 상태 전이 및 클릭 전/후 취소 경계
* YouTube HTTP 키 헤더·응답 파싱·HTML 디코딩·할당량·인증·댓글 비활성화
* Gemini JSON Schema 요청 본문과 응답 envelope의 모의 HTTP 파싱
* AI 근거 구절의 원문 일치·댓글 포함·비정상 출력 거절
* SQLite 영상 중복 제거·스냅샷·분석·초안·설정 재조회
* 동시 영상 중복 예약, 서로 다른 영상의 동시 일일 상한 경쟁
* 재시작 후 진행 중 보호 유지, 클릭 전 실패 재시도, 미확인 시도 재게시 차단
* 수정 댓글이 근거를 잃은 경우 Application에서 게시 거절

기본 43개 테스트의 외부 호출은 HTTP 모의입니다. 이와 별도로 실제 API 2개·Chrome 1개 opt-in 테스트가 통과했습니다. 실제 DB 테스트는 임시 SQLite 파일을 생성하고 정리합니다. LiveTests는 MARKETING_AGENT_LIVE_TESTS=1일 때만 실행됩니다. 로컬 Chrome fixture는 실제 YouTube에 댓글을 보내지 않습니다.

## 재현 파일

* `artifacts/release-build.log`: 최종 restore/build/test/publish/installer 컴파일 로그
* `artifacts/test-results/wlgns_WLGNS_2026-11-10_20_33_12_net10.0.trx`: 최종 테스트 결과
* `artifacts/installer-test.log`: 821개 파일 검증·제거 결과
* `artifacts/install-smoke.log`, `artifacts/uninstall-smoke.log`: Inno 설치/제거 상세
* `scripts/publish.ps1 -Installer`, `scripts/test-installer.ps1`: 검증 재현 스크립트

설치 테스트는 기존 경로를 덮어쓰지 않도록 install-smoke 폴더가 존재하면 중단합니다. 재실행 전 폴더 내용을 직접 검토해야 합니다. 앱 실행을 건너뛰는 silent 설치로 진행했으므로 설치 PASS를 실행 PASS로 해석하지 않습니다.

최종 설치 파일: `MarketingAIAgent-1.0.0-win-x64-Setup.exe`, 72,841,111 bytes.

SHA256: `D57AC3AC78D38158C3E01417562C016F5084554E677E6D2C7B4008290D4C8F6F`.

## 발견·수정 사항

SQLitePCLRaw.lib.e_sqlite3 2.1.11 전이 의존성의 알려진 취약점 경고로 초기 restore가 실패했습니다. SQLitePCLRaw.bundle_e_sqlite3 3.0.5를 직접 참조하여 해결했으며 NuGet 감사를 끄지 않았습니다. WPF 생성 프로퍼티 명명과 xUnit 분석기 오류를 수정한 뒤 최종 빌드가 통과했습니다.

Computer Use 앱 실행 요청은 `Computer Use was not approved to use marketingaiagent`로 거절되었습니다. 다른 실행 경로로 우회하지 않았습니다. 앱 시각 품질과 실제 시작 동작은 검증 완료로 표시하지 않습니다.

## 남은 제한

자막 확보 어댑터, 실제 YouTube 셀렉터/계정/게시 검증, 코드 서명, EF 업그레이드 마이그레이션, 좁은 창 단일 열 재배치, 녹화 시연 영상이 남아 있습니다. 조건 기반 선택자와 보수적인 중복 방지는 구현했지만 실제 사이트 변경에 대한 호환성을 검증한 것은 아닙니다.

