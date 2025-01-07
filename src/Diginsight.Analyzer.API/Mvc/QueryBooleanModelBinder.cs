using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Diginsight.Analyzer.API.Mvc;

internal sealed class QueryBooleanModelBinderProvider : IModelBinderProvider
{
    public static readonly IModelBinderProvider Instance = new QueryBooleanModelBinderProvider();

    private QueryBooleanModelBinderProvider() { }

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        return context.Metadata.ModelType == typeof(bool) &&
            context.BindingInfo.BindingSource?.CanAcceptDataFrom(BindingSource.Query) == true
                ? ModelBinder.Instance : null;
    }

    private sealed class ModelBinder : IModelBinder
    {
        public static readonly IModelBinder Instance = new ModelBinder();

        private ModelBinder() { }

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            string modelName = bindingContext.ModelName;

            ValueProviderResult result = bindingContext.ValueProvider.GetValue(modelName);
            if (result == ValueProviderResult.None)
            {
                bindingContext.Result = ModelBindingResult.Success(false);
            }
            else
            {
                bindingContext.ModelState.SetModelValue(modelName, result);
                string? rawValue = result.FirstValue;
                if (string.IsNullOrEmpty(rawValue))
                {
                    bindingContext.Result = ModelBindingResult.Success(true);
                }
                else if (bool.TryParse(rawValue, out bool boolValue))
                {
                    bindingContext.Result = ModelBindingResult.Success(boolValue);
                }
                else
                {
                    bindingContext.ModelState.TryAddModelError(modelName, "Value must be false, true, or empty");
                }
            }

            return Task.CompletedTask;
        }
    }
}
