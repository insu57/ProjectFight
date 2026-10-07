# 아이템 획득 알림 HUD 설정

`PlayerManager.TryAddItem`이 아이템을 보유 목록에 추가하면 `ItemAcquired(ItemDefinition)` 알림을 발생시킨다. `ItemAcquisitionHUD`는 이 알림으로 아이콘, 이름, 등급, 아이템 타입을 표시한다. `RewardChest`의 기존 획득 호출을 그대로 사용하므로 상자에 UI 참조를 연결하지 않는다.

## UI 계층

인벤토리를 열지 않아도 보여야 하므로 기존 `Canvas/PlayerHUD` 아래에 만든다. `PlayerWindow` 아래에는 배치하지 않는다. 씬과 프리팹의 UI 구성 및 Inspector 참조 연결은 직접 설정한다.

```text
Canvas
└─ PlayerHUD
   └─ ItemAcquisitionHUD (RectTransform + ItemAcquisitionHUD)
      └─ NotificationPanel (CanvasGroup + Image)
         ├─ Icon (Image)
         ├─ NameText (TextMeshProUGUI)
         ├─ RarityText (TextMeshProUGUI)
         └─ ItemTypeText (TextMeshProUGUI)
```

`ItemAcquisitionHUD`, `NotificationPanel`과 부모 오브젝트는 활성 상태로 둔다. 알림이 없으면 코드가 CanvasGroup의 Alpha를 0으로 만들어 숨긴다. GameObject를 비활성화하면 획득 이벤트 구독도 해제된다.

## 배치 예시

기존 Canvas의 1920 × 1080 기준 설정을 사용한다. 아래는 화면 오른쪽 중앙에 배치하는 예시이며 위치와 크기는 원하는 HUD 구성에 맞춰 조절한다.

- `ItemAcquisitionHUD`: Anchor Min/Max `(1, 0.5)`, Pivot `(1, 0.5)`, Anchored Position `(-24, 0)`, Size Delta `(360, 112)`.
- `NotificationPanel`: 부모에 맞춰 Stretch하고 네 방향 Offset을 0으로 설정한다. 배경색은 텍스트와 충분히 구분되도록 정한다.
- `Icon`: 왼쪽에 `64 × 64` 크기로 배치하고 Preserve Aspect를 켠다.
- `NameText`: 아이콘 오른쪽 상단에 배치한다.
- `RarityText`, `ItemTypeText`: 이름 아래에 나란히 배치한다. 예: `희귀`, `장비`.

텍스트에는 기존 UI에서 사용하는 한글 지원 TMP 폰트 에셋을 지정한다. 배경·아이콘·텍스트의 Raycast Target은 모두 끈다. 알림은 조작용 UI가 아니며 코드도 CanvasGroup의 Interactable과 Blocks Raycasts를 끈다.

## Inspector 연결

| 필드 | 연결 대상/설정 |
|---|---|
| Notification Panel | NotificationPanel의 CanvasGroup |
| Display Duration | 아이템 한 개의 표시 시간. 기본 3초 |
| Icon | Icon의 Image |
| Name Text | NameText의 TMP 텍스트 |
| Rarity Text | RarityText의 TMP 텍스트 |
| Item Type Text | ItemTypeText의 TMP 텍스트 |

기존 HUD와 동일하게 Awake에서 활성 PlayerManager를 한 번 찾는다. PlayerManager를 별도로 Inspector에 연결할 필요는 없다.

## 표시 동작

- 실제 획득 성공 후 즉시 첫 아이템을 표시한다. 실패한 아이템 추가에는 알림이 발생하지 않는다.
- 이름이 비어 있으면 ItemDefinition SO 이름을 사용한다. 아이콘이 없으면 아이콘 Image만 숨긴다.
- 등급은 `일반 / 고급 / 희귀 / 영웅 / 전설`, 타입은 `장비 / 소모품 / 유물`로 표시한다.
- 표시 시간 동안 다른 아이템을 획득하면 큐에 넣고 획득 순서대로 표시한다. 같은 SO를 연속 획득해도 각각 표시한다.
- 다음 아이템이 없으면 패널을 숨기고 아이콘 Sprite와 텍스트 내용을 초기화한다.
- 표시 시간은 `Time.unscaledDeltaTime`으로 계산하므로 게임이 일시정지되어도 흐른다.
- HUD 비활성화 시 구독, 현재 알림, 대기 큐를 정리한다. 다시 활성화하면 이후 획득부터 수신한다.

## Play Mode 확인

1. Inspector의 모든 표시 참조를 연결하고 HUD 및 부모가 활성 상태인지 확인한다.
2. 인벤토리를 닫은 상태에서 RewardChest를 열어 아이콘·이름·등급·타입이 표시되는지 확인한다.
3. 같은 상자에 재상호작용해도 획득 알림이 반복되지 않는지 확인한다.
4. 서로 다른 상자를 빠르게 열었을 때 알림이 순서대로 각각 설정 시간 동안 표시되는지 확인한다.
5. 이름이나 아이콘이 없는 SO의 대체 표시, 큐 종료 후 숨김, HUD 재활성화 후 구독을 확인한다.
6. 알림 표시 중 플레이어 창을 열어도 표시 시간이 흐르고, 알림이 포인터 입력을 막지 않는지 확인한다.

CLI 소스 컴파일과 스텁 검사는 실제 Canvas/TMP 렌더링, Inspector 연결, 폰트 표시와 Play Mode 동작을 검증하지 않는다.
