namespace HeadArena;

public sealed record Input(string? Action, string? Mode, string? Difficulty, bool Left, bool Right, bool Jump, bool Kick, bool Left2, bool Right2, bool Jump2, bool Kick2);
public sealed class Body(double x, double y, double radius) {
    public double X = x, Y = y, Vx, Vy;
    public readonly double R = radius;
    public double Cooldown;
}
public sealed class Game {
    public const double Width = 1100, Floor = 540, GoalTop = 390;
    public readonly Body P1 = new(290, Floor - 36, 36), P2 = new(810, Floor - 36, 36), Ball = new(550, 240, 18);
    public int Score1, Score2;
    public double Remaining = 90, Freeze, KickFlash1, KickFlash2;
    public string Phase = "menu", Mode = "solo", Difficulty = "normal", Message = "";
    private Input controls = new(null, null, null, false, false, false, false, false, false, false, false);
    private bool jumpWas1, jumpWas2;
    public void Handle(Input input) {
        controls = input;
        switch (input.Action) {
            case "start":
                Mode = input.Mode == "versus" ? "versus" : "solo";
                Difficulty = input.Difficulty is "easy" or "hard" ? input.Difficulty : "normal";
                Score1 = Score2 = 0; Remaining = 90; Phase = "playing"; Reset(); Freeze = 1.1; Message = "C'EST PARTI !"; break;
            case "pause": if (Phase == "playing") Phase = "paused"; else if (Phase == "paused") Phase = "playing"; break;
            case "menu": Phase = "menu"; break;
        }
    }
    public void Reset() {
        P1.X = 290; P2.X = 810; P1.Y = P2.Y = Floor - 36;
        P1.Vx = P1.Vy = P2.Vx = P2.Vy = 0;
        P1.Cooldown = P2.Cooldown = KickFlash1 = KickFlash2 = 0;
        Ball.X = Width / 2; Ball.Y = 240; Ball.Vx = Ball.Vy = 0;
        jumpWas1 = jumpWas2 = false;
    }
    public void Step(double dt) {
        if (Phase != "playing") return;
        if (Freeze > 0) { Freeze = Math.Max(0, Freeze - dt); return; }
        Remaining = Math.Max(0, Remaining - dt);
        if (Remaining == 0) { Phase = "finished"; Message = Score1 == Score2 ? "MATCH NUL" : Score1 > Score2 ? "BLEU GAGNE !" : "ORANGE GAGNE !"; return; }
        KickFlash1 = Math.Max(0, KickFlash1 - dt); KickFlash2 = Math.Max(0, KickFlash2 - dt);
        bool aiJump = false, aiKick = false;
        double aiMove = 0;
        if (Mode == "solo") {
            double target = Ball.X > 520 ? Ball.X + 42 : 810;
            target = Math.Clamp(target, 90, 1020);
            aiMove = Math.Abs(target - P2.X) > 15 ? Math.Sign(target - P2.X) : 0;
            aiJump = Ball.Y < P2.Y - 50 && Ball.Y > 230 && Math.Abs(Ball.X - P2.X) < 115;
            aiKick = Ball.X < P2.X + 20 && Math.Abs(Ball.X - P2.X) < 115 && Math.Abs(Ball.Y - P2.Y) < 100;
        }
        Move(P1, (controls.Right ? 1 : 0) - (controls.Left ? 1 : 0), controls.Jump && !jumpWas1, dt, 330);
        Move(P2, Mode == "solo" ? aiMove : (controls.Right2 ? 1 : 0) - (controls.Left2 ? 1 : 0), Mode == "solo" ? aiJump : controls.Jump2 && !jumpWas2, dt, Mode == "solo" ? Difficulty == "easy" ? 205 : Difficulty == "hard" ? 355 : 285 : 330);
        jumpWas1 = controls.Jump; jumpWas2 = controls.Jump2;
        // Small substeps keep fast shots from tunnelling through heads or posts.
        for (int i = 0; i < 4; i++) {
            double h = dt / 4;
            Ball.Vy += 900 * h; Ball.X += Ball.Vx * h; Ball.Y += Ball.Vy * h;
            Ball.Vx *= Math.Pow(0.997, h * 60);
            Collide(P1); Collide(P2);
            Post(48, GoalTop); Post(Width - 48, GoalTop);
            if (Ball.Y + Ball.R > Floor) { Ball.Y = Floor - Ball.R; Ball.Vy = -Math.Abs(Ball.Vy) * .73; if (Math.Abs(Ball.Vy) < 25) Ball.Vy = 0; Ball.Vx *= .985; }
            if (Ball.Y < Ball.R) { Ball.Y = Ball.R; Ball.Vy = Math.Abs(Ball.Vy) * .8; }
            if (Ball.Y - Ball.R > GoalTop && (Ball.X < 26 || Ball.X > Width - 26)) {
                if (Ball.X < 26) Score2++; else Score1++;
                Reset(); Freeze = 1.4; Message = "BUUUUT !"; return;
            }
            if (Ball.X < Ball.R) { Ball.X = Ball.R; Ball.Vx = Math.Abs(Ball.Vx) * .8; }
            if (Ball.X > Width - Ball.R) { Ball.X = Width - Ball.R; Ball.Vx = -Math.Abs(Ball.Vx) * .8; }
        }
        if (controls.Kick && Shoot(P1, 1)) KickFlash1 = .16;
        if ((Mode == "solo" ? aiKick : controls.Kick2) && Shoot(P2, -1)) KickFlash2 = .16;
        Ball.Vx = Math.Clamp(Ball.Vx, -950, 950); Ball.Vy = Math.Clamp(Ball.Vy, -1000, 1000);
    }
    private static void Move(Body p, double direction, bool jump, double dt, double speed) {
        p.Cooldown = Math.Max(0, p.Cooldown - dt);
        p.Vx = direction * speed;
        if (jump && p.Y >= Floor - p.R - .1) p.Vy = -610;
        p.Vy += 1500 * dt; p.X = Math.Clamp(p.X + p.Vx * dt, 75, Width - 75); p.Y += p.Vy * dt;
        if (p.Y >= Floor - p.R) { p.Y = Floor - p.R; p.Vy = 0; }
    }
    private void Collide(Body p) {
        double dx = Ball.X - p.X, dy = Ball.Y - p.Y, d = Math.Sqrt(dx * dx + dy * dy), min = p.R + Ball.R;
        if (d >= min) return;
        double nx = d < .001 ? 0 : dx / d, ny = d < .001 ? -1 : dy / d;
        Ball.X = p.X + nx * min; Ball.Y = p.Y + ny * min;
        double v = (Ball.Vx - p.Vx) * nx + (Ball.Vy - p.Vy) * ny;
        if (v < 0) { Ball.Vx -= 1.8 * v * nx; Ball.Vy -= 1.8 * v * ny; }
    }
    private void Post(double x, double y) {
        double dx = Ball.X - x, dy = Ball.Y - y, d = Math.Sqrt(dx * dx + dy * dy);
        if (d >= Ball.R + 6) return;
        double nx = d < .001 ? 0 : dx / d, ny = d < .001 ? -1 : dy / d;
        Ball.X = x + nx * (Ball.R + 6); Ball.Y = y + ny * (Ball.R + 6);
        double v = Ball.Vx * nx + Ball.Vy * ny;
        if (v < 0) { Ball.Vx -= 1.8 * v * nx; Ball.Vy -= 1.8 * v * ny; }
    }
    private bool Shoot(Body p, int direction) {
        if (p.Cooldown > 0) return false;
        p.Cooldown = .32;
        double dx = Ball.X - p.X, dy = Ball.Y - p.Y;
        if (dx * dx + dy * dy < 125 * 125) { Ball.Vx = direction * 790 + p.Vx * .25; Ball.Vy = -440; }
        return true;
    }
    public object Snapshot() => new { phase = Phase, mode = Mode, score1 = Score1, score2 = Score2, remaining = Remaining, freeze = Freeze, message = Message,
        p1 = new { x = P1.X, y = P1.Y, kick = KickFlash1, ready = P1.Cooldown == 0 },
        p2 = new { x = P2.X, y = P2.Y, kick = KickFlash2, ready = P2.Cooldown == 0 }, ball = new { x = Ball.X, y = Ball.Y } };
}
