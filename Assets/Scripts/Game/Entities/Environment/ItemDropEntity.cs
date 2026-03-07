using UnityEngine;

public class ItemDropEntity : MonoBehaviour, IInteractable
{
    public ItemStack itemData;

    public void Interact(PlayerController player)
    {
        Destroy(gameObject);
    }
}
