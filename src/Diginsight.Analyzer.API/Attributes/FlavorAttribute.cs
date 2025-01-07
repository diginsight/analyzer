namespace Diginsight.Analyzer.API.Attributes;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class FlavorAttribute : Attribute
{
    public Flavor Flavor { get; }

    public FlavorAttribute(Flavor flavor)
    {
        Flavor = flavor;
    }
}
