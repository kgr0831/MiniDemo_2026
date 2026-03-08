using UnityEngine;

// 상호작용 가능 구조물
public class InteractObject : MonoBehaviour, IInteractable
{
    public void Interact(PlayerController player)
    {
        Debug.Log("구조물과 상호작용 했습니다!");
    }
}
