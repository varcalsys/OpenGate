using System.Text;

namespace OpenGate.Migration;

public static class OpenGateMigrationProgram
{
    public static Task<int> Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;
        return MigrationCli.RunAsync(args, Console.Out, Console.Error, CancellationToken.None);
    }
}