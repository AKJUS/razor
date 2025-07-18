﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.Language;

public static class TestTagHelperDescriptorBuilderExtensions
{
    public static TagHelperDescriptorBuilder Metadata(this TagHelperDescriptorBuilder builder, string key, string value)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.Metadata(new KeyValuePair<string, string>(key, value));
    }

    public static TagHelperDescriptorBuilder Metadata(this TagHelperDescriptorBuilder builder, params KeyValuePair<string, string>[] pairs)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        // We need to be sure to add TagHelperMetadata.Runtime.Name if it doesn't already exist.
        if (Array.Exists(pairs, static pair => pair.Key == TagHelperMetadata.Runtime.Name))
        {
            builder.SetMetadata(pairs);
        }
        else
        {
            var newPairs = new KeyValuePair<string, string>[pairs.Length + 1];
            newPairs[0] = RuntimeName(TagHelperConventions.DefaultKind);
            Array.Copy(pairs, 0, newPairs, 1, pairs.Length);

            builder.SetMetadata(newPairs);
        }

        return builder;
    }

    public static TagHelperDescriptorBuilder DisplayName(this TagHelperDescriptorBuilder builder, string displayName)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.DisplayName = displayName;

        return builder;
    }

    public static TagHelperDescriptorBuilder AllowChildTag(this TagHelperDescriptorBuilder builder, string allowedChild)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AllowChildTag(childTagBuilder => childTagBuilder.Name = allowedChild);

        return builder;
    }

    public static TagHelperDescriptorBuilder TagOutputHint(this TagHelperDescriptorBuilder builder, string hint)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.TagOutputHint = hint;

        return builder;
    }

    public static TagHelperDescriptorBuilder SetCaseSensitive(this TagHelperDescriptorBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.CaseSensitive = true;

        return builder;
    }

    public static TagHelperDescriptorBuilder Documentation(this TagHelperDescriptorBuilder builder, string documentation)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.SetDocumentation(documentation);

        return builder;
    }

    public static TagHelperDescriptorBuilder AddDiagnostic(this TagHelperDescriptorBuilder builder, RazorDiagnostic diagnostic)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Diagnostics.Add(diagnostic);

        return builder;
    }

    public static TagHelperDescriptorBuilder BoundAttributeDescriptor(
        this TagHelperDescriptorBuilder builder,
        Action<BoundAttributeDescriptorBuilder> configure)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.BindAttribute(configure);

        return builder;
    }

    public static TagHelperDescriptorBuilder TagMatchingRuleDescriptor(
        this TagHelperDescriptorBuilder builder,
        Action<TagMatchingRuleDescriptorBuilder> configure)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.TagMatchingRule(configure);

        return builder;
    }

#nullable enable

    public static TagHelperDescriptorBuilder AllowedChildTag(
        this TagHelperDescriptorBuilder builder,
        string tagName,
        Action<AllowedChildTagDescriptorBuilder>? configure = null)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.AllowChildTag(childTag =>
        {
            childTag.Name = tagName;

            configure?.Invoke(childTag);
        });

        return builder;
    }

    public static TagHelperDescriptorBuilder BoundAttribute<T>(
        this TagHelperDescriptorBuilder builder,
        string name,
        string propertyName,
        Action<BoundAttributeDescriptorBuilder>? configure = null)
        where T : notnull
        => BoundAttribute(builder, name, propertyName, typeof(T), configure);

    public static TagHelperDescriptorBuilder BoundAttribute(
        this TagHelperDescriptorBuilder builder,
        string name,
        string propertyName,
        Type type,
        Action<BoundAttributeDescriptorBuilder>? configure = null)
        => BoundAttribute(builder, name, propertyName, type.FullName!, configure);

    public static TagHelperDescriptorBuilder BoundAttribute(
        this TagHelperDescriptorBuilder builder,
        string name,
        string propertyName,
        string typeName,
        Action<BoundAttributeDescriptorBuilder>? configure = null)
    {
        builder.BoundAttributeDescriptor(attribute =>
         {
             attribute.Name(name);
             attribute.Metadata(PropertyName(propertyName));
             attribute.TypeName(typeName);

             configure?.Invoke(attribute);
         });

        return builder;
    }

    public static TagHelperDescriptorBuilder TagMatchingRule(
        this TagHelperDescriptorBuilder builder,
        Action<TagMatchingRuleDescriptorBuilder> configure)
        => builder.TagMatchingRule(tagName: null, parentTagName: null, tagStructure: TagStructure.Unspecified, configure);

    public static TagHelperDescriptorBuilder TagMatchingRule(
        this TagHelperDescriptorBuilder builder,
        string tagName,
        Action<TagMatchingRuleDescriptorBuilder> configure)
        => builder.TagMatchingRule(tagName, parentTagName: null, tagStructure: TagStructure.Unspecified, configure);

    public static TagHelperDescriptorBuilder TagMatchingRule(
        this TagHelperDescriptorBuilder builder,
        string tagName,
        string parentTagName,
        Action<TagMatchingRuleDescriptorBuilder> configure)
        => builder.TagMatchingRule(tagName, parentTagName, tagStructure: TagStructure.Unspecified, configure);

    public static TagHelperDescriptorBuilder TagMatchingRule(
        this TagHelperDescriptorBuilder builder,
        string tagName,
        TagStructure tagStructure,
        Action<TagMatchingRuleDescriptorBuilder> configure)
        => builder.TagMatchingRule(tagName, parentTagName: null, tagStructure, configure);

    public static TagHelperDescriptorBuilder TagMatchingRule(
        this TagHelperDescriptorBuilder builder,
        string? tagName = null,
        string? parentTagName = null,
        TagStructure tagStructure = TagStructure.Unspecified,
        Action<TagMatchingRuleDescriptorBuilder>? configure = null)
    {
        builder.TagMatchingRule(rule =>
        {
            rule.TagName = tagName;
            rule.ParentTag = parentTagName;
            rule.TagStructure = tagStructure;

            configure?.Invoke(rule);
        });

        return builder;
    }
}
