using UnityEngine;


public class Goal : MonoBehaviour
{
    int scoringPlayer;

    void Awake()
    {
        scoringPlayer = transform.position.x < 0 ? 2 : 1;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ball")) GameManager.Instance.GoalScored(scoringPlayer);
    }
}
