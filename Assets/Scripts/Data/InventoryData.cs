using System.Collections.Generic;

[System.Serializable]
public class InventoryData 
{
    // Dictionary(몇번째 칸인지 위치 정보(int), 아이템(ItemStack)) - 보유 아이템 리스트
    public Dictionary<int, ItemStack> itemList = new Dictionary<int, ItemStack>();
}
