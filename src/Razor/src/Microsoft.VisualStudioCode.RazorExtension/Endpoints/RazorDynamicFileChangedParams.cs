﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Microsoft.VisualStudioCode.RazorExtension.Endpoints;

internal class RazorDynamicFileChangedParams
{
    [JsonPropertyName("razorDocument")]
    public required TextDocumentIdentifier RazorDocument { get; set; }
}
