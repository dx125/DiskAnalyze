using DiskAnalyze.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DiskAnalyze.Views
{
    public partial class CircularDiagramView : UserControl
    {
        // The diagram canvas is a fixed 400x400 surface scaled by a Viewbox
        private const double CenterX = 200;
        private const double CenterY = 200;
        private const double Radius = 150;

        private FileSystemEntityViewModel? _hoveredEntity;

        public CircularDiagramView()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is CircularDiagramViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is CircularDiagramViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
                UpdateDiagram();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CircularDiagramViewModel.DisplayedEntities))
            {
                UpdateDiagram();
            }
        }

        private void UpdateDiagram()
        {
            if (DataContext is not CircularDiagramViewModel viewModel)
                return;

            double startAngle = 0;

            foreach (var entity in viewModel.DisplayedEntities)
            {
                // A full 360° sweep degenerates the ArcSegment (start == end), so cap just below
                double sweep = Math.Min(entity.PercentageOfTotal * 3.6, 359.9);
                double endAngle = startAngle + sweep;

                entity.ArcStartPoint = PointOnCircle(startAngle, Radius);
                entity.ArcEndPoint = PointOnCircle(endAngle, Radius);
                entity.IsLargeArc = sweep > 180;

                double middleAngle = startAngle + sweep / 2;
                var labelCenter = PointOnCircle(middleAngle, Radius * 0.7);
                entity.LabelX = labelCenter.X - 30; // half label width
                entity.LabelY = labelCenter.Y - 20; // half label height
                entity.TooltipX = entity.LabelX;
                entity.TooltipY = entity.LabelY - 40;

                startAngle = endAngle;
            }
        }

        // 0° is at 12 o'clock, increasing clockwise
        private static Point PointOnCircle(double angleInDegrees, double radius)
        {
            double radians = angleInDegrees * Math.PI / 180;
            return new Point(CenterX + radius * Math.Sin(radians), CenterY - radius * Math.Cos(radians));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            SetHoveredEntity(HitTestEntity(e.GetPosition(CircularDiagramCanvas)));
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            SetHoveredEntity(null);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            if (DataContext is CircularDiagramViewModel viewModel && e.LeftButton == MouseButtonState.Pressed)
            {
                var hitEntity = HitTestEntity(e.GetPosition(CircularDiagramCanvas));

                if (hitEntity is DirectoryEntityViewModel directory)
                {
                    viewModel.NavigateToDirectoryCommand.Execute(directory);
                }
            }
        }

        private void SetHoveredEntity(FileSystemEntityViewModel? entity)
        {
            if (ReferenceEquals(entity, _hoveredEntity))
                return;

            if (_hoveredEntity != null)
            {
                _hoveredEntity.IsHovered = false;
            }

            _hoveredEntity = entity;

            if (_hoveredEntity != null)
            {
                _hoveredEntity.IsHovered = true;
            }
        }

        private FileSystemEntityViewModel? HitTestEntity(Point position)
        {
            var result = VisualTreeHelper.HitTest(CircularDiagramCanvas, position);

            // Walk up from the hit visual to the owning entity. The hovered segment is
            // overlaid by its tooltip and label, so the topmost visual under the cursor
            // is often not the Path itself — matching only Path would flip the hover
            // state off as soon as those overlays appear, causing a flicker loop.
            for (DependencyObject? current = result?.VisualHit; current != null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is FrameworkElement element && element.DataContext is FileSystemEntityViewModel entity)
                {
                    return entity;
                }
            }

            return null;
        }
    }
}
