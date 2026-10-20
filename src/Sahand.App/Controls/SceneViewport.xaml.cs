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
/// نگاشت (x, z, −y) به صحنه می‌آید تا نوک قطعه رو به بالا باشد.
/// کیفیت نمایش (بهبود ۰٫۳٫۰): نرمال‌های راسی «هموارِ آگاه از لبه» — در هر رأس، وجه‌های
/// همسایه با نرمالِ نزدیک (اختلاف زیر آستانهٔ ۳۵ درجه) خوشه می‌شوند و میانگینِ وزنی
/// مساحت به گوشه‌های همان خوشه می‌رسد؛ نتیجه: سطوح منحنی (ساق/رزوه) صاف و درخشان دیده
/// می‌شوند و لبه‌های تیز (گوشهٔ شش‌گوش، دیوارهٔ شیار) تیز می‌مانند. متریال با لایهٔ
/// Specular فلزی و نورپردازی سه‌نقطه‌ای (کلید/پرکننده/لبه) + سایهٔ نرمِ زمین کامل می‌شود.
/// ساخت مش سنگین پیش از این کنترل و در پس‌زمینه انجام می‌شود (سند ۳-۷)؛ این کنترل
/// فقط نمایش است. هیچ وابستگی رندر بیرونی ندارد — کاملاً آفلاین.
/// </remarks>
public partial class SceneViewport : UserControl
{
    // آستانهٔ «تیزی» لبه — اختلاف نرمال وجه‌های همسایه بالای این زاویه، لبهٔ تیز می‌ماند.
    private const double CreaseCosThreshold = 0.81915; // cos(35°)

    // حالت مدار کروی دوربین حول نقطهٔ هدف.
    private double _orbitTheta = 0.55;  // زاویهٔ افقی (رادیان).
    private double _orbitPhi = 1.05;    // زاویهٔ عمودی از محور بالا (رادیان).
    private double _orbitRadius = 80;   // فاصلهٔ دوربین (میلی‌متر صحنه).
    private Point3D _target = new(0, 10, 0);
    private Point _lastMouse;
    private bool _dragging;
    private bool _panning;

    // کرهٔ محیطی مدل فعلی در مختصات صحنه — مبنای «جا دادن» واقعی (اصلاح ۰٫۵٫۰).
    private Point3D _meshCenter;
    private double _meshRadius = 10;
    private bool _hasMesh;

    // آیا کاربر خودش چرخش/بزرگ‌نمایی/جابه‌جایی کرده؟ تا باز-جا دادن خودکار بعد از تغییر اندازه،
    // نتواند قاب دستی کاربر را بازتنظیم کند.
    private bool _userAdjusted;

    // زاویه‌های پیش‌فرض نمای ایزو — «جا دادن» به این نما برمی‌گردد.
    private const double DefaultTheta = 0.55;
    private const double DefaultPhi = 1.05;

    // حاشیهٔ قاب جا دادن — ۱۲٪ فضای خالی دور مدل.
    private const double FitMargin = 1.12;

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
        Loaded += (_, _) => FitToMesh();

