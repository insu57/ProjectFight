# 플레이어 성장 CSV

`Assets/Data/PlayerProgression` 아래의 CSV를 Unity Editor가 임포트하면 같은 위치와 이름의 `PlayerProgressionDefinition` SO 에셋을 생성하거나 갱신합니다.

예: `PlayerProgression.csv` → `PlayerProgression.asset`

생성 SO를 플레이어의 `PlayerStat > Progression Definition`에 연결하면 해당 레벨의 기본 스탯과 요구 경험치를 사용합니다.

## 편집 규칙

- UTF-8 CSV로 저장합니다. UTF-8 BOM, LF/CRLF, 빈 줄, 숫자 셀을 감싼 큰따옴표를 지원합니다.
- 첫 번째 비어 있지 않은 행은 아래 헤더를 순서와 대소문자 그대로 사용합니다.

```csv
Level,TargetExp,SkillPointsOnReach,MaxHealth,AttackPower,DefensePower,MaxEnergy
```

- `Level`: 1부터 중복과 누락 없이 오름차순으로 작성합니다.
- `TargetExp`: 해당 레벨에서 다음 레벨까지 필요한 경험치입니다. 마지막 레벨만 `0`으로 작성하고, 앞선 레벨은 양의 정수로 작성합니다.
- `SkillPointsOnReach`: 경험치로 해당 레벨에 도달할 때 지급할 스킬 포인트입니다. 1→2라면 2레벨 행의 값을 사용합니다. 여러 레벨을 한 번에 올리면 도달한 각 레벨의 값을 합산하여 `PlayerManager`를 통해 스킬 트리에 지급합니다. 보유 포인트는 `int.MaxValue`까지만 증가하며 시작 레벨 설정과 `SetLevel()`에는 지급하지 않습니다.
- `MaxHealth`, `AttackPower`, `DefensePower`, `MaxEnergy`: 증가량이 아닌 해당 레벨의 기본 스탯 값입니다. 최대 체력은 양수이고 나머지는 0 이상입니다.
- 소수점은 `.`을 사용합니다. 천 단위 구분자, NaN/Infinity, 셀 안의 줄바꿈은 허용하지 않습니다.

## 변환과 오류 처리

- CSV 수정 후 Unity가 Asset Refresh를 수행하면 자동 변환합니다.
- 수동 변환: `Tools > Player Progression > CSV 다시 변환`.
- 오류는 Console에 CSV 경로와 행/열을 표시하고, 기존 정상 SO 데이터를 유지합니다.
- CSV의 이름이나 하위 폴더를 바꾸면 기존 SO를 이동하여 GUID와 참조를 유지합니다. CSV의 `.meta` 파일도 함께 유지해야 합니다.
- CSV를 삭제하면 생성 SO는 자동 삭제하지 않습니다. 더 이상 사용하지 않는 데이터라면 CSV와 생성 SO를 함께 제거합니다.
- 빌드 전 전체 성장 CSV를 다시 검증·변환합니다. 잘못된 CSV나 원본이 사라진 SO가 있으면 빌드를 중단합니다.
- 생성 SO를 직접 편집한 값은 다음 변환 때 CSV의 값으로 복원됩니다. 수정은 CSV에서 수행하세요.

런타임에서는 생성된 SO의 `MaxLevel`과 `TryGetLevel(level, out data)`로 조회할 수 있습니다. CSV 파서와 에셋 저장 코드는 `Editor` 폴더에만 존재합니다.

## PlayerStat 연결

- 플레이어의 `PlayerStat` 컴포넌트에서 `Progression Definition`에 생성 SO를 지정합니다.
- 시작 시 Inspector의 `Level`에 해당하는 최대 체력, 공격력, 방어력, 최대 에너지, 다음 레벨 요구 경험치를 적용합니다. 시작 레벨은 성장표의 범위로 제한합니다.
- 레벨업 시 도달한 레벨 행의 기본 스탯을 적용하고, 남은 경험치는 새 요구 경험치에 따라 이월합니다. 한 번에 여러 레벨을 올릴 수 있습니다.
- 최대 체력 증가분만큼 현재 체력을 보충합니다. 최대 체력이 줄면 현재 체력을 새 최대치로 제한합니다. 사망한 플레이어의 체력은 회복하지 않습니다.
- 최대 에너지가 늘어도 현재 에너지는 유지하고, 줄어들면 새 최대치로 제한합니다.
- 마지막 레벨에서는 요구 경험치와 현재 경험치가 0이며 추가 경험치를 받지 않습니다.
- SO를 지정하지 않은 기존 플레이어는 Inspector 스탯과 고정 요구 경험치 동작을 유지합니다.
