# 인벤토리 그리드와 공통 아이템 설명 UI

`RewardChest`를 열면 배정된 아이템 한 개를 `PlayerManager`의 해당 타입 보유 목록에 추가한다. 장비·소모품·유물은 각각 별도 목록으로 관리하고, `InventoryView`는 `GetInventoryItems(ItemType)`으로 조회한 목록을 해당 타입 그리드에 전달한다. 같은 장비·유물을 여러 번 획득하면 각각 별도의 칸으로 표시한다.

`Preview Items`는 `InventoryView` 없이 그리드만 확인할 때 사용하는 UI 확인용 목록이다. `InventoryView`가 연결된 실제 인벤토리는 보유 아이템이 없어도 빈 목록을 전달하므로 확인용 아이템을 보유한 것처럼 표시하지 않는다.

## 계층

기존 타입별 패널과 탭 버튼 연결을 유지한다. 아래 `ItemScrollView` 구조를 장비·소모품·유물 패널에 각각 만든다.

```text
InventoryContent (InventoryTabController + InventoryView)
├─ Inventory TabBtns
├─ Cotents
│  ├─ Equipment_Inven (InventoryGridView: Equipment)
│  │  └─ ItemScrollView (ScrollRect)
│  │     └─ Viewport (Image + Mask)
│  │        └─ Content (GridLayoutGroup + ContentSizeFitter)
│  ├─ Consumable_Inven (InventoryGridView: Consumable)
│  │  └─ ItemScrollView / Viewport / Content
│  └─ Relic_Inven (InventoryGridView: Relic)
│     └─ ItemScrollView / Viewport / Content
└─ ItemDetailPanel (ItemDescriptionView)
   ├─ Icon (Image)
   ├─ NameText (TMP)
   ├─ RarityText (TMP)
   └─ DescriptionText (TMP)
```

공통 설명 패널은 `Cotents`와 형제 관계로 둔다. `Cotents`의 하단 여백을 확보하여 그리드와 설명이 겹치지 않도록 한다. 설명 패널은 하단에 가로로 늘어나게 배치하고, 높이는 예를 들어 180으로 설정한다.

## 그리드와 ScrollView

- `ItemScrollView`: 타입별 패널에 맞춰 늘어나게 배치한다. ScrollRect의 Horizontal은 끄고 Vertical은 켠다. Content와 Viewport 참조를 각각 지정한다.
- `Viewport`: ScrollView에 맞춰 늘어나게 배치한다. Image와 Mask를 추가하고 Mask의 Show Mask Graphic은 끈다.
- `Content`: Anchor Min `(0, 1)`, Anchor Max `(1, 1)`, Pivot `(0.5, 1)`. 상단에 정렬하고 좌우로 늘어난다.
- `GridLayoutGroup`: Start Corner는 Upper Left, Start Axis는 Horizontal, Constraint는 Fixed Column Count. 열 수는 패널 너비에 맞춰 정한다. Cell Size는 예를 들어 `(96, 96)`, Spacing은 `(8, 8)`로 설정한다.
- `ContentSizeFitter`: Horizontal Fit은 Unconstrained, Vertical Fit은 Preferred Size. Viewport에는 Content의 높이를 제어하는 LayoutGroup을 추가하지 않는다.

Content의 자식 아이템 슬롯은 공통 PoolingManager에서 대여한다. 템플릿 슬롯은 별도 프리팹으로 저장하여 Slot Prefab에 지정한다.

## 아이템 슬롯 프리팹

```text
InventoryItemSlot (Image + Button + InventoryItemSlotView)
├─ Icon (Image)
└─ SelectedVisual (Image, 기본 비활성)
```

루트 Image를 Button의 Target Graphic으로 지정한다. InventoryItemSlotView에는 Button, Icon, Selected Visual을 연결한다. 슬롯의 이름 표시는 현재 주석 처리된 상태이며, 이름은 공통 설명 패널에서 표시한다.

루트 Image의 Raycast Target은 켠다. 자식 Icon, SelectedVisual의 Raycast Target은 끈다. Button의 Navigation은 코드가 대여 후 Explicit으로 구성하므로 방향별 대상을 프리팹에 지정하지 않는다. 각 슬롯의 크기는 GridLayoutGroup이 정한다.

## 사전 풀링

