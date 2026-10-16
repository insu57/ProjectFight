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
- 설명에는 아이콘, 이름, 희귀도를 표시한다. Description Text에는 ItemDefinition.StatModifiers의 보정을 밝은 노란색(#FFE082)으로 한 줄씩 표시하고, 빈 줄 하나 뒤에 기존 ItemDescription을 `• `로 시작하는 플레이버 텍스트로 붙인다. 플레이버 텍스트는 TMP의 기존 글자색을 사용한다. 효과가 없으면 앞 여백 없이 설명만, 설명이 없으면 빈 줄과 점 없이 효과만 표시한다. 이름이 비어 있으면 SO 이름을 사용한다.
- 스탯 보정은 한국어 이름과 부호로 표시한다. FlatAdd는 원래 수치, PercentAdd는 0.2를 +20%로 표시하며 시간·거리·횟수·초당 회복량에는 해당 단위를 붙인다. 이동 속도의 FlatAdd는 배율 자체의 가산값으로 표시한다. 0 또는 유효하지 않은 보정 항목은 생략한다. 현재 이 설명에 자동 표시하는 효과는 StatModifiers이며, 기존 Description Text 참조를 그대로 사용한다.
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
| B/○/Escape | 패널만 닫고 원래 소모품으로 복귀 |

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
      └─ ControlGuide (고정 조작 안내)
         ├─ ConfirmGuide (Image + TMP, Xbox 기준 A 확인)
         └─ CancelGuide (Image + TMP, Xbox 기준 B 취소)
```

각 슬롯 버튼에 현재 등록된 아이템의 Icon(Image)과 ItemNameText(TMP)를 선택적으로 추가한다. 번호/방향 라벨은 별도 고정 텍스트로 둔다. 패널은 슬롯 1~4와 등록 해제 버튼만 순환 탐색하며, 미등록 소모품의 등록 해제 버튼은 비활성화하고 탐색에서도 건너뛴다. 버튼을 한 줄 또는 한 열로 배치하면 탐색 순서와 일치한다.

씬의 기존 CancelButton은 삭제하고, 패널 하단에 고정된 `A 확인 · B 취소` 안내를 배치한다. 안내용 이미지와 텍스트의 Raycast Target은 끄고 Button 컴포넌트는 추가하지 않는다. A는 현재 선택한 슬롯의 등록 또는 등록 해제를 실행하고, B는 선택 위치에 관계없이 패널만 닫는다. 안내 이미지는 Inspector에서 배치하며 QuickslotRegistrationView에 연결할 필드는 없다.

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
| QuickslotRegistrationView | Unregister Button | 등록 해제 Button |
| WindowInputRouter | Quickslot Registration | 위 QuickslotRegistrationView |
| InventoryItemSlotView 프리팹 | Quick Slot Visual Parent | 퀵슬롯 표시 전체를 감싸는 부모 GameObject. 등록·해제 시 활성 상태를 변경 |
| InventoryItemSlotView 프리팹 | Quick Slot Visual | 부모 아래의 번호 Image. GameObject와 Image는 활성 상태로 두고 Raycast Target은 끔. 코드에서는 Sprite만 변경 |
| InventoryItemSlotView 프리팹 | Quick Slot 01~04 Sprite | 슬롯 번호 1~4 표시용 Sprite |

등록 입력은 새 UI 액션에 연결되어 있으며 Player 입력 맵과 UI/Navigate 바인딩은 유지한다. 버튼 On Click은 코드가 연결하므로 Inspector에서 중복 등록하지 않는다. Cancel은 기존처럼 WindowInputRouter가 소유한다.

등록·이동·해제는 인벤토리 슬롯을 다시 대여하지 않고 번호 표시만 갱신한다. 창/탭을 다시 열거나 슬롯을 재사용할 때에도 현재 번호를 표시하며, 해제된 번호는 숨긴다. 등록 패널을 닫으면 배경 CanvasGroup의 기존 Interactable/Blocks Raycasts 상태를 복구한다.

### 지속 회복의 갱신과 중첩

소모품의 `지속 회복` (`RecoveryOverTimeEffectData`)에서 `RefreshDuration`은 아이템 종류와 효과 항목 번호에 관계없이 같은 회복 대상의 갱신형 효과를 교체한다. 체력과 에너지는 별도로 유지하므로 두 대상의 지속 회복을 동시에 적용할 수 있다.

다시 사용하면 회복량, 고정값/최대치 비율 방식, 회복 간격, 지속시간을 모두 새 아이템 기준으로 설정한다. 종료 시간은 사용 시점 + 새 Duration이며, 첫 회복은 사용 시점 + 새 Interval이다. 이전 효과의 남은 시간이나 다음 회복 시점은 이어받지 않는다. 더 약한 아이템을 사용해도 새 설정으로 교체한다. 같은 아이템에 같은 대상의 갱신형 효과가 여러 개 있으면 목록에서 마지막으로 실행한 항목이 적용된다.

`Independent`는 사용할 때마다 별도 실행 상태로 중첩한다. 갱신형 효과와 함께 적용할 수 있고, 같은 대상의 갱신형 아이템을 사용해도 기존 개별 중첩 효과는 유지된다. 시간 제한 스탯 보정의 `RefreshDuration`은 아래 설명처럼 스탯 종류를 기준으로 보정값과 지속시간을 교체한다.

Play Mode에서는 서로 다른 체력 회복 아이템으로 교체할 때 새 회복량·간격·지속시간이 적용되는지, 고정값과 최대치 비율의 전환, 새 간격 전에는 회복하지 않는지, 에너지 회복과 개별 중첩은 그대로 유지되는지 확인한다.

### 시간 제한 스탯 보정 아이템

소모품 ItemDefinition의 `Use Effect Data`에 `시간 제한 스탯 보정` (`TimedStatModifierEffectData`)을 추가한다. `Duration`은 지속시간(초), `Stat Modifiers`는 적용할 스탯과 계산 방식이다. 예를 들어 Duration 20, AttackPower / PercentAdd / 0.2이면 20초 동안 공격력 계산에 20% 보정을 더한다. FlatAdd는 고정값이고, 음수 보정도 가능하다.

인벤토리와 퀵슬롯에서 사용하면 즉시 적용되며, 장비·유물과 같은 계산식 `(기본값 + 모든 FlatAdd 합계) × (1 + 모든 PercentAdd 합계)`에 합산한다. 공격력·일반/가드 강인도 피해는 실제 타격 시 공격 고유의 기본 피해까지 합산한 뒤 배율을 적용한다. 효과 도중 장비 교체·유물 획득·레벨업을 해도 시간제 보정을 유지하고, 만료하면 그 효과의 보정만 제거한다.

- `RefreshDuration`: ItemDefinition과 효과 항목 번호에 관계없이 같은 ItemStatType의 갱신형 보정을 새 값으로 교체하고, 종료 시간을 사용 시점 + 새 Duration으로 설정한다. 같은 스탯의 FlatAdd·PercentAdd는 새 효과의 보정 묶음으로 함께 교체한다. 새 효과에 두 방식이 모두 있으면 모두 적용하고, FlatAdd만 있으면 기존 PercentAdd도 제거한다.
- `Independent`: 사용할 때마다 별도로 중첩하며, 각 사용 시점부터 Duration 후 개별 해제한다.
- 한 효과에 여러 스탯이 있어도 새 효과와 겹치는 스탯만 교체한다. 다른 스탯과 Independent 효과는 기존 값과 종료 시간을 유지한다. 예를 들어 공격력 +10·방어력 +5가 적용된 상태에서 공격력 +20 아이템을 사용하면 공격력만 새 값과 지속시간으로 바뀐다. 더 약한 보정도 새 값으로 교체하며, 같은 아이템 안의 서로 다른 효과 항목에 같은 스탯이 있으면 마지막으로 실행한 항목이 적용된다.
- 적용 시 보정값을 복사하므로 원본 설정을 바꿔도 진행 중인 보정은 그대로 유지하고, 다시 사용해 갱신할 때 새 값을 반영한다. 값이 0이거나 유효하지 않은 항목은 적용·교체 대상에서 제외한다.

최대 체력·에너지를 늘려도 현재량을 자동 회복하지 않는다. 만료로 최대치가 줄면 초과한 현재량만 제한하며, HUD와 스탯 창은 기존 변경 이벤트로 갱신된다. 함께 회복하는 아이템은 사용 효과 목록에서 최대치 보정 다음에 회복 효과를 배치한다.

지속시간은 Time.deltaTime 기준으로 진행하므로 timeScale이 0이면 멈춘다. 사망 또는 PlayerManager 비활성화 시 보정을 해제한다. 매 프레임 만료한 스탯 보정을 먼저 제거하고 지속 회복을 처리하므로 해당 프레임의 비율 회복은 갱신된 최대치를 사용한다. 마지막 아이템을 소진하거나 퀵슬롯 등록을 해제해도 이미 시작한 효과는 지속시간 동안 유지한다. 적용할 유효한 보정이 없는 시간제 효과만 있는 아이템은 소모하지 않는다.

Play Mode에서는 서로 다른 아이템의 같은 스탯 교체, 일부 스탯만 교체할 때 나머지의 기존 종료 시간 유지, FlatAdd·PercentAdd 묶음 교체, 개별 중첩의 순차 만료, 효과 도중 장비 교체·레벨업, 최대치 감소 시 현재량 제한, HUD 바 길이 복원, 일시정지, 사망·비활성화 해제를 확인한다.

### 게임 중 퀵슬롯 사용

`Player/QuickSlot1~4` 입력으로 해당 칸에 등록한 소모품을 사용한다. 현재 Input Actions 바인딩은 다음과 같다.

| 액션 | 키보드 | 게임패드 |
|---|---|---|
| Player/QuickSlot1 | 1 | 십자버튼 ↑ |
| Player/QuickSlot2 | 2 | 십자버튼 ← |
| Player/QuickSlot3 | 3 | 십자버튼 → |
| Player/QuickSlot4 | 4 | 십자버튼 ↓ |

`PlayerControl`은 performed 시점에 슬롯 번호를 한 번 알리고, `PlayerManager`가 등록된 Definition의 현재 보유 위치를 확인해 기존 `TryUseItem`을 호출한다. 단발성 효과와 지속 회복 모두 같은 사용·수량 차감 규칙을 적용한다. 미등록 슬롯, 보유 수량 0, 사망 등 기존 사용 조건을 만족하지 못하면 아이템을 소모하지 않는다. 마지막 1개를 사용해 수량이 0이 되면 해당 아이템의 퀵슬롯 등록을 자동 해제하고, HUD와 인벤토리 패널의 아이콘을 빈 칸의 기본 상태로 복원한다. 인벤토리 슬롯에서 직접 사용해도 같은 규칙을 적용하며, 1개 이상 남아 있으면 등록을 유지한다. 같은 아이템을 다시 획득한 경우 원하는 퀵슬롯에 다시 등록하여 사용한다.

인벤토리·상점·옵션 등 게임 입력을 차단하는 창을 열면 이 네 사용 액션도 비활성화하고, 닫으면 다시 활성화한다. 열린 인벤토리의 숫자키 등록은 기존 `UI/RegisterQuickslot1~4`로 처리한다.

### 현재 등록된 퀵슬롯 표시

QuickSlotPanel에 QuickslotPanelView를 추가하고 Slot Bindings의 크기를 4로 지정한다. 각 원소의 Slot Number는 1, 2, 3, 4로 지정하며 Icon은 해당 QuickSlot 01~04의 아이템 표시 Image에 연결한다. 번호/방향 안내용 자식 QuickIcon은 이 배열에 연결하지 않는다.

InventoryView의 Quickslot Panel View에 위 컴포넌트를 연결한다. ItemDescriptionView의 Consumable Info Panel은 기존 QuickSlotPanel을 유지한다. 현재 소모품 탭에서는 아이템을 선택하지 않아도 1~4번 등록 아이템이 표시된다.

EquipmentPanelView와 동일하게 첫 갱신 시 각 Image의 기본 Sprite와 Enabled 상태를 기억한다. 아이콘이 있는 소모품을 등록하면 해당 ItemIcon으로 바꾸고 Image를 켠다. 등록 해제 또는 아이콘이 없는 아이템은 기본 상태로 복원한다. 현재 씬처럼 빈 칸의 Image를 비활성 상태로 두면 미등록 칸에서는 아이템 아이콘만 숨고 번호/방향 표시는 유지된다.

등록·이동·목적지 교체·해제 시 PlayerManager.QuickslotsChanged를 InventoryView가 받아 그리드 번호 표시와 QuickSlotPanel을 함께 갱신한다. 창 재개방과 타입 탭 전환 시에도 최신 등록 상태를 반영한다. 패널 자체는 입력이나 PlayerQuickslot 이벤트를 직접 구독하지 않는다.

### PlayerHUD에서 퀵슬롯 표시 재사용

인벤토리의 표시 컴포넌트와 같은 `QuickslotPanelView`를 HUD에 별도 인스턴스로 배치한다. `PlayerWindow` 밖의 기존 `Canvas/PlayerHUD` 아래에 퀵슬롯 패널을 만들고 `QuickslotPanelView`를 추가한다. `Slot Bindings`의 크기는 4로 지정하고 각 원소의 `Slot Number`를 1~4, `Icon`을 해당 HUD 슬롯의 아이템 Image에 연결한다. 빈 칸의 기본 Sprite와 Enabled 상태는 기존 패널과 같은 방식으로 설정한다. 아이콘의 Raycast Target은 끈다.

`PlayerStatusHUD`의 `Quickslot Panel View`에는 HUD에 배치한 인스턴스를 연결한다. `InventoryView`의 참조에는 기존 인벤토리용 인스턴스를 유지한다. 두 패널은 같은 PlayerManager의 등록 상태를 각각 표시하며, HUD 패널은 인벤토리 창을 닫아도 표시된다.

`PlayerStatusHUD`는 활성화 시 현재 등록 상태를 반영하고 모든 Awake가 끝난 Start에서도 최초 상태를 다시 확인한다. 이후에는 `PlayerManager.QuickslotsChanged` 알림으로 등록·이동·교체·해제를 갱신하며, 비활성화 시 구독을 해제한다. HUD를 다시 켜면 꺼져 있는 동안 변경된 상태도 즉시 반영한다. 현재 HUD는 등록 아이콘만 표시한다.

### Play Mode 확인 항목

보유한 소모품 두 종류로 숫자 등록·같은 숫자 해제·다른 칸 이동·목적지 교체를 확인한다. 게임패드에서는 십자버튼으로 인벤토리를 탐색한 뒤 Y/△로 등록 패널을 열고, A/× 등록·같은 칸 등록 유지·별도 등록 해제·B/○ 취소를 확인한다. 탐색은 슬롯 4개와 활성 상태의 등록 해제 버튼만 순환하고, 조작 안내 이미지에는 포커스가 가지 않아야 한다. 패널이 열린 동안 배경 슬롯과 창 탭이 입력을 받지 않아야 하며 B/○ 한 번으로 플레이어 창까지 닫히지 않아야 한다.

등록 후 소모품 수량이 바뀌지 않는지, 창 재개방/탭 전환 후 번호가 유지되는지, 슬롯 풀 재사용 시 다른 아이템에 번호가 남지 않는지 확인한다. 소스 컴파일과 스텁 검증은 실제 UI 배치, InputActionReference 연결, 패드 입력, Canvas/TMP 표시 및 Unity Play Mode를 대신하지 않는다.

HUD용 `Quickslot Panel View`를 연결한 뒤 인벤토리에서 등록·이동·교체·해제할 때 HUD의 아이콘도 함께 바뀌는지 확인한다. 인벤토리를 닫은 뒤에도 HUD가 표시되는지, PlayerHUD를 껐다가 다시 켜면 최신 등록 상태가 반영되는지 확인한다.

게임 중 키보드 1~4와 패드 십자버튼으로 각 칸의 아이템이 한 번씩 사용되는지 확인한다. 빈 칸·수량 0·사망 시에는 소모되지 않아야 하며, 앞 아이템을 모두 사용해 보유 목록의 순서가 바뀌어도 다른 슬롯의 아이템을 올바르게 사용해야 한다. 인벤토리·상점·옵션 창이 열린 동안 같은 입력으로 아이템이 사용되지 않는지, 창을 닫으면 다시 사용할 수 있는지도 확인한다. 마지막 1개를 퀵슬롯 입력 또는 인벤토리 슬롯에서 사용하면 해당 등록과 HUD·인벤토리 아이콘이 함께 비워지는지, 다른 슬롯은 그대로 유지되는지 확인한다. 수량이 남아 있을 때는 등록을 유지하고, 소진 후 재획득만으로 이전 칸에 자동 등록되지 않는지도 확인한다.
