namespace HeadArena;
static class GameTests {
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
    public static void Run() {
        var g = new Game();
        g.Handle(new Input("start", "solo", "normal", false,false,false,false,false,false,false,false));
        g.Freeze = 0;
        g.Ball.X = 12; g.Ball.Y = 470; g.Step(1.0/60);
        Check(g.Score2 == 1 && g.Score1 == 0, "But dans la cage gauche");
        g.Freeze = 0; g.Ball.X = 1088; g.Ball.Y = 470; g.Step(1.0/60);
        Check(g.Score1 == 1, "But dans la cage droite");
        g.Freeze = 0; g.Ball.X = 12; g.Ball.Y = 300; g.Step(1.0/60);
        Check(g.Score2 == 1 && g.Ball.X >= 18, "Pas de but au-dessus de la cage");
        g.Phase = "paused"; double time = g.Remaining; g.Step(1);
        Check(g.Remaining == time, "Pause du chronometre");
        g.Phase = "playing"; g.Remaining = .001; g.Step(1.0/60);
        Check(g.Phase == "finished", "Fin du match");
        g.Handle(new Input("start", "versus", "normal", false,false,true,false,false,false,false,false));g.Freeze=0;g.Step(1.0/60);
        Check(g.P1.Vy < 0, "Saut du joueur");
        for(int i=0;i<5400;i++)g.Step(1.0/60);
        Check(double.IsFinite(g.Ball.X) && double.IsFinite(g.Ball.Y) && g.Ball.Y <= Game.Floor, "Simulation stable sur un match");
        Console.WriteLine("7 tests reussis.");
    }
}
