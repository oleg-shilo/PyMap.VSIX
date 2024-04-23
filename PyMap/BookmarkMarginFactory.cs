using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace CodeMap
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [Name(BookmarkMargin.MarginName)]
    [MarginContainer(PredefinedMarginNames.VerticalScrollBar)]
    [Order(Before = PredefinedMarginNames.OverviewChangeTracking)]
    [Order(Before = PredefinedMarginNames.OverviewError)]
    [Order(Before = PredefinedMarginNames.OverviewMark)]
    [Order(Before = PredefinedMarginNames.OverviewSourceImage)]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    internal sealed class BookmarkMarginFactory : IWpfTextViewMarginProvider
    {
        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            return new BookmarkMargin(marginContainer);
        }
    }
}
