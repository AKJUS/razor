﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.CodeAnalysis.Razor.Formatting.New;

internal partial class CSharpFormattingPass
{
    /// <summary>
    /// Represents the state of a line in the generated C# document.
    /// </summary>
    /// <param name="ProcessIndentation">Whether the formatted document text to the left the first non-whitespace character should be applied to the origin document</param>
    /// <param name="ProcessFormatting">Whether the formatted document text to the right of the first non-whitespace character should be applied to the origin document</param>
    /// <param name="CheckForNewLines">Whether the origin document text could have overflowed to multiple lines in the formatted document</param>
    /// <param name="SkipPreviousLine">Whether to skip the previous line in the formatted document, since it doesn't represent anything in the origin document</param>
    /// <param name="SkipNextLine">Whether to skip the next line in the formatted document, since it doesn't represent anything in the origin document</param>
    /// <param name="SkipNextLineIfBrace">Whether to skip the next line in the formatted document, like <see cref="SkipNextLine" />, but only skips if the next line is a brace</param>
    /// <param name="HtmlIndentLevel">The indent level that the Html formatter applied to this line</param>
    /// <param name="OriginOffset">How many characters after the first non-whitespace character of the origin line should be skipped before applying formatting</param>
    /// <param name="FormattedLength">How many characters of the origin line the formatted line represents</param>
    /// <param name="FormattedOffset">How many characters after the first non-whitespace character of the formatted line should be skipped before applying formatting</param>
    /// <param name="FormattedOffsetFromEndOfLine">How many characters before the end of the formatted line should be skipped before applying formatting</param>
    /// <param name="AdditionalIndentation">An arbitrary string representing additional indentation to apply to this line</param>
    private readonly record struct LineInfo(
        bool ProcessIndentation,
        bool ProcessFormatting,
        bool CheckForNewLines,
        bool SkipPreviousLine,
        bool SkipNextLine,
        bool SkipNextLineIfBrace,
        int HtmlIndentLevel,
        int OriginOffset,
        int FormattedLength,
        int FormattedOffset,
        int FormattedOffsetFromEndOfLine,
        string? AdditionalIndentation);
}
