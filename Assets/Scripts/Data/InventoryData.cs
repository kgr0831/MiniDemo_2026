using System.Collections.Generic;

[System.Serializable]
public class InventoryData 
{
    public Dictionary<int, ItemStack> itemList = new Dictionary<int, ItemStack>();
}
