﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorEngine
{
    public ImmutableArray<IRazorEngineFeature> Features { get; }
    public ImmutableArray<IRazorEnginePhase> Phases { get; }

    private readonly FeatureCache<IRazorEngineFeature> _featureCache;

    internal RazorEngine(ImmutableArray<IRazorEngineFeature> features, ImmutableArray<IRazorEnginePhase> phases)
    {
        Features = features;
        Phases = phases;

        _featureCache = new(features);

        foreach (var feature in features)
        {
            feature.Initialize(this);
        }

        foreach (var phase in phases)
        {
            phase.Initialize(this);
        }
    }

    public void Process(RazorCodeDocument codeDocument)
    {
        ArgHelper.ThrowIfNull(codeDocument);

        foreach (var phase in Phases)
        {
            phase.Execute(codeDocument);
        }
    }

    public ImmutableArray<TFeature> GetFeatures<TFeature>()
        where TFeature : class, IRazorEngineFeature
        => _featureCache.GetFeatures<TFeature>();

    public bool TryGetFeature<TFeature>([NotNullWhen(true)] out TFeature? result)
        where TFeature : class, IRazorEngineFeature
    {
        if (GetFeatures<TFeature>() is [var feature, ..])
        {
            result = feature;
            return true;
        }

        result = null;
        return false;
    }
}
