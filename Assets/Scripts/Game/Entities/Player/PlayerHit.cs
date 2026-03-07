using UnityEngine;

public class PlayerHit : MonoBehaviour, IDamageable 
{
    private PlayerController controller;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    public void TakeDamage(int damage)
    {
        if (controller != null)
        {
            controller.ChangeState(PlayerState.Hit);
        }
    }
}
