using UnityEngine;

// 게임 전체 상태 관리
public class GameStateManager : MonoBehaviour
{
    public GameState currentState; // 현재 게임 상태

    void Start() 
    { 
        ChangeState(GameState.Game); 
    }

    public void ChangeState(GameState newState)
    {
        currentState = newState;
    }
}
