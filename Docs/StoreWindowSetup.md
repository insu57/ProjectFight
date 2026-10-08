# 상점창 설정

상점 상호작용은 `StoreInteractionTrigger → Store → StageManager.StoreInteractionRequested → WindowInputRouter → StoreWindowController` 순서로 전달된다. `StoreWindowController`는 기존 `WindowControllerBase`를 사용하여 패널 표시, 플레이어 게임 입력 차단, 일시정지와 복구를 처리한다.

## UI 계층

기존 Canvas에 상점창을 추가한다. 씬과 프리팹의 UI 구성 및 Inspector 참조 연결은 직접 설정한다.

```text
Canvas (기존 WindowInputRouter)
└─ StoreWindow (RectTransform + StoreWindowController, 활성)
   └─ StoreWindowPanel (Image)
      ├─ TitleText (TextMeshProUGUI)
      ├─ StoreItemGrid (GridLayoutGroup)
      │  ├─ StoreItemView 0 (Button + StoreItemView)
      │  ├─ StoreItemView 1 (Button + StoreItemView)
      │  ├─ StoreItemView 2 (Button + StoreItemView)
      │  └─ StoreItemView 3 (Button + StoreItemView)
      ├─ RerollBtn (Button)
      │  └─ RerollPriceTxt (TextMeshProUGUI)
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
| StoreWindowController | Reroll Btn | RerollBtn |
| StoreWindowController | Reroll Price Txt | RerollPriceTxt의 TMP_Text |
| StoreWindowController | Item Views | 사전 배치한 StoreItemView 4개를 표시 순서대로 연결 |
| StoreItemView | Button | 상품 루트의 Button |
| StoreItemView | Icon | Icon의 Image |
| StoreItemView | Rarity Outline | Outline의 Image |
| StoreItemView | Name Text | NameTxt의 TMP_Text |
| StoreItemView | Type Text | TypeTxt의 TMP_Text |
| StoreItemView | Rarity Text | RarityTxt의 TMP_Text |
| StoreItemView | Description Text | DescriptionTxt의 TMP_Text |

기존 `Assets/Prefabs/UI/StoreItemView.prefab`의 루트에 `StoreItemView` 컴포넌트를 붙이고 표시 필드를 연결한다. `StoreItemGrid`에 상품 뷰를 총 4개 사전 배치한 뒤 컨트롤러의 `Item Views` 배열에 연결한다. 창을 열고 닫을 때 상품 뷰를 생성하거나 반환하지 않으며, 닫을 때 데이터와 표시만 비운다.

CloseButton의 클릭 이벤트는 코드가 등록하므로 Inspector의 On Click에 닫기 함수를 중복 등록하지 않는다. 닫기 버튼을 연결하지 않아도 `UI.Cancel`로 닫을 수 있다.

RerollBtn도 코드가 클릭 이벤트를 등록하므로 Inspector에 리롤 함수를 중복 등록하지 않는다. `StageDefinition`의 `Base Reroll Price`와 `Reroll Price Increase`에 기본 비용과 리롤 성공마다 증가할 금액을 설정한다. 초기값은 모두 0이며, 기본 비용이 0이면 최초 리롤은 무료이고 증가 비용이 0이면 가격이 유지된다.

EventSystem과 Canvas의 GraphicRaycaster를 유지하고 InputSystemUIInputModule에 기존 `UI/Navigate`와 `UI/Submit`을 연결한다. `UI.Cancel`은 WindowInputRouter가 처리하므로 기존 InputSystemUIInputModule의 Cancel 액션을 비워 두고 별도 Cancel 구독을 추가하지 않는다.

상점의 StoreInteractionTrigger 참조 및 StageManager 등록은 기존 구조를 사용한다. 생성된 상점방의 Store는 Room을 통해 자동 등록되며, 씬에 직접 배치한 Store는 StageManager의 Stores 목록에 등록한다.

## 표시 동작

- StageManager는 `Resources/ItemDefinition`의 데이터를 한 번 읽고 상점 등록 시 상품 4개를 추첨한다. 각 칸은 중복을 허용해 독립적으로 추첨하며, 추첨한 상품은 개별 Store에 보관한다.
- StageDefinition의 `Store Item Type Weights`로 종류를 먼저 정하고, 해당 종류 안에서 `Store Rarity Weights`로 희귀도를 정한 다음 해당 묶음의 아이템을 균등 선택한다. 후보가 없거나 가중치가 0인 종류·희귀도는 제외한다. 선택 가능한 조합이 없으면 경고를 남기고 4개 뷰를 빈 상태로 표시한다.
- 같은 상점을 다시 열거나 같은 그래프에서 매니저를 재활성화하면 상품 목록을 유지한다. 새 그래프가 생성되면 사전 지정 상점의 상품도 다시 추첨한다. StageDefinition이 없는 테스트 씬에서는 유효한 전체 아이템을 균등 선택한다.
- 사전 배치한 상품 뷰의 아이콘·이름·종류·희귀도·설명을 갱신하고 테두리에 희귀도 색상을 적용한다. 아이콘이 없는 상품은 아이콘 표시만 끈다.
- 리롤 설정은 StageManager가 각 Store에 주입한다. 리롤 버튼은 Store를 통해 StageManager에 요청하며, 플레이어 Money가 현재 `RerollPrice` 이상이면 기존 가중치로 상품 4개를 재추첨한다. 같은 금액도 지불할 수 있다.
- 재추첨에 성공하면 PlayerManager의 `TrySpendMoney`로 비용을 차감하고 해당 Store의 다음 리롤 가격에 증가 비용을 더한다. 잔액 부족이나 추첨 실패 시 기존 상품·잔액·가격은 유지한다. 가격 누적은 int 상한까지 허용한다.
- `RerollPriceTxt`는 비용이 부족하면 빨강, 지불 가능하면 검정으로 표시한다. 색상은 `ItemDisplayUtility.GetPriceColor`에서 가져오며 상점 개방, 리롤 처리, Money 변경 시 갱신한다.
- 상점을 다시 열거나 같은 그래프에서 매니저를 재활성화해도 증가한 리롤 가격은 유지한다. 새 스테이지 그래프가 생성되면 기본 비용으로 초기화한다.
- 상점과 같은 플레이어의 유효한 상호작용 요청을 LateUpdate에서 처리해 창을 연다. 열린 상점은 `StoreWindowController.CurrentStore`로 확인할 수 있다.
- 창이 열린 동안 이동·시점·전투·상호작용 게임 입력을 차단하고 Time.timeScale을 0으로 설정한다. UI 입력은 계속 사용할 수 있다.
- 창을 열면 상품 데이터를 채운 뒤 첫 사용 가능한 상품에 포커스를 지정한다. 상품이 없으면 리롤, 닫기 버튼 순서로 선택하며, 선택이 사라진 상태에서 `UI.Navigate`를 입력해도 같은 순서로 복구한다.
- 상품·리롤·닫기 버튼의 Navigation은 창을 열 때 `Automatic`으로 설정한다. Selected 색상은 각 버튼의 기존 Highlighted 색상에 맞춰 키보드·게임패드 선택을 표시하고, 리롤로 상품을 갱신할 때는 현재 선택을 유지한다.
- 닫기 버튼 또는 UI.Cancel로 닫으면 현재 상점 참조를 지우고 게임 입력 및 창을 열기 전 Time.timeScale을 복구한다.
- 상점창이 열린 동안 플레이어 창과 옵션 창 단축키를 무시한다. 플레이어 창·옵션 창·스킬 확인창이 이미 열려 있으면 상점창을 겹쳐 열지 않는다.
- 같은 프레임에 상점 이용과 UI.Cancel이 들어오면 Cancel을 우선한다. 일반 창 단축키와 상점 이용이 동시에 들어오면 상점 이용을 우선한다.
- StoreWindowController 또는 WindowInputRouter가 비활성화되면 열린 상점창을 닫고 입력 제한을 정리한다.

## Play Mode 확인

1. 상점 범위에서 상호작용하여 패널이 열리고 게임 입력과 게임 시간이 멈추는지 확인한다.
2. 창을 처음 열거나 다시 열 때 첫 상품이 Hover 색상으로 선택되는지 확인한다. 키보드·게임패드의 UI.Navigate로 상품·리롤·닫기 버튼 사이를 이동하고, UI.Submit으로 리롤과 닫기가 실행되는지 확인한다. 마우스 클릭도 함께 확인한다.
3. 상점창이 열린 상태에서 UI.Cancel을 입력해 닫히는지 확인한다.
4. 닫힌 뒤 이동과 상호작용이 다시 가능하고 같은 상점을 재이용할 수 있는지 확인한다.
5. 상점창이 열린 동안 플레이어 창·옵션 창 단축키로 다른 창이 열리지 않는지 확인한다.
6. 창을 연 상태에서 StoreWindowController 또는 WindowInputRouter를 비활성화해 일시정지와 입력 차단이 해제되는지 확인한다.
7. 사전 배치한 4개 상품 뷰에 실제 데이터가 표시되고 같은 상점을 다시 열어도 상품 목록과 UI 오브젝트가 유지되는지 확인한다.
8. 종류·희귀도 가중치를 각각 하나만 양수로 설정해 해당 조합의 상품만 표시되는지 확인한다. 모든 가중치가 0이면 경고와 빈 표시를 확인한다.
9. Money가 비용보다 적을 때 빨강 표시와 리롤 실패를, 같거나 많을 때 검정 표시와 비용 차감·상품 재추첨·다음 비용 증가를 확인한다.
10. 상점을 닫고 다시 열어 상품과 누적 비용이 유지되는지 확인한다. 열린 상태에서 다른 경로로 Money를 변경해 가격 색상이 갱신되는지 확인한다.
11. 비용은 충분하지만 추첨 가능한 후보가 없는 경우 기존 상품·Money·비용이 그대로 유지되는지 확인한다.
12. 상품이 없을 때 리롤 버튼이 선택되는지, EventSystem의 선택을 비운 뒤 UI.Navigate를 입력하면 첫 사용 가능한 버튼으로 복구되는지 확인한다. 리롤 후에는 기존 선택이 유지되는지 확인한다.

CLI 소스 컴파일과 스텁 검사는 실제 Canvas/EventSystem 표시, Inspector 연결, Input System의 키·게임패드 입력 및 Play Mode 동작을 검증하지 않는다.
