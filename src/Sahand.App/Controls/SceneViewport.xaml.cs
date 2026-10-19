using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Sahand.Geometry.Meshing;

namespace Sahand.App.Controls;

/// <summary>
/// کنترل پیش‌نمایش سه‌بعدی — دوربین مداری (چرخش/بزرگ‌نمایی/جابه‌جایی) با Viewport3D بومی.
/// </summary>
/// <remarks>
/// قرارداد جهت (سند ۷-۵): محور قطعه Z است؛ صحنه با Y-بالا نمایش داده می‌شود و مش با
/// نگاشت (x, z, −y) به صحنه می‌آید تا نوک قطعه رو به بالا باشد. برای سایه‌روشن تختِ
/// قطعات مهندسی، رأس‌ها per-face تکرار و نرمال وجهی صریح داده می‌شوند (بدون smooth shading).
/// ساخت مش سنگین پیش از این کنترل و در پس‌زمینه انجام می‌شود (سند ۳-۷)؛ این کنترل
/// فقط نمایش است. هیچ وابستگی رندر بیرونی ندارد — کاملاً آفلاین.
/// </remarks>
public partial class SceneViewport : UserControl
{
    // حالت مدار کروی دوربین حول نقطهٔ هدف.
    private double _orbitTheta = 0.55;  // زاویهٔ افقی (رادیان).
    private double _orbitPhi = 1.05;    // زاویهٔ عمودی از محور بالا (رادیان).
    private double _orbitRadius = 80;   // فاصلهٔ دوربین (میلی‌متر صحنه).
    private Point3D _target = new(0, 10, 0);
    private Point _lastMouse;
    private bool _dragging;
    private bool _panning;

    /// <summary>رنگ نمایشی متریال مش.</summary>
    public static readonly DependencyProperty MeshColorProperty = DependencyProperty.Register(
        nameof(MeshColor), typeof(Color), typeof(SceneViewport),
        new PropertyMetadata(Color.FromRgb(0xB8, 0xBC, 0xC2)));

    /// <summary>رنگ نمایشی مش — پس از تغییر، ShowMesh دوباره فراخوانی شود.</summary>
    public Color MeshColor
    {
        get => (Color)GetValue(MeshColorProperty);
        set => SetValue(MeshColorProperty, value);
    }

