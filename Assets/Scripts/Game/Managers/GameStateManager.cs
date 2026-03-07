using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public GameState currentState;

    void Start() 
    { 
        ChangeState(GameState.Game); 
    }

    public void ChangeState(GameState newState)
    {
        currentState = newState;
    }
}