        // باز-جا دادن خودکار وقتی پنجره تغییر اندازه می‌دهد — فقط تا وقتی کاربر
        // قاب را دستی تنظیم نکرده باشد (اصلاح ۰٫۵٫۰).
        SizeChanged += (_, _) =>
        {
            if (!_userAdjusted)
                FitToMesh();
        };
    }

    /// <summary>
    /// مش را با سایه‌روشن هموارِ آگاه از لبه، متریال فلزی و سایهٔ زمین نمایش می‌دهد؛
    /// مش null صحنه را خالی می‌کند.
    /// </summary>
    public void ShowMesh(Mesh? mesh)
    {
        var group = new Model3DGroup();

        if (mesh is { Vertices.Length: > 0, Triangles.Length: > 0 })
        {
            var tri = mesh.Triangles;
            var faceCount = tri.Length;

            // ۱) موقعیت گوشه‌ها + نرمال وجهی + مساحت وجه‌ها (در مختصات صحنه).
            var positions = new Point3D[faceCount * 3];
            var normals = new Vector3D[faceCount * 3];
            var faceNormals = new Vector3D[faceCount];
            var faceAreas = new double[faceCount];

            for (var i = 0; i < faceCount; i++)
            {
                var t = tri[i];
                var a = MapToScene(mesh.Vertices[t.A]);
                var b = MapToScene(mesh.Vertices[t.B]);
                var c = MapToScene(mesh.Vertices[t.C]);

                var cross = Vector3D.CrossProduct(b - a, c - a);
                var area = 0.5 * cross.Length;
                var normal = cross;
                if (normal.LengthSquared > 1e-20) normal.Normalize();

                faceNormals[i] = normal;
                faceAreas[i] = area;

                positions[i * 3] = a;
                positions[i * 3 + 1] = b;
                positions[i * 3 + 2] = c;
                // مقدار اولیه = نرمال وجهی؛ خوشه‌های هموار آن را بازنویسی می‌کنند.
                normals[i * 3] = normal;
                normals[i * 3 + 1] = normal;
                normals[i * 3 + 2] = normal;
            }

            SmoothNormals(mesh, tri, faceNormals, faceAreas, positions, normals);

            var indices = new Int32Collection(faceCount * 3);
            for (var i = 0; i < positions.Length; i++) indices.Add(i);

            var geometry = new MeshGeometry3D
            {
                Positions = new Point3DCollection(positions),
                Normals = new Vector3DCollection(normals),
                TriangleIndices = indices,
            };

            // ۲) متریال فلزی: پخش + بازتاب ویژه (براقیت فولاد).
            var diffuse = new DiffuseMaterial(new SolidColorBrush(MeshColor))
            {
                AmbientColor = Color.FromRgb(
                    (byte)(MeshColor.R * 0.55),
                    (byte)(MeshColor.G * 0.55),
                    (byte)(MeshColor.B * 0.55)),
            };
            var specular = new SpecularMaterial(
                new SolidColorBrush(Color.FromRgb(0xD6, 0xDE, 0xE6)), 34.0);
            var material = new MaterialGroup { Children = { diffuse, specular } };
            group.Children.Add(new GeometryModel3D(geometry, material) { BackMaterial = material });

            // ۳) سایهٔ نرم زمین — یک صفحهٔ شفاف با گرادیان شعاعی زیر قطعه.
            AddGroundShadow(group, mesh);

            // کرهٔ محیطی مدل در مختصات صحنه — مبنای «جا دادن» (اصلاح ۰٫۵٫۰).
            var (minX, minY, minZ, maxX, maxY, maxZ) = mesh.Bounds();
            var centerScene = MapToScene(new MeshVertex(
                (minX + maxX) / 2, (minY + maxY) / 2, (minZ + maxZ) / 2));
            _meshCenter = new Point3D(centerScene.X, centerScene.Y, centerScene.Z);
            _meshRadius = Math.Max(
                Math.Sqrt(
                    (maxX - minX) * (maxX - minX) +
                    (maxY - minY) * (maxY - minY) +
                    (maxZ - minZ) * (maxZ - minZ)) / 2.0,
                0.5);
            _hasMesh = true;
        }

        PartModel.Content = group;
        _userAdjusted = false;
        FitToMesh();
    }

    /// <summary>خالی‌کردن صحنه.</summary>
    public void Clear()
    {
        PartModel.Content = null;
        _hasMesh = false;
        _userAdjusted = false;
    }

    /// <summary>
    /// نرمال‌های راسی هموارِ آگاه از لبه را می‌سازد و در آرایهٔ <paramref name="normals"/>
    /// (per-corner) می‌نویسد.
    /// </summary>
    /// <remarks>
    /// الگوریتم: نگاشت رأس → گوشه‌های incident؛ سپس در هر رأس، وجه‌های incident با
    /// نرمال‌های نزدیک (dot ≥ آستانه) به خوشه‌های هموار گروه‌بندی می‌شوند (اتصال انتقالی،
    /// حریصانه) و میانگینِ وزنی به مساحتِ هر خوشه به گوشه‌های عضو آن می‌رسد. لبهٔ تیز
    /// (اختلاف > آستانه) نرمال جدا می‌ماند و قطعه تیز دیده می‌شود.
    /// </remarks>
    private static void SmoothNormals(
        Mesh mesh,
        MeshTriangle[] tri,
        Vector3D[] faceNormals,
        double[] faceAreas,
        Point3D[] positions,
        Vector3D[] normals)
    {
        var vertexCount = mesh.Vertices.Length;

        // گوشه‌های incident هر رأس (اندیس آرایهٔ positions = 3×وجه + ۰..۲).
        var incident = new List<int>[vertexCount];
        for (var corner = 0; corner < positions.Length; corner++)
        {
            var vertex = corner % 3 == 0
                ? tri[corner / 3].A
                : corner % 3 == 1 ? tri[corner / 3].B : tri[corner / 3].C;
            (incident[vertex] ??= []).Add(corner);
        }

        for (var v = 0; v < vertexCount; v++)
        {
            var corners = incident[v];
            if (corners is not { Count: > 1 }) continue;

            // وجه‌های incident این رأس — هر وجه حداکثر یک‌بار (مثلث غیرمضرس).
            var faces = new int[corners.Count];
            for (var i = 0; i < corners.Count; i++) faces[i] = corners[i] / 3;

            var assigned = new bool[faces.Length];

            for (var seed = 0; seed < faces.Length; seed++)
            {
                if (assigned[seed]) continue;

                // خوشهٔ جدید — رشد انتقالی با آستانهٔ زاویه.
                var cluster = new List<int> { seed };
                assigned[seed] = true;
                bool grew;
                do
                {
                    grew = false;
                    for (var i = 0; i < faces.Length; i++)
                    {
                        if (assigned[i]) continue;
                        var belongs = false;
                        foreach (var member in cluster)
                        {
                            if (Vector3D.DotProduct(faceNormals[faces[i]], faceNormals[faces[member]])
                                >= CreaseCosThreshold)
                            {
                                belongs = true;
                                break;
                            }
                        }

                        if (!belongs) continue;
                        cluster.Add(i);
                        assigned[i] = true;
                        grew = true;
                    }
                }
                while (grew);

                // میانگین وزنی به مساحت در سرتاسر خوشه.
                var sum = new Vector3D();
                foreach (var member in cluster)
                {
                    var f = faces[member];
                    sum += faceNormals[f] * faceAreas[f];
                }

                if (sum.LengthSquared < 1e-20) continue; // وجه‌های مضرس — نرمال وجهی می‌ماند.
                sum.Normalize();

                foreach (var member in cluster)
                    normals[corners[member]] = sum;
            }
        }
    }

    /// <summary>صفحهٔ سایهٔ نرم زیر قطعه — گرادیان شعاعی شفاف روی صفحهٔ افقی.</summary>
    private static void AddGroundShadow(Model3DGroup group, Mesh mesh)
    {
        var (minX, minY, minZ, maxX, maxY, maxZ) = mesh.Bounds();

        // مرز صحنه: X همان X قطعه است؛ «بالا» در صحنه = محور Z قطعه (نگاشت (x,z,−y)).
        var sceneMinY = minZ;
        var sceneMaxX = maxX;
        var sceneMinX = minX;
        var sceneMaxZ = -minY;
        var sceneMinZ = -maxY;

        var size = Math.Max(Math.Max(maxX - minX, maxY - minY), maxZ - minZ);
        var groundY = sceneMinY - Math.Max(size * 0.02, 0.15);
        var cx = (sceneMinX + sceneMaxX) / 2.0;
        var cz = (sceneMinZ + sceneMaxZ) / 2.0;

        var xSpan = sceneMaxX - sceneMinX;
        var zSpan = sceneMaxZ - sceneMinZ;
        var half = Math.Max(Math.Max(xSpan, zSpan) * 0.72, size * 0.25);

        var brush = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop(Color.FromArgb(0xA6, 0x00, 0x00, 0x00), 0.0),
                new GradientStop(Color.FromArgb(0x54, 0x00, 0x00, 0x00), 0.55),
                new GradientStop(Color.FromArgb(0x00, 0x00, 0x00, 0x00), 1.0),
            },
        };

        // گوشه‌ها پادساعتگرد از بالا (نرمال +Y): p0→p2→p1 و p0→p3→p2.
        var p0 = new Point3D(cx - half, groundY, cz - half);
        var p1 = new Point3D(cx + half, groundY, cz - half);
        var p2 = new Point3D(cx + half, groundY, cz + half);
        var p3 = new Point3D(cx - half, groundY, cz + half);
        var up = new Vector3D(0, 1, 0);

        var geometry = new MeshGeometry3D
        {
            Positions = new Point3DCollection { p0, p1, p2, p3 },
            Normals = new Vector3DCollection { up, up, up, up },
            TriangleIndices = new Int32Collection { 0, 2, 1, 0, 3, 2 },
            TextureCoordinates = new PointCollection { new(0, 1), new(1, 1), new(1, 0), new(0, 0) },
        };

        group.Children.Add(new GeometryModel3D(geometry, new DiffuseMaterial(brush)));
    }

    /// <summary>نگاشت مختصات قطعه به مختصات صحنه (محور قطعه Z → بالای صحنه).</summary>
    private static Point3D MapToScene(MeshVertex v) => new(v.X, v.Z, -v.Y);

    /// <summary>
    /// «جا دادن» واقعی مدل در نما — اصلاح ۰٫۵٫۰.
    /// </summary>
    /// <remarks>
    /// پیش‌تر دکمهٔ «جا دادن» فقط UpdateCamera را صدا می‌زد که دوربین را در همان
    /// زاویه/شعاع/هدفِ فعلی رها می‌کرد — عملاً هیچ کاری نمی‌کرد. اکنون:
    /// ۱) هدف = مرکز کرهٔ محیطی مدل؛ ۲) فاصلهٔ دوربین از هندسهٔ منظر (FOV عمودی +
    /// نسبت عرض/ارتفاع viewport) طوری حساب می‌شود که کرهٔ محیطی با حاشیهٔ امن
    /// «کامل» در قاب بیفتد؛ ۳) زاویه‌ها به نمای ایزوی استاندارد برمی‌گردند.
    /// </remarks>
    private void FitToMesh()
    {
        if (_hasMesh)
        {
            _target = _meshCenter;

            // منظر مؤثر: کوچک‌تر از FOV عمودی و FOV افقی (بر اساس نسبت تصویر).
            var fovV = Camera.FieldOfView * Math.PI / 180.0;
            var width = Viewport.ActualWidth;
            var height = Viewport.ActualHeight;
            var aspect = height > 1 ? width / height : 1.0;
            var fovH = 2.0 * Math.Atan(Math.Tan(fovV / 2.0) * aspect);
            var fov = Math.Max(Math.Min(fovV, fovH), 0.1);

            // فاصلهٔ دوربین برای جا شدن کرهٔ محیطی با حاشیه؛ سقف/کف ایمنی عددی.
            var half = Math.Min(fov / 2.0, Math.PI / 2.0 - 0.02);
            _orbitRadius = Math.Clamp(_meshRadius * FitMargin / Math.Sin(half), 2.0, 5000.0);
        }
        else
        {
            // بدون مدل: نمای ایزوی پیش‌فرض دور هدفِ فعلی.
            _orbitRadius = Math.Max(_orbitRadius, 40);
        }

        _orbitTheta = DefaultTheta;
        _orbitPhi = DefaultPhi;
        UpdateCamera();
    }

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

    private void OnFitClick(object sender, RoutedEventArgs e)
    {
        _userAdjusted = false; // باز-جا دادن خودکار بعد از تغییر اندازه فعال می‌شود.
        FitToMesh();
    }

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
        _userAdjusted = true;
        _lastMouse = e.GetPosition(Viewport);
        CaptureMouse();
        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        _panning = true;
        _userAdjusted = true;
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
        _userAdjusted = true;
        _orbitRadius = Math.Clamp(_orbitRadius * Math.Pow(0.9, Math.Sign(e.Delta)), 2, 5000);
        UpdateCamera();
        base.OnMouseWheel(e);
    }
}
