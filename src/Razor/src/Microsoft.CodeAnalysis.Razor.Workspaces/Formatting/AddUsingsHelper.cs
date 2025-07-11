﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Razor.Extensions;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Extensions;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using RazorSyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.CodeAnalysis.Razor.Formatting;

internal static class AddUsingsHelper
{
    private static readonly Regex s_addUsingVSCodeAction = new Regex("@?using ([^;]+);?$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private readonly record struct RazorUsingDirective(RazorDirectiveSyntax Node, AddImportChunkGenerator Statement);

    public static async Task<TextEdit[]> GetUsingStatementEditsAsync(RazorCodeDocument codeDocument, SourceText changedCSharpText, CancellationToken cancellationToken)
    {
        // Now that we're done with everything, lets see if there are any using statements to fix up
        // We do this by comparing the original generated C# code, and the changed C# code, and look for a difference
        // in using statements. We can't use edits for this for two main reasons:
        //
        // 1. Using statements in the generated code might come from _Imports.razor, or from this file, and C# will shove them anywhere
        // 2. The edit might not be clean. eg given:
        //      using System;
        //      using System.Text;
        //    Adding "using System.Linq;" could result in an insert of "Linq;\r\nusing System." on line 2
        //
        // So because of the above, we look for a difference in C# using directive nodes directly from the C# syntax tree, and apply them manually
        // to the Razor document.

        var originalCSharpSyntaxTree = codeDocument.GetOrParseCSharpSyntaxTree(cancellationToken);
        var changedCSharpSyntaxTree = originalCSharpSyntaxTree.WithChangedText(changedCSharpText);
        var oldUsings = await FindUsingDirectiveStringsAsync(originalCSharpSyntaxTree, cancellationToken).ConfigureAwait(false);
        var newUsings = await FindUsingDirectiveStringsAsync(changedCSharpSyntaxTree, cancellationToken).ConfigureAwait(false);

        using var edits = new PooledArrayBuilder<TextEdit>();
        foreach (var usingStatement in newUsings.Except(oldUsings))
        {
            edits.Add(CreateAddUsingTextEdit(usingStatement, codeDocument));
        }

        return edits.ToArray();
    }

    /// <summary>
    /// Extracts the namespace from a C# add using statement provided by Visual Studio
    /// </summary>
    /// <param name="csharpAddUsing">Add using statement of the form `using System.X;`</param>
    /// <param name="namespace">Extract namespace `System.X`</param>
    /// <param name="prefix">The prefix to show, before the namespace, if any</param>
    /// <returns></returns>
    public static bool TryExtractNamespace(string csharpAddUsing, out string @namespace, out string prefix)
    {
        // We must remove any leading/trailing new lines from the add using edit
        csharpAddUsing = csharpAddUsing.Trim();
        var regexMatchedTextEdit = s_addUsingVSCodeAction.Match(csharpAddUsing);
        if (!regexMatchedTextEdit.Success ||

            // Two Regex matching groups are expected
            // 1. `using namespace;`
            // 2. `namespace`
            regexMatchedTextEdit.Groups.Count != 2)
        {
            // Text edit in an unexpected format
            @namespace = string.Empty;
            prefix = string.Empty;
            return false;
        }

        @namespace = regexMatchedTextEdit.Groups[1].Value;
        prefix = csharpAddUsing[..regexMatchedTextEdit.Index];
        return true;
    }

    public static TextEdit CreateAddUsingTextEdit(string @namespace, RazorCodeDocument codeDocument)
    {
        /* The heuristic is as follows:
         *
         * - If no @using, @namespace, or @page directives are present, insert the statements at the top of the
         *   file in alphabetical order.
         * - If a @namespace or @page are present, the statements are inserted after the last line-wise in
         *   alphabetical order.
         * - If @using directives are present and alphabetized with System directives at the top, the statements
         *   will be placed in the correct locations according to that ordering.
         * - Otherwise it's kind of undefined; it's only geared to insert based on alphabetization.
         *
         * This is generally sufficient for our current situation (inserting a single @using statement to include a
         * component), however it has holes if we eventually use it for other purposes. If we want to deal with
         * that now I can come up with a more sophisticated heuristic (something along the lines of checking if
         * there's already an ordering, etc.).
         */

        using var usingDirectives = new PooledArrayBuilder<RazorUsingDirective>();
        CollectUsingDirectives(codeDocument, ref usingDirectives.AsRef());
        if (usingDirectives.Count > 0)
        {
            return GetInsertUsingTextEdit(codeDocument, @namespace, in usingDirectives);
        }

        return GetInsertUsingTextEdit(codeDocument, @namespace);
    }

    public static async Task<ImmutableArray<string>> FindUsingDirectiveStringsAsync(SyntaxTree syntaxTree, CancellationToken cancellationToken)
    {
        var syntaxRoot = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);
        var sourceText = await syntaxTree.GetTextAsync(cancellationToken).ConfigureAwait(false);

        return syntaxRoot
            .DescendantNodes(static n => n is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax)
            .OfType<UsingDirectiveSyntax>()
            .Where(static u => u.Name is not null) // If the Name is null then this isn't a using directive, it's probably an alias for a tuple type
            .SelectAsArray(u => GetNamespaceFromDirective(u, sourceText));

        static string GetNamespaceFromDirective(UsingDirectiveSyntax usingDirectiveSyntax, SourceText sourceText)
        {
            var nameSyntax = usingDirectiveSyntax.Name.AssumeNotNull();

            var end = nameSyntax.Span.End;

            // FullSpan to get the end of the trivia before the next
            // token. Testing shows that the trailing whitespace is always given
            // as trivia to the using keyword.
            var start = usingDirectiveSyntax.UsingKeyword.FullSpan.End;

            return sourceText.GetSubTextString(TextSpan.FromBounds(start, end));
        }
    }

