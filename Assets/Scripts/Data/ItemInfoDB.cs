using UnityEngine;

[CreateAssetMenu(fileName = "NewItem_DB", menuName = "Data/ItemInfo")]
public class ItemInfoDB : ScriptableObject
{
    public int index; // 아이템 식별 번호
    public string itemName; // 아이템 이름
    public ItemType type; // 아이템 종류
    public float weight; // 아이템 무게
}
