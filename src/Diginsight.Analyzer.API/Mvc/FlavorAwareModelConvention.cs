using Diginsight.Analyzer.API.Attributes;
using Diginsight.Analyzer.Common;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Diginsight.Analyzer.API.Mvc;

internal sealed class FlavorAwareModelConvention : IActionModelConvention, IParameterModelConvention
{
    public static readonly FlavorAwareModelConvention Instance = new ();

    private FlavorAwareModelConvention() { }

    public void Apply(ActionModel action)
    {
        if (action.Attributes.OfType<FlavorAttribute>().FirstOrDefault() is not { Flavor: var flavor })
        {
            return;
        }

        bool isAgent = CommonUtils.IsAgent;
        if ((isAgent && flavor == Flavor.OrchestratorOnly) || (!isAgent && flavor == Flavor.AgentOnly))
        {
            action.Controller.Actions.Remove(action);
        }
    }

    public void Apply(ParameterModel parameter)
    {
        if (parameter.Attributes.OfType<FlavorAttribute>().FirstOrDefault() is not { Flavor: var flavor })
        {
            return;
        }

        bool isAgent = CommonUtils.IsAgent;
        if ((isAgent && flavor == Flavor.OrchestratorOnly) || (!isAgent && flavor == Flavor.AgentOnly))
        {
            parameter.Action.Parameters.Remove(parameter);
        }
    }
}
