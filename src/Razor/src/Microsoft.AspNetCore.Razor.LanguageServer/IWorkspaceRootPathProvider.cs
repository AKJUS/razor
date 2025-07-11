﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal interface IWorkspaceRootPathProvider
{
    Task<string> GetRootPathAsync(CancellationToken cancellationToken);
}