    /// <summary>
    /// Generates a <see cref="TextEdit"/> to insert a new using directive into the Razor code document, at the right spot among existing using directives.
    /// </summary>
    private static TextEdit GetInsertUsingTextEdit(
        RazorCodeDocument codeDocument,
        string newUsingNamespace,
        ref readonly PooledArrayBuilder<RazorUsingDirective> existingUsingDirectives)
    {
        Debug.Assert(existingUsingDirectives.Count > 0);

        var newText = $"@using {newUsingNamespace}{Environment.NewLine}";

        foreach (var usingDirective in existingUsingDirectives)
        {
            // Skip System directives; if they're at the top we don't want to insert before them
            var usingDirectiveNamespace = usingDirective.Statement.ParsedNamespace;
            if (usingDirectiveNamespace.StartsWith("System", StringComparison.Ordinal))
            {
                continue;
            }

            if (string.CompareOrdinal(newUsingNamespace, usingDirectiveNamespace) < 0)
            {
                var usingDirectiveLineIndex = codeDocument.Source.Text.GetLinePosition(usingDirective.Node.Span.Start).Line;
                return LspFactory.CreateTextEdit(line: usingDirectiveLineIndex, character: 0, newText);
            }
        }

        // If we haven't actually found a place to insert the using directive, do so at the end
        var endIndex = existingUsingDirectives[^1].Node.Span.End;
        var lineIndex = GetLineIndexOrEnd(codeDocument, endIndex - 1) + 1;
        return LspFactory.CreateTextEdit(line: lineIndex, character: 0, newText);
    }

    /// <summary>
    /// Generates a <see cref="TextEdit"/> to insert a new using directive into the Razor code document, at the top of the file.
    /// </summary>
    private static TextEdit GetInsertUsingTextEdit(
        RazorCodeDocument codeDocument,
        string newUsingNamespace)
    {
        var insertPosition = (0, 0);

        // If we don't have usings, insert after the last namespace or page directive, which ever comes later
        var root = codeDocument.GetRequiredSyntaxRoot();
        var lastNamespaceOrPageDirective = root
            .DescendantNodes()
            .LastOrDefault(IsNamespaceOrPageDirective);

        if (lastNamespaceOrPageDirective != null)
        {
            var lineIndex = GetLineIndexOrEnd(codeDocument, lastNamespaceOrPageDirective.Span.End - 1) + 1;
            insertPosition = (lineIndex, 0);
        }

        return LspFactory.CreateTextEdit(insertPosition, newText: $"@using {newUsingNamespace}{Environment.NewLine}");
    }

    private static int GetLineIndexOrEnd(RazorCodeDocument codeDocument, int endIndex)
    {
        if (endIndex < codeDocument.Source.Text.Length)
        {
            return codeDocument.Source.Text.GetLinePosition(endIndex).Line;
        }
        else
        {
            return codeDocument.Source.Text.Lines.Count;
        }
    }

    private static void CollectUsingDirectives(RazorCodeDocument codeDocument, ref PooledArrayBuilder<RazorUsingDirective> directives)
    {
        var root = codeDocument.GetRequiredSyntaxRoot();
        foreach (var node in root.DescendantNodes())
        {
            if (node is RazorDirectiveSyntax directiveNode)
            {
                foreach (var child in directiveNode.DescendantNodes())
                {
                    if (child.GetChunkGenerator() is AddImportChunkGenerator { IsStatic: false } usingStatement)
                    {
                        directives.Add(new RazorUsingDirective(directiveNode, usingStatement));
                    }
                }
            }
        }
    }

    private static bool IsNamespaceOrPageDirective(RazorSyntaxNode node)
    {
        if (node is RazorDirectiveSyntax directiveNode)
        {
            return directiveNode.DirectiveDescriptor == ComponentPageDirective.Directive ||
                directiveNode.DirectiveDescriptor == NamespaceDirective.Directive ||
                directiveNode.DirectiveDescriptor == PageDirective.Directive;
        }

        return false;
    }
}
