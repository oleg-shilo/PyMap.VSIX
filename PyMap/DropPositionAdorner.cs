using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Controls;

public class DropPositionAdorner : Adorner
{
    private readonly int _insertIndex;
    private readonly ItemsControl _itemsControl;

    public DropPositionAdorner(ItemsControl itemsControl, int insertIndex)
        : base(itemsControl)
    {
        _itemsControl = itemsControl;
        _insertIndex = insertIndex;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_itemsControl.Items.Count == 0)
            return;

        var itemContainer = _insertIndex < _itemsControl.Items.Count
            ? _itemsControl.ItemContainerGenerator.ContainerFromIndex(_insertIndex) as FrameworkElement
            : _itemsControl.ItemContainerGenerator.ContainerFromIndex(_itemsControl.Items.Count - 1) as FrameworkElement;

        if (itemContainer == null)
            return;

        Point start, end;
        if (_insertIndex < _itemsControl.Items.Count)
        {
            var topLeft = itemContainer.TransformToAncestor(_itemsControl).Transform(new Point(0, 0));
            start = new Point(0, topLeft.Y);
            end = new Point(_itemsControl.ActualWidth, topLeft.Y);
        }
        else
        {
            var bottomLeft = itemContainer.TransformToAncestor(_itemsControl).Transform(new Point(0, itemContainer.ActualHeight));
            start = new Point(0, bottomLeft.Y);
            end = new Point(_itemsControl.ActualWidth, bottomLeft.Y);
        }

        var pen = new Pen(Brushes.DodgerBlue, 2);
        drawingContext.DrawLine(pen, start, end);
    }
}