씬의 항상 활성화된 PoolingManager에서 `Initial Pools`에 항목을 추가한다.

- Prefab: 각 그리드의 Slot Prefab과 같은 InventoryItemSlotView 프리팹 컴포넌트
- Count: 한 번에 표시할 슬롯 수에 맞춘 초기 수량. 예를 들어 가장 큰 탭이 30개라면 30으로 설정한다.

PoolingManager는 Awake 단계에 비활성 슬롯을 미리 생성한다. 슬롯은 최초 대여 시 실제 Content 아래에서 활성화된다. 장비·소모품·유물 그리드에 같은 프리팹을 지정하면 동일한 풀을 공유한다. 설정한 수량이 부족하면 추가 생성한다.

InventoryGridView의 최초 대여는 모든 Awake가 끝난 뒤 Start에서 진행한다. 이후에는 활성화 시 대여하고 비활성화 시 반환한다. 반환 시 아이템 참조, 선택 표시, EventSystem 선택, Navigation 참조와 이전 그리드의 이벤트 구독을 초기화한다.

## Inspector 연결

| 컴포넌트 | 필드 | 연결 대상 |
|---|---|---|
| InventoryView | Tab Controller | 기존 InventoryTabController |
| InventoryView | Grids | 장비·소모품·유물 InventoryGridView 3개 |
| InventoryView | Description View | 공통 ItemDescriptionView |
| InventoryView | Player Window Inventory Tab Button | 플레이어 윈도우 상단의 인벤토리 탭 Button 컴포넌트 |
| 각 InventoryGridView | Item Type | 해당 패널의 ItemType |
| 각 InventoryGridView | Content | 해당 ScrollView의 Content RectTransform |
| 각 InventoryGridView | Slot Prefab | 공통 InventoryItemSlot 프리팹 |
| 각 InventoryGridView | Preview Items | 그리드 단독 확인용 ItemDefinition SO 목록 |
| ItemDescriptionView | Icon / Name Text / Rarity Text / Description Text | 공통 설명 패널의 Image와 TMP 컴포넌트 |

각 그리드는 자신의 타입에 해당하는 보유 목록을 전달받는다. null 항목과 다른 타입의 아이템은 기존 안전 검사로 건너뛴다. `InventoryView`는 기존 플레이어 창의 다른 View와 동일하게 Awake에서 PlayerManager를 한 번 찾으므로 추가 Inspector 연결은 필요하지 않다.

버튼 클릭 이벤트는 코드가 구독하므로 Inspector On Click에 별도로 등록하지 않는다. 기존 Canvas의 GraphicRaycaster와 EventSystem을 사용한다.

## 키보드·패드 슬롯 탐색

InventoryView가 위쪽 이동 대상과 ItemType에 맞는 오른쪽 타입 탭을 각 그리드에 전달한다. Grids 배열의 순서는 이동 경로에 영향을 주지 않는다.

상단 플레이어 윈도우의 인벤토리 버튼에서 아래를 누르면 Equipment 타입 탭에 포커스가 이동한다. InventoryView가 활성화된 동안 기존 Player Window Inventory Tab Button 참조를 통해 아래 방향 대상만 전달하며, 다른 방향은 해당 버튼의 기존 Navigation을 사용한다. 창을 닫거나 다른 플레이어 창 탭으로 전환하면 이 지정은 해제된다. Equipment로 포커스를 이동하는 것만으로 표시 중인 아이템 타입을 바꾸지는 않으며, 탭 확정 입력은 기존 동작을 유지한다. 추가 Inspector 연결은 필요하지 않다.

| 슬롯 위치와 입력 | 이동 대상 |
|---|---|
| 최상단 행에서 위 | Player Window Inventory Tab Button |
| 각 행의 최우단에서 오른쪽 | 해당 그리드의 인벤토리 타입 탭 Button |
| 내부에서 상하좌우 | 같은 행 또는 열의 인접 슬롯 |
| 최좌단에서 왼쪽 / 아래쪽 슬롯이 없는 곳에서 아래 | 현재 슬롯 유지 |

마지막 행에 일부 슬롯만 있으면 그 행의 마지막 실제 슬롯을 오른쪽 끝으로 처리한다. 이동 대상이 연결되지 않았으면 해당 경계에서는 현재 슬롯을 유지한다.

