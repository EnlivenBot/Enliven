using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using Serilog.Parsing;

#pragma warning disable CA2254

namespace Bot.Infrastructure.Tracing;

public static class ActivitySourceExtensions
{
    private static readonly MessageTemplateParser MessageTemplateParser = new();
    private static readonly Dictionary<string, string[]> ParsedCache = new();

    public static Activity? StartActivityStructured(this ActivitySource source, ActivityKind activityKind,
        [StructuredMessageTemplate] string message, params Span<object?> args)
    {
        return StartActivityStructured(source, activityKind, default, message, args);
    }

    public static Activity? StartActivityStructured(this ActivitySource source, ActivityKind activityKind,
        ActivityContext activityContext, [StructuredMessageTemplate] string message, params Span<object?> args)
    {
        if (!ParsedCache.TryGetValue(message, out var parsedArgs))
        {
            var template = MessageTemplateParser.Parse(message);
            parsedArgs = template.Tokens
                .OfType<PropertyToken>()
                .Select(token => token.PropertyName)
                .ToArray();

            ParsedCache[message] = parsedArgs;
        }

        if (parsedArgs.Length != args.Length)
        {
            throw new Exception("Invalid number of arguments");
        }

        var activity = source.StartActivity(message, activityKind, activityContext);
        if (activity is null || args.Length <= 0) return activity;

        for (var i = 0; i < parsedArgs.Length; i++)
        {
            var argName = parsedArgs[i];
            activity?.SetTag(argName, args[i]);
        }

        return activity;
    }
}