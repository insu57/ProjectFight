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

| 슬롯 위치와 입력 | 이동 대상 |
|---|---|
| 최상단 행에서 위 | Player Window Inventory Tab Button |
| 각 행의 최우단에서 오른쪽 | 해당 그리드의 인벤토리 타입 탭 Button |
| 내부에서 상하좌우 | 같은 행 또는 열의 인접 슬롯 |
| 최좌단에서 왼쪽 / 아래쪽 슬롯이 없는 곳에서 아래 | 현재 슬롯 유지 |

마지막 행에 일부 슬롯만 있으면 그 행의 마지막 실제 슬롯을 오른쪽 끝으로 처리한다. 이동 대상이 연결되지 않았으면 해당 경계에서는 현재 슬롯을 유지한다.

인벤토리 타입 탭에서 왼쪽을 누르면 현재 표시 중인 그리드의 마지막 선택 슬롯으로 복귀한다. 선택 이력이 없으면 첫 번째 사용 가능한 슬롯으로 이동한다. 그리드가 비어 있으면 탭 포커스를 유지한다. 포커스만 다른 타입 탭으로 옮기고 아직 탭을 선택하지 않은 경우에도 현재 표시 중인 그리드로 복귀한다.

슬롯 선택·목록 교체·타입 전환·풀 반환 시 복귀 대상을 갱신한다. 타입 탭은 왼쪽 이동을 지정하기 위해 실행 중 Explicit으로 전환하며, 나머지 방향은 기존 Navigation 설정 또는 자동 탐색 결과를 보존한다. 인벤토리를 닫거나 InventoryView가 비활성화되면 타입 탭의 원래 Navigation 설정을 복원한다. 추가 Inspector 연결은 필요하지 않다.

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
