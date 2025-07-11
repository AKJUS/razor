﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.VisualStudio.LegacyEditor.Razor;
using Microsoft.VisualStudio.Text.Editor;
using ContextChangeEventArgsInternal = Microsoft.VisualStudio.LegacyEditor.Razor.ContextChangeEventArgs;
using ContextChangeKindInternal = Microsoft.VisualStudio.LegacyEditor.Razor.ContextChangeKind;

namespace Microsoft.AspNetCore.Razor.ExternalAccess.LegacyEditor;

internal static partial class RazorWrapperFactory
{
    private class DocumentTrackerWrapper(IVisualStudioDocumentTracker obj) : Wrapper<IVisualStudioDocumentTracker>(obj), IRazorDocumentTracker
    {
        private EventHandler<ContextChangeEventArgs>? _contextChanged;

        public ImmutableArray<ITextView> TextViews
            => Object.TextViews.ToImmutableArray();

        public event EventHandler<ContextChangeEventArgs> ContextChanged
        {
            add
            {
                // If this is the first handler, hook the inner event.
                if (_contextChanged is null)
                {
                    Object.ContextChanged += OnContextChanged;
                }

                _contextChanged += value;
            }

            remove
            {
                _contextChanged -= value;

                // If there are no more handlers, unhook the inner event.
                if (_contextChanged is null)
                {
                    Object.ContextChanged -= OnContextChanged;
                }
            }
        }

        private void OnContextChanged(object sender, ContextChangeEventArgsInternal e)
        {
            // Be sure to use our wrapper as the sender to avoid leaking the inner object.
            if (_contextChanged is { } handler)
            {
                var kind = e.Kind switch
                {
                    ContextChangeKindInternal.ProjectChanged => ContextChangeKind.ProjectChanged,
                    ContextChangeKindInternal.EditorSettingsChanged => ContextChangeKind.EditorSettingsChanged,
                    ContextChangeKindInternal.TagHelpersChanged => ContextChangeKind.TagHelpersChanged,
                    ContextChangeKindInternal.ImportsChanged => ContextChangeKind.ImportsChanged,
                    _ => throw new NotSupportedException()
                };

                handler(sender: this, new ContextChangeEventArgs(kind));
            }
        }
    }
}