인벤토리 타입 탭에서 왼쪽을 누르면 현재 표시 중인 그리드의 마지막 선택 슬롯으로 복귀한다. 선택 이력이 없으면 첫 번째 사용 가능한 슬롯으로 이동한다. 그리드가 비어 있으면 탭 포커스를 유지한다. 포커스만 다른 타입 탭으로 옮기고 아직 탭을 선택하지 않은 경우에도 현재 표시 중인 그리드로 복귀한다.

슬롯 선택·목록 교체·타입 전환·풀 반환 시 복귀 대상을 갱신한다. 타입 탭은 실행 중 Explicit으로 전환하며 위·아래의 내부 이동은 Equipment ↔ Consumable ↔ Relic으로 직접 연결한다. 이 연결은 ItemType으로 찾으므로 Inspector 배열 순서나 초기 레이아웃 배치 시점에 영향을 받지 않으며, 빈 그리드에서도 타입 탭 사이를 이동할 수 있다. 왼쪽은 현재 그리드의 복귀 대상을 사용하고, Equipment의 위·Relic의 아래와 오른쪽은 기존 Navigation 설정 또는 자동 탐색 결과를 보존한다. 인벤토리를 닫거나 InventoryView가 비활성화되면 타입 탭의 원래 Navigation 설정을 복원한다. 추가 Inspector 연결은 필요하지 않다.

슬롯 목록을 구성할 때와 Content 크기가 바뀔 때 레이아웃을 확정하고 실제 슬롯 위치로 연결을 계산한다. 슬롯마다 매 프레임 이웃을 검색하거나 새로운 목록을 할당하지 않는다.

## 표시 동작

- 슬롯을 가리키거나 클릭하거나 키보드·패드로 선택하면 선택 표시와 공통 설명이 갱신된다.
- 설명에는 아이콘, 이름, 희귀도, ItemDescription을 표시한다. 이름이 비어 있으면 SO 이름을 사용한다.
- 아이콘이 없으면 이미지 표시를 끈다.
- 탭 전환 또는 창 닫기·재개방 시 아이템 선택과 설명을 초기화한다. 마지막으로 선택한 아이템 타입은 유지한다.
- 슬롯은 미리 준비한 공통 풀에서 대여하고, 목록 교체·탭 전환·창 닫기 시 반환한다.
- 닫힌 그리드에 SetItems를 호출하면 마지막 목록을 저장하고 다시 열 때 표시한다. SetItems(null)은 다음 활성화에서도 빈 목록을 유지한다.
- `InventoryView`는 활성화 시 모든 그리드에 타입별 현재 보유 목록을 즉시 전달한다. 활성화된 동안에는 `PlayerManager.InventoryChanged`가 알린 ItemType의 그리드만 갱신하고, 다른 타입의 슬롯·선택·설명은 유지한다. 비활성화 시 구독을 해제한다.

## Play Mode 확인

아이템을 획득하기 전에는 확인용 아이템이 지정되어 있어도 실제 인벤토리가 비어 있는지 확인한다. 장비·유물 보상 상자를 열고 해당 탭에 아이템이 한 개씩 추가되는지, 같은 상자에 다시 상호작용해도 중복 지급되지 않는지 확인한다. 같은 SO를 보상으로 가진 서로 다른 상자를 열면 각각 별도 칸이 생겨야 한다.

창을 닫은 동안 상자를 연 뒤 다시 열어 최신 목록을 확인한다. 각 탭의 목록, 포인터·키보드·패드 선택, 설명 갱신, 탭 전환과 창 재개방 시 초기화, 목록이 길어졌을 때 스크롤도 확인한다. 시작 시 PoolingManager/ PooledObjects/ Inactive 아래 초기 수량이 준비되는지, 탭 전환마다 같은 슬롯이 이동하는지 확인한다. CLI 소스 컴파일과 스텁 검사는 실제 Canvas/TMP 렌더링과 EventSystem 동작을 검증하지 않는다.

장비 탭에서 아이템을 선택한 상태로 `PlayerManager.TryAddItem`을 통해 유물을 추가했을 때 장비 슬롯·선택·설명이 유지되는지 확인한다. 이후 유물 탭을 열면 새 아이템이 표시되어야 한다. 현재 표시 중인 타입에 아이템을 추가하면 해당 그리드만 다시 구성되며 선택과 설명은 초기화된다.

