using UnityEngine;

// 주울 수 있는 상태의 드롭된 아이템 (객체 엔티티)
public class ItemDropEntity : MonoBehaviour, IInteractable
{
    public ItemStack itemData; // 드롭된 아이템 데이터

    public void Interact(PlayerController player)
    {
        // 플레이어 인벤토리에 itemData 추가 후 자신 파괴
        Destroy(gameObject);
    }
}
