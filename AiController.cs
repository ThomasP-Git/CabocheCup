using UnityEngine;



public class AiController : MonoBehaviour
{
    public bool hard = false;

    float timer;
    PlayerInput decision;

    public PlayerInput Think(PlayerController me, Rigidbody2D ball)
    {
        timer -= Time.deltaTime;
        if (timer > 0) return decision;
        timer = hard ? 0.03f : 0.11f; // temps de réaction
        decision = Decide(me, ball);
        return decision;
    }

    PlayerInput Decide(PlayerController me, Rigidbody2D ball)
    {
        var input = new PlayerInput();
        int f = me.facing;                        // notre but est du côté -f
        Vector2 b = ball.position;
        Vector2 p = me.transform.position;
        float predX = Mathf.Clamp(b.x + ball.linearVelocity.x * 0.2f, -5.4f, 5.4f);
        bool ballBehind = (b.x - p.x) * f < -0.06f;  // la balle est entre nous et notre but

        float targetX = ballBehind ? predX - f * 0.8f : predX - f * 0.48f;
        float dx = targetX - p.x;
        if (dx < -0.1f) input.left = true;
        else if (dx > 0.1f) input.right = true;

        float above = b.y - p.y;
        float horiz = Mathf.Abs(b.x - p.x);
        if (above > 0.5f && above < 2.8f && horiz < 0.9f && ball.linearVelocity.y < 2f) input.jump = true; // tête
        if (ballBehind && horiz < 0.85f && above < 0.3f) input.jump = true;                               // sauter par-dessus

        if (!ballBehind && Vector2.Distance(b, me.foot.position) < 0.8f && Random.value < (hard ? 0.9f : 0.5f))
            input.kick = true;

        return input;
    }
}
