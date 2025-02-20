using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Permissions;

public sealed class SpecificPermissionAssignmentQO
{
    [JsonProperty("principalId")]
    public Guid? PrincipalId => throw new NotSupportedException();

    [JsonProperty("permission")]
    public string Permission => throw new NotSupportedException();
}
