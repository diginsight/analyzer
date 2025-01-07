using Diginsight.Analyzer.API.Attributes;
using Diginsight.Analyzer.Common;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace Diginsight.Analyzer.API.Mvc;

internal sealed class FlavorAwareControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (TypeInfo controllerType in feature.Controllers.ToArray())
        {
            if (controllerType.GetCustomAttribute<FlavorAttribute>() is not { Flavor: var flavor })
            {
                continue;
            }

            bool isAgent = CommonUtils.IsAgent;
            if ((isAgent && flavor == Flavor.OrchestratorOnly) || (!isAgent && flavor == Flavor.AgentOnly))
            {
                feature.Controllers.Remove(controllerType);
            }
        }
    }
}
