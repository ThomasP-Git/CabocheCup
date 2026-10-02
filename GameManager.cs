using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Score, chrono, engagement, but en or et fin de match.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerController player1, player2;
    public Rigidbody2D ball;
    public TMP_Text scoreText, timerText, messageText;
    public float matchDuration = 90f;

    public bool CanPlay { get; private set; }

    int score1, score2;
    float timeLeft;
    bool suddenDeath, gameOver;

    void Awake()
    {
        Instance = this;
        Physics2D.gravity = new Vector2(0, -19f); // gravité plus forte = jeu plus nerveux
    }

    void Start()
    {
        timeLeft = matchDuration;
        StartCoroutine(KickOff());
    }

    void Update()
    {
        if (gameOver)
        {
            if (Input.GetKeyDown(KeyCode.R)) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        if (CanPlay && !suddenDeath)
        {
            timeLeft -= Time.deltaTime;
            if (timeLeft <= 0)
            {
                timeLeft = 0;
                if (score1 != score2) EndMatch();
                else { suddenDeath = true; StartCoroutine(Flash("BUT EN OR !")); }
            }
        }
        UpdateUi();
    }

    public void GoalScored(int who)
    {
        if (!CanPlay) return;
        if (who == 1) score1++; else score2++;
        CanPlay = false;
        StartCoroutine(AfterGoal(who));
    }

    IEnumerator AfterGoal(int who)
    {
        messageText.text = $"BUT DE J{who} !";
        yield return new WaitForSeconds(2f);
        if (suddenDeath) EndMatch();
        else yield return KickOff();
    }

    IEnumerator KickOff()
    {
        CanPlay = false;
        player1.ResetPosition();
        player2.ResetPosition();

        ball.simulated = false; // balle figée pendant le décompte
        Vector2 start = new Vector2(0, 1.5f);
        ball.transform.position = start;
        ball.position = start;
        ball.linearVelocity = new Vector2(Random.Range(-0.8f, 0.8f), 0);
        ball.angularVelocity = 0;

        messageText.text = "PRET ?";
        yield return new WaitForSeconds(1f);
        ball.simulated = true;
        CanPlay = true;
        yield return Flash("GO !");
    }

    IEnumerator Flash(string text)
    {
        messageText.text = text;
        yield return new WaitForSeconds(0.8f);
        if (messageText.text == text) messageText.text = "";
    }

    void EndMatch()
    {
        gameOver = true;
        CanPlay = false;
        messageText.text = (score1 > score2 ? "VICTOIRE DE J1 !" : "VICTOIRE DE J2 !") + "\nR pour rejouer";
        UpdateUi();
    }

    void UpdateUi()
    {
        scoreText.text = $"{score1}  -  {score2}";
        int t = Mathf.CeilToInt(timeLeft);
        timerText.text = suddenDeath ? "BUT EN OR" : $"{t / 60}:{t % 60:00}";
    }
}
