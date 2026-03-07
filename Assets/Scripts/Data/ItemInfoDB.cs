using UnityEngine;

[CreateAssetMenu(fileName = "NewItem_DB", menuName = "Data/ItemInfo")]
public class ItemInfoDB : ScriptableObject
{
    public int index;
    public string itemName;
    public ItemType type;
    public float weight;
}
