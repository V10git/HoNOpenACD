using UniCheat;

namespace ExampleScript;

public class ExampleScript : BaseScript
{
    public override bool Check(Func<string, bool>? waiter = null)
    {
        return true;
    }

    protected override bool Inject()
    {
        Console.WriteLine("Hello World! [ from ExampleScript ]");
        return true;
    }
}
