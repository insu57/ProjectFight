using UnityEngine;

// 아이템 표시 문구와 등급 색상을 한 곳에 정의하여 여러 UI에서 같은 기준을 사용한다.
public static class ItemDisplayUtility
{
    public static string GetRarityText(ItemRarity rarity)
    {
        // enum.ToString이나 문자열 포맷 대신 고정 문구를 사용한다.
        return rarity switch
        {
            ItemRarity.Common => "일반",
            ItemRarity.Uncommon => "고급",
            ItemRarity.Rare => "희귀",
            ItemRarity.Epic => "영웅",
            ItemRarity.Legendary => "전설",
            _ => string.Empty
        };
    }

    public static string GetItemTypeText(ItemType itemType)
    {
        return itemType switch
        {
            ItemType.Equipment => "장비",
            ItemType.Consumable => "소모품",
            ItemType.Relic => "유물",
            _ => string.Empty
        };
    }

    public static string GetEquipmentSlotText(EquipmentSlot equipmentSlot)
    {
        // 고정 문구로 장비 부위를 표시하여 enum 문자열 변환 할당을 피한다.
        return equipmentSlot switch
        {
            EquipmentSlot.None => "미지정",
            EquipmentSlot.Head => "머리",
            EquipmentSlot.UpperBody => "상의",
            EquipmentSlot.LowerBody => "하의",
            EquipmentSlot.Gloves => "장갑",
            EquipmentSlot.Shoes => "신발",
            _ => string.Empty
        };
    }

    public static Color GetPriceColor(bool canAfford)
    {
        // 지불 가능한 가격과 금액이 부족한 가격의 표시 색상을 공통으로 관리한다.
        return canAfford ? Color.black : Color.red;
    }

    public static Color GetRarityColor(ItemRarity rarity)
    {
        // Color32는 값 형식이므로 색상 변환 시 별도 객체를 할당하지 않는다.
        return rarity switch
        {
            ItemRarity.Common => Color.white,
            ItemRarity.Uncommon => new Color32(76, 175, 80, 255),
            ItemRarity.Rare => new Color32(66, 165, 245, 255),
            ItemRarity.Epic => new Color32(171, 71, 188, 255),
            ItemRarity.Legendary => new Color32(255, 167, 38, 255),
            _ => Color.white
        };
    }
}
