using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodeMap
{
    internal class BookmarkMargin : Canvas, IWpfTextViewMargin
    {
        private bool _isDisposed;
        private static double
            _lineWidth = 200,
            _lineHeight = 5;
        private static byte _opacity = 0x50;
        private readonly IVerticalScrollBar _scrollBar;
        private readonly Dictionary<string, Brush> _palette;
        private readonly SyntaxParser _parser;

        public const string MarginName = "CodeMapBookmarkMargin";

        public BookmarkMargin(IWpfTextViewMargin marginContainer)
        {
            ToolWindowPane window = ToolWindow1Command.Instance.package.FindToolWindow(typeof(ToolWindow1), 0, true);
            ToolWindow1Control control = ((ToolWindow1)window).Content as ToolWindow1Control;

            _scrollBar = marginContainer as IVerticalScrollBar;

            _palette = new Dictionary<string, Brush>()
            {
                { "bookmark1", SetOpacity(control.Resources["Bookmark1"] as SolidColorBrush)},
                { "bookmark2", SetOpacity(control.Resources["Bookmark2"] as SolidColorBrush)},
                { "bookmark3", SetOpacity(control.Resources["Bookmark3"] as SolidColorBrush)},
                { "bookmark4", SetOpacity(control.Resources["Bookmark4"] as SolidColorBrush)},
            };

            _parser = control.DataContext as SyntaxParser;

            _parser.PropertyChanged += (s, e) =>
            {
                InvalidateVisual();
            };

            control.BookmarkMenuClick += (s, e) =>
            {
                InvalidateVisual();
            };
        }

        private Brush SetOpacity(SolidColorBrush solidColorBrush)
        {
            Color color = solidColorBrush.Color;
            return new SolidColorBrush(Color.FromArgb(_opacity, color.R, color.G, color.B));
        }

        #region IWpfTextViewMargin

        public FrameworkElement VisualElement
        {
            get
            {
                ThrowIfDisposed();
                return this;
            }
        }

        #endregion

        #region ITextViewMargin

        public double MarginSize
        {
            get
            {
                ThrowIfDisposed();
                return ActualHeight;
            }
        }

        public bool Enabled
        {
            get
            {
                ThrowIfDisposed();
                return true;
            }
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return string.Equals(marginName, BookmarkMargin.MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                GC.SuppressFinalize(this);
                _isDisposed = true;
            }
        }

        #endregion

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(MarginName);
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            DrawMarkers(drawingContext);
        }

        void DrawMarkers(DrawingContext drawingContext)
        {
            if (_parser.MemberList is null || !_parser.MemberList.Any())
            {
                return;
            }

            foreach (MemberInfo memberInfo in _parser.MemberList.Where(x => !string.IsNullOrEmpty(x.ColorContext)))
            {
                if (_palette.TryGetValue(memberInfo.ColorContext, out Brush brush))
                {
                    double y = _scrollBar.GetYCoordinateOfScrollMapPosition(memberInfo.Line);
                    drawingContext.DrawRectangle(brush, null, new Rect(0, y, _lineWidth, _lineHeight));
                }
            }
        }
    }
}
