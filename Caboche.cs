// HEAD FOOT - un jeu de foot "grosses têtes" inspiré de Head Soccer / Head Football.
// Tout est dessiné en code (WinForms + GDI+), aucune ressource externe.
//
// Joueur 1 : Q/A = gauche, D = droite, Z/W = sauter, Espace = tirer
// Joueur 2 : ← → = déplacement, ↑ = sauter, ↓ ou Entrée = tirer
// P / Échap : pause

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Media;
using System.Windows.Forms;

namespace HeadFoot
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GameForm());
        }
    }

    internal static class Field
    {
        public const float W = 1000f;
        public const float H = 600f;
        public const float GroundY = 530f;   // niveau où les pieds touchent le sol
        public const float GoalW = 78f;      // profondeur des cages
        public const float GoalTop = 330f;   // dessous de la barre transversale
        public const float BarH = 10f;

        public static readonly RectangleF LeftBar = new RectangleF(0, GoalTop - BarH, GoalW + 8, BarH);
        public static readonly RectangleF RightBar = new RectangleF(W - GoalW - 8, GoalTop - BarH, GoalW + 8, BarH);
    }

    internal enum GameState { Menu, Ready, Playing, Goal, Paused, GameOver }

    internal enum Difficulty { Facile, Normal, Difficile }

    internal readonly record struct Input(bool Left, bool Right, bool Jump, bool Kick);

    internal sealed class Ball
    {
        public const float R = 15f;
        public float X, Y, VX, VY, Angle;
    }

    internal sealed class Player
    {
        public const float R = 34f;          // rayon de la tête
        public const float ShoeR = 12f;
        public const float FootH = 16f;      // hauteur entre le bas de la tête et le sol
        public const int KickDuration = 14;
        public static float RestY => Field.GroundY - R - FootH;

        public float X, Y, VX, VY;
        public bool OnGround = true;
        public int KickTimer;
        public bool KickHit;
        public int Score;
        public float SpeedMul = 1f;
        public string Name = "";

        public readonly int Facing;          // +1 regarde à droite, -1 à gauche
        public readonly Color Skin, Hair, Team;

        public Player(int facing, Color skin, Color hair, Color team)
        {
            Facing = facing;
            Skin = skin;
            Hair = hair;
            Team = team;
        }

        public float KickSwing =>
            KickTimer > 0 ? MathF.Sin(MathF.PI * (1f - KickTimer / (float)KickDuration)) : 0f;

        public float ShoeX => X + Facing * (10f + 30f * KickSwing);
        public float ShoeY => Y + R + 4f - 24f * KickSwing;
    }

    internal sealed class GameForm : Form
    {
        private const float W = Field.W, H = Field.H;
        private const float PlayerGravity = 0.6f;
        private const float BallGravity = 0.4f;
        private const float MoveSpeed = 5.2f;
        private const float JumpSpeed = 12.8f;
        private const float MaxBallSpeed = 22f;
        private const int MatchSeconds = 90;
        private const double Step = 1.0 / 60.0;

        private readonly HashSet<Keys> down = new HashSet<Keys>();
        private readonly HashSet<Keys> pressed = new HashSet<Keys>();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 1 };
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random rng = new Random();
        private double lastTime, accumulator;

        private GameState state = GameState.Menu;
        private Difficulty difficulty = Difficulty.Normal;
        private bool vsCpu = true;
        private readonly Player p1, p2;
        private readonly Ball ball = new Ball();
        private int framesLeft, stateTimer;
        private string banner = "";

        private Input aiInput;
        private int aiThinkTimer;

        private Bitmap? background;
        private float scale = 1f;
        private PointF offset;

        private readonly Font fontTitle = new Font("Segoe UI", 84, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font fontBig = new Font("Segoe UI", 64, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font fontMid = new Font("Segoe UI", 30, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font fontSmall = new Font("Segoe UI", 19, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font fontTiny = new Font("Segoe UI", 16, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font fontScore = new Font("Consolas", 38, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly HatchBrush netBrush = new HatchBrush(HatchStyle.DiagonalCross, Color.FromArgb(150, 255, 255, 255), Color.FromArgb(40, 255, 255, 255));

        private static readonly StringFormat Center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        public GameForm()
        {
            Text = "Head Foot";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.Black;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            float dpi = DeviceDpi / 96f;
            Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
            float fit = Math.Min(1f, Math.Min(area.Width * 0.9f / (W * dpi), area.Height * 0.85f / (H * dpi)));
            ClientSize = new Size((int)(W * dpi * fit), (int)(H * dpi * fit));
            MinimumSize = new Size(500, 330);

            p1 = new Player(1, Color.FromArgb(241, 194, 150), Color.FromArgb(70, 40, 20), Color.FromArgb(215, 40, 45)) { Name = "JOUEUR 1" };
            p2 = new Player(-1, Color.FromArgb(198, 134, 86), Color.FromArgb(245, 205, 60), Color.FromArgb(35, 95, 215)) { Name = "CPU" };
            Kickoff();

            timer.Tick += (_, _) => Tick();
            timer.Start();
            Deactivate += (_, _) => down.Clear();
        }

        // ------------------------------------------------------------------ Entrées

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.Alt) != 0)
                return base.ProcessCmdKey(ref msg, keyData);
            Keys k = keyData & Keys.KeyCode;
            if (down.Add(k))
                pressed.Add(k);
            return true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            down.Remove(e.KeyCode);
            base.OnKeyUp(e);
        }

        private bool IsDown(params Keys[] keys)
        {
            foreach (var k in keys)
                if (down.Contains(k)) return true;
            return false;
        }

        private bool WasPressed(params Keys[] keys)
        {
            foreach (var k in keys)
                if (pressed.Contains(k)) return true;
            return false;
        }

        private Input ReadP1() => new Input(
            IsDown(Keys.Q, Keys.A),
            IsDown(Keys.D),
            IsDown(Keys.Z, Keys.W),
            IsDown(Keys.Space) || WasPressed(Keys.Space));

        private Input ReadP2() => new Input(
            IsDown(Keys.Left),
            IsDown(Keys.Right),
            IsDown(Keys.Up),
            IsDown(Keys.Down, Keys.Enter, Keys.NumPad0) || WasPressed(Keys.Down, Keys.Enter, Keys.NumPad0));

        // ------------------------------------------------------------------ Boucle

        private void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            accumulator += Math.Min(0.25, now - lastTime);
            lastTime = now;

            bool stepped = false;
            while (accumulator >= Step)
            {
                UpdateGame();
                pressed.Clear();
                accumulator -= Step;
                stepped = true;
            }
            if (stepped)
                Invalidate();
        }

        private void UpdateGame()
        {
            switch (state)
            {
                case GameState.Menu:
                    if (WasPressed(Keys.D1, Keys.NumPad1, Keys.Enter)) StartMatch(true);
                    else if (WasPressed(Keys.D2, Keys.NumPad2)) StartMatch(false);
                    else if (WasPressed(Keys.Left) && difficulty > Difficulty.Facile) difficulty--;
                    else if (WasPressed(Keys.Right) && difficulty < Difficulty.Difficile) difficulty++;
                    else if (WasPressed(Keys.Escape)) Close();
                    break;

                case GameState.Ready:
                    if (--stateTimer <= 0) state = GameState.Playing;
                    break;

                case GameState.Playing:
                    if (WasPressed(Keys.P, Keys.Escape))
                    {
                        state = GameState.Paused;
                        break;
                    }
                    Simulate(true);
                    if (state == GameState.Playing && --framesLeft <= 0)
                        EndMatch();
                    break;

                case GameState.Goal:
                    Simulate(false);
                    if (--stateTimer <= 0)
                    {
                        if (framesLeft <= 0) EndMatch();
                        else BeginKickoff();
                    }
                    break;

                case GameState.Paused:
                    if (WasPressed(Keys.P)) state = GameState.Playing;
                    else if (WasPressed(Keys.Escape)) GoToMenu();
                    break;

                case GameState.GameOver:
                    if (WasPressed(Keys.Enter, Keys.Space)) StartMatch(vsCpu);
                    else if (WasPressed(Keys.Escape)) GoToMenu();
                    break;
            }
        }

        private void StartMatch(bool cpu)
        {
            vsCpu = cpu;
            p1.Score = p2.Score = 0;
            p2.Name = cpu ? "CPU" : "JOUEUR 2";
            p2.SpeedMul = !cpu ? 1f : difficulty switch
            {
                Difficulty.Facile => 0.72f,
                Difficulty.Normal => 0.88f,
                _ => 1f,
            };
            framesLeft = MatchSeconds * 60;
            BeginKickoff();
        }

        private void BeginKickoff()
        {
            Kickoff();
            state = GameState.Ready;
            stateTimer = 60;
            banner = "C'EST PARTI !";
        }

        private void GoToMenu()
        {
            Kickoff();
            state = GameState.Menu;
        }

        private void Kickoff()
        {
            foreach (var p in new[] { p1, p2 })
            {
                p.Y = Player.RestY;
                p.VX = p.VY = 0;
                p.KickTimer = 0;
                p.OnGround = true;
            }
            p1.X = W * 0.25f;
            p2.X = W * 0.75f;
            ball.X = W / 2;
            ball.Y = 170;
            ball.VX = (rng.NextSingle() - 0.5f) * 3f;
            ball.VY = 0;
            aiThinkTimer = 0;
        }

        private void GoalFor(Player scorer)
        {
            scorer.Score++;
            state = GameState.Goal;
            stateTimer = 120;
            banner = "BUUUT !";
            SystemSounds.Asterisk.Play();
        }

        private void EndMatch()
        {
            state = GameState.GameOver;
            if (p1.Score == p2.Score) banner = "MATCH NUL";
            else banner = "VICTOIRE : " + (p1.Score > p2.Score ? p1.Name : p2.Name);
        }

        // ------------------------------------------------------------------ Physique

        private void Simulate(bool controls)
        {
            Input i1 = controls ? ReadP1() : default;
            Input i2 = controls ? (vsCpu ? ThinkAI() : ReadP2()) : default;
            UpdatePlayer(p1, i1);
            UpdatePlayer(p2, i2);
            CollidePlayers();

            ball.VY += BallGravity;
            ball.VX *= 0.998f;

            // Sous-pas pour éviter que la balle traverse les têtes ou la barre.
            const int Sub = 3;
            for (int s = 0; s < Sub; s++)
            {
                ball.X += ball.VX / Sub;
                ball.Y += ball.VY / Sub;
                CollideBallWorld();
                CollideBallPlayer(p1);
                CollideBallPlayer(p2);
            }

            float speed = MathF.Sqrt(ball.VX * ball.VX + ball.VY * ball.VY);
            if (speed > MaxBallSpeed)
            {
                ball.VX *= MaxBallSpeed / speed;
                ball.VY *= MaxBallSpeed / speed;
            }
            ball.Angle += ball.VX / Ball.R;

            if (controls)
                CheckGoal();
        }

        private void UpdatePlayer(Player p, Input input)
        {
            float target = 0;
            if (input.Left) target -= MoveSpeed * p.SpeedMul;
            if (input.Right) target += MoveSpeed * p.SpeedMul;
            p.VX += (target - p.VX) * (p.OnGround ? 0.35f : 0.2f);

            if (input.Jump && p.OnGround)
            {
                p.VY = -JumpSpeed;
                p.OnGround = false;
            }
            if (input.Kick && p.KickTimer == 0)
            {
                p.KickTimer = Player.KickDuration;
                p.KickHit = false;
            }
            if (p.KickTimer > 0)
                p.KickTimer--;

            p.VY += PlayerGravity;
            p.X += p.VX;
            p.Y += p.VY;

            p.OnGround = false;
            if (p.Y >= Player.RestY)
            {
                p.Y = Player.RestY;
                p.VY = 0;
                p.OnGround = true;
            }
            p.X = Math.Clamp(p.X, Player.R, W - Player.R);

            foreach (var bar in new[] { Field.LeftBar, Field.RightBar })
            {
                if (CircleRect(ref p.X, ref p.Y, ref p.VX, ref p.VY, Player.R, bar, 0f, out float ny) && ny < -0.6f)
                {
                    p.VY = 0;
                    p.OnGround = true;   // on peut se tenir sur la barre
                }
            }
        }

        private void CollidePlayers()
        {
            float dx = p2.X - p1.X, dy = p2.Y - p1.Y;
            float min = 2 * Player.R;
            float d2 = dx * dx + dy * dy;
            if (d2 >= min * min) return;

            float d = MathF.Sqrt(d2);
            if (d < 0.01f) { dx = 1; dy = 0; d = 1; }
            float nx = dx / d, ny = dy / d, overlap = min - d;

            if (ny > 0.6f)            // p1 est sur la tête de p2
            {
                p1.X -= nx * overlap;
                p1.Y -= ny * overlap;
                if (p1.VY > 0) p1.VY = 0;
                p1.OnGround = true;
            }
            else if (ny < -0.6f)      // p2 est sur la tête de p1
            {
                p2.X += nx * overlap;
                p2.Y += ny * overlap;
                if (p2.VY > 0) p2.VY = 0;
                p2.OnGround = true;
            }
            else
            {
                p1.X -= nx * overlap / 2;
                p2.X += nx * overlap / 2;
            }
            p1.X = Math.Clamp(p1.X, Player.R, W - Player.R);
            p2.X = Math.Clamp(p2.X, Player.R, W - Player.R);
        }

        private void CollideBallWorld()
        {
            if (ball.Y > Field.GroundY - Ball.R)
            {
                ball.Y = Field.GroundY - Ball.R;
                if (ball.VY > 0) ball.VY = -ball.VY * 0.7f;
                if (MathF.Abs(ball.VY) < 1.5f) ball.VY = 0;
                ball.VX *= 0.995f;
            }
            if (ball.Y < Ball.R)
            {
                ball.Y = Ball.R;
                ball.VY = MathF.Abs(ball.VY) * 0.8f;
            }
            if (ball.X < Ball.R)
            {
                ball.X = Ball.R;
                ball.VX = MathF.Abs(ball.VX) * 0.6f;
            }
            if (ball.X > W - Ball.R)
            {
                ball.X = W - Ball.R;
                ball.VX = -MathF.Abs(ball.VX) * 0.6f;
            }

            if (CircleRect(ref ball.X, ref ball.Y, ref ball.VX, ref ball.VY, Ball.R, Field.LeftBar, 0.7f, out float ny) && ny < -0.7f)
                ball.VX += 0.25f;   // la balle roule hors de la barre
            if (CircleRect(ref ball.X, ref ball.Y, ref ball.VX, ref ball.VY, Ball.R, Field.RightBar, 0.7f, out ny) && ny < -0.7f)
                ball.VX -= 0.25f;
        }

        private void CollideBallPlayer(Player p)
        {
            CollideBallCircle(p.X, p.Y, Player.R, p.VX, p.VY, 0.6f);
            if (p.KickTimer > 0 && !p.KickHit)
                TryKick(p);
            CollideBallCircle(p.ShoeX, p.ShoeY, Player.ShoeR, p.VX, p.VY, 0.4f);
        }

        private void TryKick(Player p)
        {
            float dx = ball.X - p.ShoeX, dy = ball.Y - p.ShoeY;
            float reach = Player.ShoeR + Ball.R + 16f;
            if (dx * dx + dy * dy > reach * reach || dx * p.Facing < -12f)
                return;

            p.KickHit = true;
            if (p.OnGround)
            {
                // tir lobé
                ball.VX = p.Facing * 13.5f + p.VX * 0.6f;
                ball.VY = -10.5f;
            }
            else
            {
                // volée : tir tendu et puissant
                ball.VX = p.Facing * 18f + p.VX * 0.5f;
                ball.VY = -3.5f;
            }
        }

        private void CollideBallCircle(float cx, float cy, float cr, float cvx, float cvy, float restitution)
        {
            float dx = ball.X - cx, dy = ball.Y - cy;
            float min = cr + Ball.R;
            float d2 = dx * dx + dy * dy;
            if (d2 >= min * min) return;

            float d = MathF.Sqrt(d2);
            if (d < 0.001f) { dx = 0; dy = -1; d = 1; }
            float nx = dx / d, ny = dy / d;
            ball.X = cx + nx * min;
            ball.Y = cy + ny * min;

            float rvx = ball.VX - cvx, rvy = ball.VY - cvy;
            float vn = rvx * nx + rvy * ny;
            if (vn < 0)
            {
                rvx -= (1 + restitution) * vn * nx;
                rvy -= (1 + restitution) * vn * ny;
            }
            ball.VX = rvx + cvx;
            ball.VY = rvy + cvy;
        }

        private static bool CircleRect(ref float x, ref float y, ref float vx, ref float vy, float r, RectangleF rc, float restitution, out float ny)
        {
            float cx = Math.Clamp(x, rc.Left, rc.Right);
            float cy = Math.Clamp(y, rc.Top, rc.Bottom);
            float dx = x - cx, dy = y - cy;
            float d2 = dx * dx + dy * dy;
            ny = 0;
            if (d2 >= r * r) return false;

            float nx;
            float d = MathF.Sqrt(d2);
            if (d < 0.001f)
            {
                nx = 0;
                ny = -1;
                y = rc.Top - r;
            }
            else
            {
                nx = dx / d;
                ny = dy / d;
                x = cx + nx * r;
                y = cy + ny * r;
            }

            float vn = vx * nx + vy * ny;
            if (vn < 0)
            {
                vx -= (1 + restitution) * vn * nx;
                vy -= (1 + restitution) * vn * ny;
            }
            return true;
        }

        private void CheckGoal()
        {
            if (ball.Y <= Field.GoalTop) return;
            if (ball.X < Field.GoalW - Ball.R * 0.5f) GoalFor(p2);
            else if (ball.X > W - Field.GoalW + Ball.R * 0.5f) GoalFor(p1);
        }

        // ------------------------------------------------------------------ IA (joueur de droite)

        private Input ThinkAI()
        {
            if (--aiThinkTimer > 0)
                return aiInput;
            aiThinkTimer = difficulty switch { Difficulty.Facile => 12, Difficulty.Normal => 6, _ => 2 };

            Player p = p2;
            float look = difficulty switch { Difficulty.Facile => 4f, Difficulty.Normal => 10f, _ => 16f };
            float predictX = Math.Clamp(ball.X + ball.VX * look, Ball.R, W - Ball.R);

            // La balle est entre l'IA et son but : il faut repasser derrière.
            bool ballBehind = ball.X > p.X + 8f;
            float target = ballBehind
                ? ball.X + Player.R + Ball.R + 10f
                : predictX + Player.R * 0.6f;

            // Hors difficulté max, l'IA reste un peu en défense.
            if (difficulty != Difficulty.Difficile && ball.X < W * 0.3f)
                target = Math.Max(target, W * 0.58f);
            target = Math.Clamp(target, Player.R, W - Player.R);

            bool left = target < p.X - 8f;
            bool right = target > p.X + 8f;

            float dx = ball.X - p.X, dy = ball.Y - p.Y;
            bool headerChance = MathF.Abs(dx) < 70f && dy < -40f && dy > -170f && ball.VY > -3f;
            bool jumpOver = ballBehind && MathF.Abs(dx) < 60f && ball.Y > p.Y - 20f;
            bool jump = p.OnGround && (headerChance || jumpOver);

            float sdx = ball.X - p.ShoeX, sdy = ball.Y - p.ShoeY;
            bool kick = sdx < 10f && sdx > -70f && MathF.Abs(sdy) < 50f;
            if (difficulty == Difficulty.Facile && rng.NextDouble() < 0.35) kick = false;
            if (difficulty == Difficulty.Facile && rng.NextDouble() < 0.4) jump = false;

            aiInput = new Input(left, right, jump, kick);
            return aiInput;
        }

        // ------------------------------------------------------------------ Rendu

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateView();
            Invalidate();
        }

        private void UpdateView()
        {
            Size cs = ClientSize;
            if (cs.Width < 10 || cs.Height < 10) return;
            scale = Math.Min(cs.Width / W, cs.Height / H);
            offset = new PointF((cs.Width - W * scale) / 2, (cs.Height - H * scale) / 2);

            background?.Dispose();
            background = new Bitmap(cs.Width, cs.Height);
            using Graphics g = Graphics.FromImage(background);
            g.Clear(Color.Black);
            ApplyView(g);
            DrawStadium(g);
        }

        private void ApplyView(Graphics g)
        {
            g.TranslateTransform(offset.X, offset.Y);
            g.ScaleTransform(scale, scale);
            g.SetClip(new RectangleF(0, 0, W, H));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            if (background == null) UpdateView();
            if (background == null) return;

            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(background, 0, 0, background.Width, background.Height);
            g.CompositingMode = CompositingMode.SourceOver;

            ApplyView(g);
            DrawBall(g);
            DrawPlayer(g, p1);
            DrawPlayer(g, p2);
            DrawGoalFront(g, true);
            DrawGoalFront(g, false);

            if (state != GameState.Menu)
                DrawHud(g);

            switch (state)
            {
                case GameState.Menu: DrawMenu(g); break;
                case GameState.Ready: Label(g, banner, fontBig, Color.White, W / 2, 230); break;
                case GameState.Goal: Label(g, banner, fontTitle, Color.Gold, W / 2, 220); break;
                case GameState.Paused:
                    Dim(g, 140);
                    Label(g, "PAUSE", fontBig, Color.White, W / 2, 230);
                    Label(g, "P : reprendre     Échap : menu", fontSmall, Color.White, W / 2, 300);
                    break;
                case GameState.GameOver:
                    Dim(g, 150);
                    Label(g, "FIN DU MATCH", fontMid, Color.White, W / 2, 180);
                    Label(g, banner, fontBig, Color.Gold, W / 2, 250);
                    Label(g, $"{p1.Score}  -  {p2.Score}", fontBig, Color.White, W / 2, 330);
                    Label(g, "Entrée : rejouer     Échap : menu", fontSmall, Color.White, W / 2, 410);
                    break;
            }
        }

        private void DrawStadium(Graphics g)
        {
            // ciel
            using (var sky = new LinearGradientBrush(new RectangleF(0, 0, W, 300), Color.FromArgb(14, 24, 64), Color.FromArgb(70, 125, 195), 90f))
                g.FillRectangle(sky, 0, 0, W, 300);

            // projecteurs
            foreach (float x in new[] { 90f, 910f })
            {
                using var pole = new Pen(Color.FromArgb(90, 90, 100), 6);
                g.DrawLine(pole, x, 60, x, 160);
                using var glow = new SolidBrush(Color.FromArgb(40, 255, 255, 220));
                g.FillEllipse(glow, x - 70, 0, 140, 90);
                g.FillRectangle(Brushes.DimGray, x - 36, 34, 72, 26);
                for (int i = 0; i < 4; i++)
                    g.FillEllipse(Brushes.LightYellow, x - 32 + i * 17, 38, 13, 18);
            }

            // tribunes + public
            var stands = new RectangleF(0, 150, W, 245);
            using (var sb = new LinearGradientBrush(stands, Color.FromArgb(55, 55, 72), Color.FromArgb(28, 28, 38), 90f))
                g.FillRectangle(sb, stands);

            Color[] crowd =
            {
                Color.FromArgb(215, 40, 45), Color.FromArgb(35, 95, 215), Color.White, Color.FromArgb(250, 200, 50),
                Color.FromArgb(241, 194, 150), Color.FromArgb(198, 134, 86), Color.FromArgb(120, 80, 50), Color.FromArgb(60, 170, 90),
            };
            var brushes = Array.ConvertAll(crowd, c => new SolidBrush(c));
            var r = new Random(7);
            int row = 0;
            for (float y = 160; y < 385; y += 13, row++)
            {
                for (float x = row % 2 * 6; x < W; x += 12)
                    g.FillEllipse(brushes[r.Next(brushes.Length)], x, y + r.Next(3), 9, 10);
            }
            foreach (var b in brushes) b.Dispose();

            // panneaux publicitaires
            string[] ads = { "HEAD FOOT", "C# ARENA", ".NET CUP", "BUUUT !", "GDI+ TV" };
            Color[] adColors = { Color.FromArgb(200, 30, 40), Color.FromArgb(25, 70, 170), Color.FromArgb(30, 140, 70), Color.FromArgb(230, 150, 20), Color.FromArgb(110, 40, 160) };
            for (int i = 0; i < 5; i++)
            {
                using var ab = new SolidBrush(adColors[i]);
                g.FillRectangle(ab, i * 200, 392, 200, 32);
                g.DrawString(ads[i], fontSmall, Brushes.White, i * 200 + 100, 408, Center);
            }

            // pelouse rayée
            using (var g1 = new SolidBrush(Color.FromArgb(58, 150, 58)))
            using (var g2 = new SolidBrush(Color.FromArgb(48, 132, 48)))
            {
                for (int i = 0; i * 50 < W; i++)
                    g.FillRectangle(i % 2 == 0 ? g1 : g2, i * 50, 424, 50, H - 424);
            }
            using (var line = new Pen(Color.FromArgb(230, 255, 255, 255), 3))
            {
                g.DrawLine(line, 0, 430, W, 430);
                g.DrawLine(line, W / 2, 430, W / 2, H);
                g.DrawEllipse(line, W / 2 - 110, 505, 220, 50);
            }

            // intérieur des cages
            using (var shade = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
            {
                g.FillRectangle(shade, 0, Field.GoalTop, Field.GoalW, Field.GroundY - Field.GoalTop);
                g.FillRectangle(shade, W - Field.GoalW, Field.GoalTop, Field.GoalW, Field.GroundY - Field.GoalTop);
            }
            using (var post = new Pen(Color.FromArgb(200, 200, 200), 5))
            {
                g.DrawLine(post, 3, Field.GoalTop, 3, Field.GroundY);
                g.DrawLine(post, W - 3, Field.GoalTop, W - 3, Field.GroundY);
            }
        }

        private void DrawGoalFront(Graphics g, bool left)
        {
            var net = new RectangleF(left ? 0 : W - Field.GoalW, Field.GoalTop, Field.GoalW, Field.GroundY - Field.GoalTop);
            g.FillRectangle(netBrush, net);

            RectangleF bar = left ? Field.LeftBar : Field.RightBar;
            g.FillRectangle(Brushes.White, bar);
            g.DrawRectangle(Pens.Gray, bar.X, bar.Y, bar.Width, bar.Height);

            float px = left ? Field.GoalW + 3 : W - Field.GoalW - 8;
            using var postBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
            g.FillRectangle(postBrush, px, Field.GoalTop, 5, Field.GroundY - Field.GoalTop);
        }

        private static void DrawShadow(Graphics g, float x, float bottomY, float width)
        {
            float height = Field.GroundY - bottomY;
            float w = width * Math.Clamp(1f - height / 400f, 0.35f, 1f);
            using var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
            g.FillEllipse(b, x - w / 2, Field.GroundY - 5, w, 10);
        }

        private void DrawBall(Graphics g)
        {
            DrawShadow(g, ball.X, ball.Y + Ball.R, 34);

            GraphicsState st = g.Save();
            g.TranslateTransform(ball.X, ball.Y);
            g.RotateTransform(ball.Angle * 180f / MathF.PI);

            var rect = new RectangleF(-Ball.R, -Ball.R, 2 * Ball.R, 2 * Ball.R);
            g.FillEllipse(Brushes.White, rect);
            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(rect);
                g.SetClip(clip, CombineMode.Intersect);
            }
            using (var spot = new SolidBrush(Color.FromArgb(30, 30, 30)))
            {
                g.FillPolygon(spot, Pentagon(0, 0, 5.5f, 0));
                for (int i = 0; i < 5; i++)
                {
                    float a = i * MathF.Tau / 5 - MathF.PI / 2;
                    g.FillPolygon(spot, Pentagon(MathF.Cos(a) * 14, MathF.Sin(a) * 14, 5.5f, MathF.PI));
                }
            }
            g.Restore(st);

            using var outline = new Pen(Color.FromArgb(60, 60, 60), 1.5f);
            g.DrawEllipse(outline, ball.X - Ball.R, ball.Y - Ball.R, 2 * Ball.R, 2 * Ball.R);
        }

        private static PointF[] Pentagon(float cx, float cy, float r, float rot)
        {
            var pts = new PointF[5];
            for (int i = 0; i < 5; i++)
            {
                float a = rot + i * MathF.Tau / 5 - MathF.PI / 2;
                pts[i] = new PointF(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r);
            }
            return pts;
        }

        private void DrawPlayer(Graphics g, Player p)
        {
            const float R = Player.R;
            int f = p.Facing;
            DrawShadow(g, p.X, p.Y + R + Player.FootH, 70);

            // jambe
            using (var leg = new Pen(p.Team, 8) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(leg, p.X, p.Y + R - 8, p.ShoeX - f * 4, p.ShoeY - 3);

            // chaussure
            GraphicsState st = g.Save();
            g.TranslateTransform(p.ShoeX, p.ShoeY);
            g.RotateTransform(-f * 35f * p.KickSwing);
            g.FillEllipse(Brushes.Black, -15 + f * 3, -9, 30, 18);
            using (var stripe = new Pen(p.Team, 3))
                g.DrawLine(stripe, -6 + f * 3, -5, 4 + f * 3, -5);
            g.FillRectangle(Brushes.WhiteSmoke, -13 + f * 3, 6, 26, 3);
            g.Restore(st);

            // tête
            var head = new RectangleF(p.X - R, p.Y - R, 2 * R, 2 * R);
            using (var skin = new SolidBrush(p.Skin))
                g.FillEllipse(skin, head);

            st = g.Save();
            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(head);
                g.SetClip(clip, CombineMode.Intersect);
            }
            using (var hair = new SolidBrush(p.Hair))
                g.FillEllipse(hair, p.X - R * 1.1f - f * R * 0.35f, p.Y - R * 1.4f, R * 2.2f, R * 1.3f);
            using (var band = new SolidBrush(p.Team))
                g.FillRectangle(band, p.X - R, p.Y - R * 0.42f, 2 * R, 7);
            g.Restore(st);

            // oreille
            using (var ear = new SolidBrush(ControlPaint.Dark(p.Skin, 0.05f)))
                g.FillEllipse(ear, p.X - f * R * 0.15f - 6, p.Y - 4, 12, 16);

            // œil qui suit la balle
            float ex = p.X + f * R * 0.42f, ey = p.Y - R * 0.02f;
            g.FillEllipse(Brushes.White, ex - 9, ey - 10, 18, 20);
            float ldx = ball.X - ex, ldy = ball.Y - ey;
            float len = MathF.Max(1f, MathF.Sqrt(ldx * ldx + ldy * ldy));
            float pxp = ex + ldx / len * 4.5f, pyp = ey + ldy / len * 5f;
            g.FillEllipse(Brushes.Black, pxp - 4.5f, pyp - 4.5f, 9, 9);
            using (var brow = new Pen(p.Hair, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(brow, ex - 9, ey - 15 + (p.KickTimer > 0 ? 3 : 0), ex + 9, ey - 13);

            // nez
            using (var nose = new SolidBrush(ControlPaint.Dark(p.Skin, 0.1f)))
                g.FillEllipse(nose, p.X + f * R * 0.88f - 6, p.Y + 2, 12, 10);

            // bouche
            float mx = p.X + f * R * 0.5f, my = p.Y + R * 0.5f;
            if (p.KickTimer > 0 || state == GameState.Goal)
                g.FillEllipse(Brushes.DarkRed, mx - 7, my - 5, 14, 11);
            else
                using (var mouth = new Pen(Color.FromArgb(120, 40, 30), 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawArc(mouth, mx - 9, my - 8, 18, 12, 20, 140);

            using (var outline = new Pen(ControlPaint.Dark(p.Skin, 0.4f), 2))
                g.DrawEllipse(outline, head);
        }

        private void DrawHud(Graphics g)
        {
            var box = new RectangleF(W / 2 - 260, 8, 520, 56);
            using (var path = RoundRect(box, 14))
            using (var bg = new SolidBrush(Color.FromArgb(210, 15, 15, 25)))
                g.FillPath(bg, path);

            using (var c1 = new SolidBrush(p1.Team))
                g.FillRectangle(c1, box.X + 14, 26, 10, 20);
            using (var c2 = new SolidBrush(p2.Team))
                g.FillRectangle(c2, box.Right - 24, 26, 10, 20);

            g.DrawString(p1.Name, fontSmall, Brushes.White, W / 2 - 180, 36, Center);
            g.DrawString(p2.Name, fontSmall, Brushes.White, W / 2 + 180, 36, Center);
            g.DrawString(p1.Score.ToString(), fontScore, Brushes.White, W / 2 - 80, 36, Center);
            g.DrawString(p2.Score.ToString(), fontScore, Brushes.White, W / 2 + 80, 36, Center);

            int secs = Math.Max(0, (framesLeft + 59) / 60);
            Brush timeBrush = secs <= 10 && state == GameState.Playing ? Brushes.OrangeRed : Brushes.Gold;
            g.DrawString($"{secs / 60}:{secs % 60:00}", fontMid, timeBrush, W / 2, 36, Center);
        }

        private void DrawMenu(Graphics g)
        {
            Dim(g, 165);
            Label(g, "HEAD FOOT", fontTitle, Color.Gold, W / 2, 110);
            Label(g, "1  —  Joueur vs CPU", fontMid, Color.White, W / 2, 225);
            Label(g, "2  —  Joueur vs Joueur", fontMid, Color.White, W / 2, 275);
            Label(g, $"Difficulté CPU :   ←  {difficulty}  →", fontSmall, Color.LightSkyBlue, W / 2, 330);

            Label(g, "Joueur 1 :   Q/A  gauche  ·  D  droite  ·  Z/W  sauter  ·  Espace  tirer", fontTiny, Color.White, W / 2, 400);
            Label(g, "Joueur 2 :   ← →  déplacement  ·  ↑  sauter  ·  ↓ / Entrée  tirer", fontTiny, Color.White, W / 2, 430);
            Label(g, "Tir au sol = lob  ·  Tir en l'air = volée tendue  ·  P = pause  ·  Échap = quitter", fontTiny, Color.Silver, W / 2, 470);
            Label(g, $"Match de {MatchSeconds} secondes", fontTiny, Color.Silver, W / 2, 500);
        }

        private static void Dim(Graphics g, int alpha)
        {
            using var b = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0));
            g.FillRectangle(b, 0, 0, W, H);
        }

        private static void Label(Graphics g, string s, Font font, Color color, float x, float y)
        {
            using var shadow = new SolidBrush(Color.FromArgb(170, 0, 0, 0));
            g.DrawString(s, font, shadow, x + 2, y + 3, Center);
            using var b = new SolidBrush(color);
            g.DrawString(s, font, b, x, y, Center);
        }

        private static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                timer.Dispose();
                background?.Dispose();
                fontTitle.Dispose();
                fontBig.Dispose();
                fontMid.Dispose();
                fontSmall.Dispose();
                fontTiny.Dispose();
                fontScore.Dispose();
                netBrush.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

