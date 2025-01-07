﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Entities;

[JsonConverter(typeof(StringEnumConverter))]
public enum ExecutionKind
{
    Analysis,
}