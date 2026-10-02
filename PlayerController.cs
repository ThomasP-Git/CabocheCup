using UnityEngine;

public struct PlayerInput { public bool left, right, jump, kick; }

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public int facing = 1;

    [Header("Touches")]
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode jumpKey = KeyCode.W;
    public KeyCode kickKey = KeyCode.Space;

    [Header("Références")]
    public Transform footPivot;   // objet vide au centre de la tête
    public Transform foot;        // le pied
    public Rigidbody2D ball;

    [Header("Réglages")]
    public float moveSpeed = 4f;
    public float jumpSpeed = 8.4f;
    public float kickDuration = 0.24f;
    
    Rigidbody2D rb;
    AiController ai;
    Vector3 startPos;
    bool grounded, kicking, kickHasHit;
    float kickTime;
    PlayerInput input;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        ai = GetComponent<AiController>();
        startPos = transform.position;
        SetupFoot();
    }

    // Range le pied sous le joueur (Player > FootPivot > Foot)
    void SetupFoot()
    {
        if (foot == null)
        {
            Debug.LogError($"{name} : le champ 'Foot' est vide dans PlayerController.");
            enabled = false;
            return;
        }
        if (footPivot == null || footPivot == foot) footPivot = new GameObject("FootPivot").transform;

        footPivot.SetParent(transform, false);
        footPivot.localPosition = Vector3.zero;
        footPivot.localRotation = Quaternion.identity;

        foot.SetParent(footPivot, false);
        foot.localPosition = new Vector3(0.15f * facing, -0.46f, 0);
        foot.localRotation = Quaternion.identity;

        // Seul le joueur doit avoir un Rigidbody2D
        RemoveRigidbody(footPivot);
        RemoveRigidbody(foot);
    }

    static void RemoveRigidbody(Transform t) // enlever le body
    {
        var body = t.GetComponent<Rigidbody2D>();
        if (body == null) return;
        body.simulated = false; // bloque le physique
        Destroy(body);
    }

   
    void Update()  // Les touches
    {
        if (!GameManager.Instance.CanPlay) { input = default; return; }

        PlayerInput i = (ai != null && ai.enabled)
            ? ai.Think(this, ball)
            : new PlayerInput
            {
                left = Input.GetKey(leftKey),
                right = Input.GetKey(rightKey),
                jump = Input.GetKey(jumpKey),
                kick = Input.GetKeyDown(kickKey),
            };

        input.left = i.left;
        input.right = i.right;
        input.jump = i.jump;
        if (i.kick) input.kick = true; // gardé jusqu'au prochain FixedUpdate
    }

    void FixedUpdate()
    {
        float target = ((input.right ? 1 : 0) - (input.left ? 1 : 0)) * moveSpeed;
        Vector2 v = rb.linearVelocity;
        v.x = Mathf.MoveTowards(v.x, target, 32f * Time.fixedDeltaTime); // accélération
        if (input.jump && grounded) v.y = jumpSpeed;
        rb.linearVelocity = v; // renvoie vitesse

        if (input.kick && !kicking) { kicking = true; kickHasHit = false; kickTime = 0; }
        input.kick = false;
        UpdateKick();

        grounded = false; 
    }

    void UpdateKick()
    {
        if (!kicking) return;
        kickTime += Time.fixedDeltaTime;
        float t = kickTime / kickDuration;
        if (t >= 1f)
        {
            kicking = false;
            footPivot.localRotation = Quaternion.identity;
            return;
        }
        footPivot.localRotation = Quaternion.Euler(0, 0, facing * Mathf.Sin(t * Mathf.PI) * 110f);
        if (!kickHasHit && t < 0.75f) TryHitBall();
    }

    void TryHitBall()
    {
        Vector2 b = ball.position;
        if (Vector2.Distance(b, foot.position) > 0.5f) return;
        if ((b.x - rb.position.x) * facing < -0.08f) return; // balle derrière le joueur

        kickHasHit = true;
        bool high = b.y > rb.position.y - 0.1f;
        Vector2 v = high ? new Vector2(facing * 12f, 2.8f)   // volée : tir tendu
                         : new Vector2(facing * 8.8f, 6.4f); // balle au sol : lob
        v.x += rb.linearVelocity.x * 0.35f;
        if (!grounded) v *= 1.1f;
        ball.linearVelocity = v;
    }

    void OnCollisionStay2D(Collision2D c)
    {
        if (c.rigidbody == ball) return;
        foreach (var contact in c.contacts)
            if (contact.normal.y > 0.5f) grounded = true; // sol ou tête de l'adversaire
    }

    public void ResetPosition()
    {
        transform.position = startPos;
        rb.position = startPos;
        rb.linearVelocity = Vector2.zero;
        kicking = false;
        footPivot.localRotation = Quaternion.identity;
    }
}
