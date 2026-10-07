# 상점창 설정

상점 상호작용은 `StoreInteractionTrigger → Store → StageManager.StoreInteractionRequested → WindowInputRouter → StoreWindowController` 순서로 전달된다. `StoreWindowController`는 기존 `WindowControllerBase`를 사용하여 패널 표시, 플레이어 게임 입력 차단, 일시정지와 복구를 처리한다.

## UI 계층

기존 Canvas에 상점창을 추가한다. 씬과 프리팹의 UI 구성 및 Inspector 참조 연결은 직접 설정한다.

```text
Canvas (기존 WindowInputRouter)
└─ StoreWindow (RectTransform + StoreWindowController, 활성)
   └─ StoreWindowPanel (Image)
      ├─ TitleText (TextMeshProUGUI)
      └─ CloseButton (Button)
         └─ Text (TextMeshProUGUI)
```

`StoreWindow`와 부모는 활성 상태로 유지한다. 코드가 `StoreWindowPanel`만 열고 닫으며, Awake에서 패널을 닫힌 상태로 초기화한다. `StoreWindowController`를 닫히는 패널 자체에 붙이지 않는다.

기존 CanvasScaler 설정을 유지하고, 패널은 화면 중앙에 충분한 크기로 배치한다. 패널 Image의 Raycast Target을 켜서 뒤의 UI에 포인터 입력이 전달되지 않게 한다. CloseButton은 패널 안에 배치하고, 글자에는 기존 한글 TMP 폰트를 지정한다. 장식용 텍스트의 Raycast Target은 끈다.

## Inspector 연결

| 컴포넌트 | 필드 | 연결 대상 |
|---|---|---|
| WindowInputRouter | Stage Manager | 상점을 등록하고 관리하는 StageManager |
| WindowInputRouter | Store Window | StoreWindow의 StoreWindowController |
| WindowInputRouter | Cancel Action | 기존 InputSystem_Actions의 UI/Cancel |
| StoreWindowController | Window Panel | StoreWindowPanel |
| StoreWindowController | Navigate Action | 기존 InputSystem_Actions의 UI/Navigate |
| StoreWindowController | Close Button | CloseButton |

CloseButton의 클릭 이벤트는 코드가 등록하므로 Inspector의 On Click에 닫기 함수를 중복 등록하지 않는다. 닫기 버튼을 연결하지 않아도 `UI.Cancel`로 닫을 수 있다.

EventSystem과 Canvas의 GraphicRaycaster를 유지한다. `UI.Cancel`은 WindowInputRouter가 처리하므로 기존 InputSystemUIInputModule의 Cancel 액션을 비워 두고 별도 Cancel 구독을 추가하지 않는다.

상점의 StoreInteractionTrigger 참조 및 StageManager 등록은 기존 구조를 사용한다. 생성된 상점방의 Store는 Room을 통해 자동 등록되며, 씬에 직접 배치한 Store는 StageManager의 Stores 목록에 등록한다.

## 표시 동작

- 상점과 같은 플레이어의 유효한 상호작용 요청을 LateUpdate에서 처리해 창을 연다. 열린 상점은 `StoreWindowController.CurrentStore`로 확인할 수 있다.
- 창이 열린 동안 이동·시점·전투·상호작용 게임 입력을 차단하고 Time.timeScale을 0으로 설정한다. UI 입력은 계속 사용할 수 있다.
- 닫기 버튼 또는 UI.Cancel로 닫으면 현재 상점 참조를 지우고 게임 입력 및 창을 열기 전 Time.timeScale을 복구한다.
- 상점창이 열린 동안 플레이어 창과 옵션 창 단축키를 무시한다. 플레이어 창·옵션 창·스킬 확인창이 이미 열려 있으면 상점창을 겹쳐 열지 않는다.
- 같은 프레임에 상점 이용과 UI.Cancel이 들어오면 Cancel을 우선한다. 일반 창 단축키와 상점 이용이 동시에 들어오면 상점 이용을 우선한다.
- StoreWindowController 또는 WindowInputRouter가 비활성화되면 열린 상점창을 닫고 입력 제한을 정리한다.

## Play Mode 확인

1. 상점 범위에서 상호작용하여 패널이 열리고 게임 입력과 게임 시간이 멈추는지 확인한다.
2. UI.Navigate로 닫기 버튼을 선택하고 UI.Submit 또는 마우스로 눌러 닫는지 확인한다.
3. 상점창이 열린 상태에서 UI.Cancel을 입력해 닫히는지 확인한다.
4. 닫힌 뒤 이동과 상호작용이 다시 가능하고 같은 상점을 재이용할 수 있는지 확인한다.
5. 상점창이 열린 동안 플레이어 창·옵션 창 단축키로 다른 창이 열리지 않는지 확인한다.
6. 창을 연 상태에서 StoreWindowController 또는 WindowInputRouter를 비활성화해 일시정지와 입력 차단이 해제되는지 확인한다.

CLI 소스 컴파일과 스텁 검사는 실제 Canvas/EventSystem 표시, Inspector 연결, Input System의 키·게임패드 입력 및 Play Mode 동작을 검증하지 않는다.
