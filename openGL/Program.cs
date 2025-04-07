using openGL;
class Program
{
    static void Main(string[] args)
    {
        using (Game game = new Game(800,400))
        {
            game.Run();
        }
    }
}

