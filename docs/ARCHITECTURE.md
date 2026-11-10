# 아키텍처

| 계층 | 파일 | 책임 |
|---|---|---|
| Domain | Models.cs, Interfaces.cs | 엔티티·서비스 계약·URL/댓글/시간/상태 전이 규칙 |
| Application | AgentWorkflow.cs | 분석→저장, 초안 근거 검증→게시 예약→브라우저→결과 저장 |
| Infrastructure | ApiServices.cs | YouTube·Gemini HTTP, JSON Schema, 응답 파싱, 근거 검증 |
| Infrastructure | Persistence.cs | EF SQLite, WAL, 즉시 트랜잭션, 영상 보호 인덱스, 이력 |
| Infrastructure | BrowserService.cs | headed Chrome persistent context, 계정 확인, 입력·승인·검증·캡처 |
| Infrastructure | Security.cs | AppData 경로, DPAPI 보호 저장 |
| App | MainViewModel.cs | 바인딩·취소·상태·작업 명령 조정 |
| App | MainWindow.xaml, Controls/VideoCard.xaml, Styles/Dark.xaml | 3열 대시보드·내비게이션·설정·활동 기록 |

의존성: App → Application → Domain. App은 composition root에서 Infrastructure 구현을 DI로 연결합니다. Infrastructure는 Domain 계약을 구현하고 Application을 참조합니다. 외부 호출은 HttpClientFactory의 typed client로 주입됩니다.

## 게시 무결성

`ReserveAsync`는 BEGIN IMMEDIATE 아래 일일 시도 횟수·영상 보호·성공 댓글 중복을 검사하고 AwaitingApproval 기록과 GuardVideoId를 원자적으로 생성합니다. GuardVideoId 유일 인덱스는 영상당 진행/성공/미확인 예약 하나만 허용하며 Failed로 종료된 클릭 전 시도에서만 null로 해제합니다.

AwaitingApproval → OpeningBrowser → Typing → Posting → Verifying → Success.

클릭 전 오류: Failed. Posting 이후 오류: VerificationUnknown. 강제 종료: 기존 상태와 GuardVideoId 보존. 브라우저 클릭은 외부 부작용이므로 DB와 분산 원자성을 보장할 수 없습니다. 대신 모호한 시도를 차단하는 보수적 at-most-once 정책을 사용합니다. Success에는 본문·실제 채널 핸들·영상 ID·고유 댓글 링크 확인이 모두 필요합니다.

## 근거와 비밀

입력 메타데이터와 댓글은 신뢰하지 않는 데이터로 프롬프트에 전달합니다. 자막은 사용하지 않으며 sourceType=metadata를 강제합니다. 모델이 반환한 evidence는 제목/설명의 연속 구절과 일치해야 합니다. 댓글에도 하나 이상 포함해야 합니다. 수정 후 Application에서 재검사합니다. 규칙 검증은 의미적 정확성을 완전히 증명하지 않으므로 사용자가 승인합니다.

API 키는 Domain 엔티티·SQLite·일반 설정에 들어가지 않습니다. DPAPI 보호 파일에서 요청 직전에 읽고 헤더로만 전달합니다. HttpClient 자동 로깅은 제거하고 예외 타입과 안전한 오류 메시지만 저장합니다.

## 비동기·생명주기

WPF 바인딩 컬렉션은 UI 동기화 컨텍스트에서 갱신합니다. API/EF/Playwright는 await하고 최종 승인 UI는 Dispatcher에 전달합니다. 선택 복원에는 버전 토큰을 써서 늦게 도착한 저장 분석이 새 분석을 덮지 못하게 합니다. 프로필 사용은 SemaphoreSlim으로 한 프로세스 내 직렬화하고 다른 프로세스와의 Chrome 락 충돌은 실패로 중단합니다.
