using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace LocalMultiControl.Scripts.Runtime;

internal sealed partial class LocalSimpleTextButton : NButton
{
    private const string ButtonTexturePath = "res://images/packed/common_ui/settings_tiny_left_arrow.png";
    private const string HsvShaderPath = "res://shaders/hsv.gdshader";
    private static readonly Vector2 DefaultButtonSize = new Vector2(56f, 32f);

    private TextureRect? _image;
    private Label? _label;
    private ShaderMaterial? _hsv;
    private Tween? _hoverTween;
    private string _buttonText = string.Empty;
    private int _fontSize = 20;
    private Vector2 _imageScale = Vector2.One;
    private bool _mirrorImageX;

    public string ButtonText
    {
        get => _buttonText;
        set
        {
            _buttonText = value;
            if (_label != null)
            {
                _label.Text = _buttonText;
            }
        }
    }

    public int FontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            if (_label != null)
            {
                _label.AddThemeFontSizeOverride("font_size", _fontSize);
            }
        }
    }

    public Vector2 ImageScale
    {
        get => _imageScale;
        set
        {
            _imageScale = value;
            ApplyImageTransform();
        }
    }

    public bool MirrorImageX
    {
        get => _mirrorImageX;
        set
        {
            _mirrorImageX = value;
            ApplyImageTransform();
        }
    }

    public override void _Ready()
    {
        BuildVisualTree();
        ConnectSignals();
        ApplyDefaultVisualState();
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        _hoverTween?.Kill();
        UpdateShaderValue(1.15f);
        Scale = Vector2.One * 1.04f;
    }

    protected override void OnUnfocus()
    {
        _hoverTween?.Kill();
        _hoverTween = CreateTween().SetParallel();
        _hoverTween.TweenMethod(Callable.From<float>(UpdateShaderValue), 1.15f, 1f, 0.2);
        _hoverTween.TweenProperty(this, "scale", Vector2.One, 0.2);
    }

    protected override void OnPress()
    {
        base.OnPress();
        _hoverTween?.Kill();
        UpdateShaderValue(0.85f);
        Scale = Vector2.One * 0.98f;
    }

    protected override void OnRelease()
    {
        if (IsFocused)
        {
            UpdateShaderValue(1.15f);
            Scale = Vector2.One * 1.04f;
            return;
        }

        ApplyDefaultVisualState();
    }

    private void BuildVisualTree()
    {
        FocusMode = FocusModeEnum.All;
        MouseFilter = MouseFilterEnum.Stop;

        // 注意：这里不能再硬编码大尺寸。
        // 之前强制 120x52 导致所有业务按钮（+/-、切人、上下切人）被放大且遮挡，甚至出现无法点击 + 的回归。
        // 如需调整按钮大小，请在各业务调用处设置 CustomMinimumSize，并保持本处“尊重外部传入尺寸”的行为。
        Vector2 desiredSize = ResolveDesiredSize();
        CustomMinimumSize = desiredSize;
        Size = desiredSize;
        PivotOffset = desiredSize * 0.5f;

        _image ??= CreateImageNode();
        _label ??= CreateLabelNode();

        if (_image.GetParent() == null)
        {
            AddChild(_image);
        }

        if (_label.GetParent() == null)
        {
            AddChild(_label);
        }

        _label.Text = _buttonText;
        ApplyImageTransform();
    }

    private TextureRect CreateImageNode()
    {
        TextureRect image = new TextureRect
        {
            Name = "Image",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = ResourceLoader.Load<Texture2D>(ButtonTexturePath, null, ResourceLoader.CacheMode.Reuse)
        };
        image.Set("expand_mode", 1);
        image.Set("stretch_mode", 5);
        image.SelfModulate = new Color(1f, 1f, 1f, 0.95f);

        Shader? shader = ResourceLoader.Load<Shader>(HsvShaderPath, null, ResourceLoader.CacheMode.Reuse);
        if (shader != null)
        {
            _hsv = new ShaderMaterial
            {
                ResourceLocalToScene = true,
                Shader = shader
            };
            _hsv.SetShaderParameter("h", 1f);
            _hsv.SetShaderParameter("s", 1f);
            _hsv.SetShaderParameter("v", 1f);
            image.Material = _hsv;
        }

        return image;
    }

    private Label CreateLabelNode()
    {
        Label label = new Label
        {
            Name = "Label",
            AnchorLeft = 0f,
            AnchorTop = 0f,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 0f,
            OffsetTop = 0f,
            OffsetRight = 0f,
            OffsetBottom = 0f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        label.AddThemeColorOverride("font_color", new Color("fdf6e3"));
        label.AddThemeColorOverride("font_outline_color", new Color("13202e"));
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.25f));
        label.AddThemeConstantOverride("outline_size", 7);
        label.AddThemeConstantOverride("shadow_offset_x", 2);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeFontSizeOverride("font_size", _fontSize);
        return label;
    }

    private Vector2 ResolveDesiredSize()
    {
        if (CustomMinimumSize.X > 1f && CustomMinimumSize.Y > 1f)
        {
            return CustomMinimumSize;
        }

        if (Size.X > 1f && Size.Y > 1f)
        {
            return Size;
        }

        return DefaultButtonSize;
    }

    private void ApplyDefaultVisualState()
    {
        UpdateShaderValue(1f);
        Scale = Vector2.One;
    }

    private void ApplyImageTransform()
    {
        if (_image == null)
        {
            return;
        }

        float scaleX = _mirrorImageX ? -_imageScale.X : _imageScale.X;
        _image.PivotOffset = ResolveDesiredSize() * 0.5f;
        _image.Scale = new Vector2(scaleX, _imageScale.Y);
    }

    private void UpdateShaderValue(float value)
    {
        _hsv?.SetShaderParameter("v", value);
    }
}
