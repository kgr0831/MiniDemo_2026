using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public CharacterData myData;
    public PlayerState currentState;

    private PlayerMove moveModule;
    private PlayerAnim animModule;
    private PlayerAttack attackModule;
    private PlayerInteract interactModule;
    private PlayerHit hitModule;

    void Awake()
    {
        moveModule = GetComponent<PlayerMove>();
        animModule = GetComponent<PlayerAnim>();
        attackModule = GetComponent<PlayerAttack>();
        interactModule = GetComponent<PlayerInteract>();
        hitModule = GetComponent<PlayerHit>();
    }

    public void ChangeState(PlayerState newState)
    {
        currentState = newState;
        if (animModule != null)
        {
            animModule.PlayStateAnim(newState);
        }
    }
}
