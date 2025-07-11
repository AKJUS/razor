﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Editor;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.DocumentMapping;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.Text;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.DocumentMapping;

public class LSPDocumentMappingProviderTest : ToolingTestBase
{
    private static readonly Uri s_razorFile = new("file:///some/folder/to/file.razor");
    private static readonly Uri s_razorVirtualCSharpFile = new("file:///some/folder/to/file.razor.ide.g.cs");
    private static readonly Uri s_anotherRazorFile = new("file:///some/folder/to/anotherfile.razor");

    private readonly Lazy<LSPDocumentManager> _documentManager;

    public LSPDocumentMappingProviderTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        var csharpVirtualDocumentSnapshot = new CSharpVirtualDocumentSnapshot(projectKey: default, s_razorVirtualCSharpFile, new StringTextSnapshot(string.Empty), hostDocumentSyncVersion: 0);
        var documentSnapshot1 = new TestLSPDocumentSnapshot(s_razorFile, version: 1, "first doc", csharpVirtualDocumentSnapshot);
        var documentSnapshot2 = new TestLSPDocumentSnapshot(s_anotherRazorFile, version: 5, "second doc", csharpVirtualDocumentSnapshot);
        var documentManager = new TestDocumentManager();
        documentManager.AddDocument(s_razorFile, documentSnapshot1);
        documentManager.AddDocument(s_anotherRazorFile, documentSnapshot2);
        _documentManager = new Lazy<LSPDocumentManager>(() => documentManager);
    }

    [Fact]
    public async Task RazorMapToDocumentRangeAsync_InvokesLanguageServer()
    {
        // Arrange
        var uri = new Uri("file:///some/folder/to/file.razor");

        var response = new RazorMapToDocumentRangesResponse()
        {
            Ranges = [LspFactory.CreateRange(1, 1, 3, 3)],
            HostDocumentVersion = 1,
            Spans = [new() { Start = 1, Length = 2 }],
        };
        var requestInvoker = new Mock<LSPRequestInvoker>(MockBehavior.Strict);
        requestInvoker
            .Setup(r => r.ReinvokeRequestOnServerAsync<RazorMapToDocumentRangesParams, RazorMapToDocumentRangesResponse>(
                It.IsAny<ITextBuffer>(),
                LanguageServerConstants.RazorMapToDocumentRangesEndpoint,
                RazorLSPConstants.RazorLanguageServerName,
                It.IsAny<RazorMapToDocumentRangesParams>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReinvocationResponse<RazorMapToDocumentRangesResponse>("TestLanguageClient", response));

        var mappingProvider = new LSPDocumentMappingProvider(requestInvoker.Object, _documentManager);
        var projectedRange = LspFactory.CreateRange(10, 10, 15, 15);

        // Act
        var result = await mappingProvider.MapToDocumentRangesAsync(RazorLanguageKind.CSharp, uri, new[] { projectedRange }, DisposalToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.HostDocumentVersion);
        var actualRange = result.Ranges[0];
        Assert.Equal(LspFactory.CreatePosition(1, 1), actualRange.Start);
        Assert.Equal(LspFactory.CreatePosition(3, 3), actualRange.End);
    }
}