## 소모품 퀵슬롯 등록

플레이어의 PlayerManager와 같은 GameObject에 PlayerQuickslot을 추가한다. Items 배열은 4칸이며 처음에는 비워 둔다. 공개 API의 슬롯 번호는 1~4이고 Inspector 배열의 인덱스는 0~3이다. 등록 상태는 PlayerQuickslot에, 실제 보유 수량은 기존 PlayerManager에 유지한다.

| 조작 | 동작 |
|---|---|
| 소모품 선택 후 숫자키 1~4 | 선택한 소모품을 해당 칸에 등록 |
| 이미 같은 칸에 등록된 소모품에 같은 숫자키 | 해당 소모품 등록 해제 |
| 다른 칸에 등록된 소모품을 새 칸에 등록 | 기존 칸을 비우고 새 칸의 기존 아이템 등록을 교체 |
| 게임패드 Y/△ | 실제 UI 포커스가 있는 소모품의 등록 패널 열기 |
| 패널에서 방향 입력 후 A/× | 1~4번 중 선택한 칸에 등록. 같은 칸을 선택하면 등록 유지 |
| 패널의 등록 해제 버튼 | 선택한 소모품의 기존 등록 해제 |
| 패널의 취소 버튼 또는 B/○/Escape | 패널만 닫고 원래 소모품으로 복귀 |

예를 들어 A가 1번, B가 2번일 때 A를 2번에 등록하면 1번은 비고 2번에는 A가 들어간다. B는 퀵슬롯에서만 해제되며 보유 수량은 바뀌지 않는다.

### 등록 패널 계층

등록 패널을 창 본문의 CanvasGroup 밖에 둔다. 기존 탭·인벤토리 구성은 그대로 유지하면서, 모든 창 탭과 본문을 포함하는 그룹을 배경으로 지정한다.

```text
PlayerWindowPanel
├─ WindowBody (CanvasGroup)
│  ├─ 기존 플레이어 창 탭 버튼
│  └─ 기존 탭 Content (InventoryView 포함)
└─ QuickslotRegistrationController (QuickslotRegistrationView, 기본 활성)
   └─ RegistrationPanel (Image, 기본 비활성)
      ├─ SelectedItemNameText (TMP)
      ├─ Slot1Button (Button, 라벨: 1 ↑)
      ├─ Slot2Button (Button, 라벨: 2 →)
      ├─ Slot3Button (Button, 라벨: 3 ↓)
      ├─ Slot4Button (Button, 라벨: 4 ←)
      ├─ UnregisterButton (Button, 라벨: 등록 해제)
      └─ CancelButton (Button, 라벨: 취소)
```

각 슬롯 버튼에 현재 등록된 아이템의 Icon(Image)과 ItemNameText(TMP)를 선택적으로 추가한다. 번호/방향 라벨은 별도 고정 텍스트로 둔다. 패널 버튼은 배열 순서대로 상하좌우 탐색하며, 미등록 소모품의 등록 해제 버튼은 비활성화하고 탐색에서도 건너뛴다. 버튼을 한 줄 또는 한 열로 배치하면 탐색 순서와 일치한다.

RegistrationPanel의 배경 Image는 창 영역을 덮고 Raycast Target을 켜며, 아이콘과 텍스트의 Raycast Target은 끈다. QuickslotRegistrationView를 비활성 RegistrationPanel 자체에 붙이지 않고 활성 컨트롤러에 붙인다. Background Group 아래에 RegistrationPanel을 넣으면 배경 입력 차단이 팝업에도 적용되므로 잘못된 구성은 패널을 열지 않고 경고한다.

### 추가 Inspector 연결

