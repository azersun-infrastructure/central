namespace OpsCentral.Options;

/// <summary>ActionType (e.g. "Unlock") -> DispatchTarget name ("Jenkins"/"AzureAutomation"), config-bound from the "AdActionRouting" section.</summary>
public class AdActionRoutingOptions : Dictionary<string, string>
{
    public const string SectionName = "AdActionRouting";
}