    public SceneViewport()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateCamera();
    }

    /// <summary>
    /// مش را با سایه‌روشن تخت نمایش می‌دهد؛ مش null صحنه را خالی می‌کند.
    /// </summary>
    public void ShowMesh(Mesh? mesh)
    {
        var group = new Model3DGroup();

        if (mesh is { Vertices.Length: > 0, Triangles.Length: > 0 })
        {
            // رأس‌های per-face (۳×تکرار) برای نرمال وجهی صریح — قطعات مهندسی تخت‌روشن دیده می‌شوند.
            var positions = new Point3D[mesh.Triangles.Length * 3];
            var normals = new Vector3D[mesh.Triangles.Length * 3];

            for (var i = 0; i < mesh.Triangles.Length; i++)
            {
                var t = mesh.Triangles[i];
                var a = MapToScene(mesh.Vertices[t.A]);
                var b = MapToScene(mesh.Vertices[t.B]);
                var c = MapToScene(mesh.Vertices[t.C]);

                // نرمال وجه از winding بیرونی؛ جهت صحنه پس از نگاشت محاسبه می‌شود.
                var normal = Vector3D.CrossProduct(b - a, c - a);
                normal.Normalize();

                positions[i * 3] = a;
                positions[i * 3 + 1] = b;
                positions[i * 3 + 2] = c;
                normals[i * 3] = normal;
                normals[i * 3 + 1] = normal;
                normals[i * 3 + 2] = normal;
            }

            var indices = new Int32Collection(mesh.Triangles.Length * 3);
            for (var i = 0; i < positions.Length; i++) indices.Add(i);

            var geometry = new MeshGeometry3D
            {
                Positions = new Point3DCollection(positions),
                Normals = new Vector3DCollection(normals),
                TriangleIndices = indices,
            };

            var material = new DiffuseMaterial(new SolidColorBrush(MeshColor)) { Color = MeshColor };
            group.Children.Add(new GeometryModel3D(geometry, material));

            // تنظیم هدف/شعاع مدار بر اساس جعبهٔ محیطی (در مختصات صحنه).
            var (minX, minY, minZ, maxX, maxY, maxZ) = mesh.Bounds();
            var centerScene = MapToScene(new MeshVertex(
                (minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2));
            _target = new Point3D(centerScene.X, centerScene.Y, centerScene.Z);
            var size = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
            _orbitRadius = Math.Max(size * 2.0, 10);
        }

        PartModel.Content = group;
        UpdateCamera();
    }

    /// <summary>خالی‌کردن صحنه.</summary>
    public void Clear() => PartModel.Content = null;

    /// <summary>نگاشت مختصات قطعه به مختصات صحنه (محور قطعه Z → بالای صحنه).</summary>
    private static Point3D MapToScene(MeshVertex v) => new(v.X, v.Z, -v.Y);

    private void UpdateCamera()
    {
        var sinPhi = Math.Sin(_orbitPhi);
        var x = _target.X + _orbitRadius * sinPhi * Math.Cos(_orbitTheta);
        var y = _target.Y + _orbitRadius * Math.Cos(_orbitPhi);
        var z = _target.Z + _orbitRadius * sinPhi * Math.Sin(_orbitTheta);

        Camera.Position = new Point3D(x, y, z);
        Camera.LookDirection = new Vector3D(_target.X - x, _target.Y - y, _target.Z - z);
        Camera.UpDirection = new Vector3D(0, 1, 0);
    }

    private void OnFitClick(object sender, RoutedEventArgs e) => UpdateCamera();

    private void OnFrontClick(object sender, RoutedEventArgs e)
    {
        _orbitTheta = -Math.PI / 2;
        _orbitPhi = Math.PI / 2;
        UpdateCamera();
    }

    private void OnIsoClick(object sender, RoutedEventArgs e)
    {
        _orbitTheta = 0.55;
        _orbitPhi = 1.05;
        UpdateCamera();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragging = true;
        _lastMouse = e.GetPosition(Viewport);
        CaptureMouse();
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        _panning = true;
        _lastMouse = e.GetPosition(Viewport);
        CaptureMouse();
        base.OnMouseRightButtonDown(e);
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        _dragging = false;
        _panning = false;
        ReleaseMouseCapture();
        base.OnMouseUp(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var position = e.GetPosition(Viewport);
        var dx = position.X - _lastMouse.X;
        var dy = position.Y - _lastMouse.Y;
        _lastMouse = position;

        if (_dragging)
        {
            _orbitTheta += dx * 0.01;
            _orbitPhi = Math.Clamp(_orbitPhi - dy * 0.01, 0.15, Math.PI - 0.15);
            UpdateCamera();
        }
        else if (_panning)
        {
            // جابه‌جایی صفحه‌ای هدف روی بردارهای راست/بالا دوربین.
            var forward = Camera.LookDirection;
            forward.Normalize();
            var right = Vector3D.CrossProduct(forward, Camera.UpDirection);
            right.Normalize();
            var up = Vector3D.CrossProduct(right, forward);

            var scale = _orbitRadius / Math.Max(Viewport.ActualWidth, 1) * 1.5;
            _target = new Point3D(
                _target.X - right.X * dx * scale + up.X * dy * scale,
                _target.Y - right.Y * dx * scale + up.Y * dy * scale,
                _target.Z - right.Z * dx * scale + up.Z * dy * scale);
            UpdateCamera();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        // بزرگ‌نمایی لگاریتمی برای حس طبیعی.
        _orbitRadius = Math.Clamp(_orbitRadius * Math.Pow(0.9, Math.Sign(e.Delta)), 2, 5000);
        UpdateCamera();
        base.OnMouseWheel(e);
    }
}