| 컴포넌트 | 필드 | 연결 대상 |
|---|---|---|
| InventoryView | Quickslot Registration View | 위 QuickslotRegistrationView |
| InventoryView | Register Slot Actions | InputSystem_Actions의 UI/RegisterQuickslot1, 2, 3, 4 순서 |
| InventoryView | Open Registration Action | UI/OpenQuickslotRegistration |
| QuickslotRegistrationView | Panel | RegistrationPanel |
| QuickslotRegistrationView | Background Group | WindowBody의 CanvasGroup |
| QuickslotRegistrationView | Selected Item Name Text | 선택한 소모품 이름을 표시할 TMP, 선택 사항 |
| QuickslotRegistrationView | Slots | Size 4. 각 원소의 Button을 1~4번 버튼에 연결. Icon과 Item Name Text는 선택 사항 |
| QuickslotRegistrationView | Unregister Button / Cancel Button | 등록 해제 / 취소 Button |
| WindowInputRouter | Quickslot Registration | 위 QuickslotRegistrationView |
| InventoryItemSlotView 프리팹 | Quick Slot Visual Parent | 퀵슬롯 표시 전체를 감싸는 부모 GameObject. 등록·해제 시 활성 상태를 변경 |
| InventoryItemSlotView 프리팹 | Quick Slot Visual | 부모 아래의 번호 Image. GameObject와 Image는 활성 상태로 두고 Raycast Target은 끔. 코드에서는 Sprite만 변경 |
| InventoryItemSlotView 프리팹 | Quick Slot 01~04 Sprite | 슬롯 번호 1~4 표시용 Sprite |

등록 입력은 새 UI 액션에 연결되어 있으며 Player 입력 맵과 UI/Navigate 바인딩은 유지한다. 버튼 On Click은 코드가 연결하므로 Inspector에서 중복 등록하지 않는다. Cancel은 기존처럼 WindowInputRouter가 소유한다.

등록·이동·해제는 인벤토리 슬롯을 다시 대여하지 않고 번호 표시만 갱신한다. 창/탭을 다시 열거나 슬롯을 재사용할 때에도 현재 번호를 표시하며, 해제된 번호는 숨긴다. 등록 패널을 닫으면 배경 CanvasGroup의 기존 Interactable/Blocks Raycasts 상태를 복구한다.

### 현재 등록된 퀵슬롯 표시

QuickSlotPanel에 QuickslotPanelView를 추가하고 Slot Bindings의 크기를 4로 지정한다. 각 원소의 Slot Number는 1, 2, 3, 4로 지정하며 Icon은 해당 QuickSlot 01~04의 아이템 표시 Image에 연결한다. 번호/방향 안내용 자식 QuickIcon은 이 배열에 연결하지 않는다.

InventoryView의 Quickslot Panel View에 위 컴포넌트를 연결한다. ItemDescriptionView의 Consumable Info Panel은 기존 QuickSlotPanel을 유지한다. 현재 소모품 탭에서는 아이템을 선택하지 않아도 1~4번 등록 아이템이 표시된다.

EquipmentPanelView와 동일하게 첫 갱신 시 각 Image의 기본 Sprite와 Enabled 상태를 기억한다. 아이콘이 있는 소모품을 등록하면 해당 ItemIcon으로 바꾸고 Image를 켠다. 등록 해제 또는 아이콘이 없는 아이템은 기본 상태로 복원한다. 현재 씬처럼 빈 칸의 Image를 비활성 상태로 두면 미등록 칸에서는 아이템 아이콘만 숨고 번호/방향 표시는 유지된다.

등록·이동·목적지 교체·해제 시 PlayerManager.QuickslotsChanged를 InventoryView가 받아 그리드 번호 표시와 QuickSlotPanel을 함께 갱신한다. 창 재개방과 타입 탭 전환 시에도 최신 등록 상태를 반영한다. 패널 자체는 입력이나 PlayerQuickslot 이벤트를 직접 구독하지 않는다.

### Play Mode 확인 항목

보유한 소모품 두 종류로 숫자 등록·같은 숫자 해제·다른 칸 이동·목적지 교체를 확인한다. 게임패드에서는 십자버튼으로 인벤토리를 탐색한 뒤 Y/△로 등록 패널을 열고, 등록·같은 칸 등록 유지·별도 등록 해제·B/○ 취소를 확인한다. 패널이 열린 동안 배경 슬롯과 창 탭이 입력을 받지 않아야 하며 B/○ 한 번으로 플레이어 창까지 닫히지 않아야 한다.

등록 후 소모품 수량이 바뀌지 않는지, 창 재개방/탭 전환 후 번호가 유지되는지, 슬롯 풀 재사용 시 다른 아이템에 번호가 남지 않는지 확인한다. 소스 컴파일과 스텁 검증은 실제 UI 배치, InputActionReference 연결, 패드 입력, Canvas/TMP 표시 및 Unity Play Mode를 대신하지 않는다.
