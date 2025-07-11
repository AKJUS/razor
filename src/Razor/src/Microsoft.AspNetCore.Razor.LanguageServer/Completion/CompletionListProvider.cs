﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion;

internal class CompletionListProvider(
    RazorCompletionListProvider razorCompletionListProvider,
    DelegatedCompletionListProvider delegatedCompletionListProvider,
    CompletionTriggerAndCommitCharacters triggerAndCommitCharacters)
{
    private readonly RazorCompletionListProvider _razorCompletionListProvider = razorCompletionListProvider;
    private readonly DelegatedCompletionListProvider _delegatedCompletionListProvider = delegatedCompletionListProvider;
    private readonly CompletionTriggerAndCommitCharacters _triggerAndCommitCharacters = triggerAndCommitCharacters;

    public ValueTask<RazorVSInternalCompletionList?> GetCompletionListAsync(
        int absoluteIndex,
        VSInternalCompletionContext completionContext,
        DocumentContext documentContext,
        VSInternalClientCapabilities clientCapabilities,
        RazorCompletionOptions razorCompletionOptions,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var isDelegationTrigger = _triggerAndCommitCharacters.IsValidDelegationTrigger(completionContext);
        var isRazorTrigger = _triggerAndCommitCharacters.IsValidRazorTrigger(completionContext);

        // We don't have a valid trigger, so we can't provide completions
        return isDelegationTrigger || isRazorTrigger
            ? new(GetCompletionListCoreAsync(
                absoluteIndex,
                completionContext,
                documentContext,
                clientCapabilities,
                razorCompletionOptions,
                correlationId,
                isDelegationTrigger,
                isRazorTrigger,
                cancellationToken))
            : default;
    }

    private async Task<RazorVSInternalCompletionList?> GetCompletionListCoreAsync(
        int absoluteIndex,
        VSInternalCompletionContext completionContext,
        DocumentContext documentContext,
        VSInternalClientCapabilities clientCapabilities,
        RazorCompletionOptions razorCompletionOptions,
        Guid correlationId,
        bool isDelegationTrigger,
        bool isRazorTrigger,
        CancellationToken cancellationToken)
    {
        Debug.Assert(isDelegationTrigger || isRazorTrigger);

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

        // First we delegate to get completion items from the individual language server
        RazorVSInternalCompletionList? delegatedCompletionList = null;
        HashSet<string>? existingItems = null;

        if (isDelegationTrigger)
        {
            delegatedCompletionList = await _delegatedCompletionListProvider
                .GetCompletionListAsync(
                    codeDocument,
                    absoluteIndex,
                    completionContext,
                    documentContext,
                    clientCapabilities,
                    razorCompletionOptions,
                    correlationId,
                    cancellationToken)
                .ConfigureAwait(false);

            // Extract the items we got back from the delegated server, to inform tag helper completion
            if (delegatedCompletionList?.Items is { } delegatedItems)
            {
                existingItems = [.. delegatedItems.Select(static i => i.Label)];
            }
        }

        // Now we get the Razor completion list, using information from the actual language server if necessary
        RazorVSInternalCompletionList? razorCompletionList = null;

        if (isRazorTrigger)
        {
            razorCompletionList = _razorCompletionListProvider.GetCompletionList(
                codeDocument,
                absoluteIndex,
                completionContext,
                clientCapabilities,
                existingItems,
                razorCompletionOptions);
        }

        return CompletionListMerger.Merge(razorCompletionList, delegatedCompletionList);
    }
}
