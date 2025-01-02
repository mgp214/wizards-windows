using System;
using Godot;

// Disables compiler warnings about unused or private fields in this snippet.
#pragma warning disable CS0649, IDE0044

/// <summary>
/// CleanlinessMeter is a Node2D that visually represents a "grimy" percentage of an area.
/// Uses a noise function (FastNoiseLite) to generate grime color patterns.
/// </summary>
public class CleanlinessMeter : Node2D, Manager
{
    // ------------------------------------------------------------------------
    // Fields & Properties
    // ------------------------------------------------------------------------
    
    // The 2D noise generator for the "grime" effect
    private FastNoiseLite _noise;

    /// <summary>
    /// Frequency for the noise generator. Exposed in editor, can be tweaked.
    /// </summary>
    [Export]
    private float _frequency = 1.0f;
    
    private Sprite _background;
    private Sprite _foreground;
    
    // The two main colors used for interpolation when generating grime.
    private Color _grimeColor1;
    private Color _grimeColor2;

    // 2D array storing the color results of the noise-based interpolation.
    private Color[,] _grimeNoiseValues;

    // Keep track of the last known "percent grimy" from GrimeManager to avoid redundant updates.
    private float _lastPercentGrimy;

    // Singleton-like static accessor
    public static CleanlinessMeter Instance { get; private set; }

    /// <summary>
    /// Shortcut: The width is determined from the 0th dimension of _grimeNoiseValues.
    /// </summary>
    private int Width => _grimeNoiseValues.GetLength(0);

    /// <summary>
    /// Shortcut: The height is determined from the 1st dimension of _grimeNoiseValues.
    /// </summary>
    private int Height => _grimeNoiseValues.GetLength(1);

    // ------------------------------------------------------------------------
    // Lifecycle Methods
    // ------------------------------------------------------------------------
    
    public override void _Ready()
    {
        _lastPercentGrimy = 0f;
        Instance = this;

        // Retrieve nodes from scene
        _background = GetNodeOrNull<Sprite>("background");
        _foreground = GetNodeOrNull<Sprite>("foreground");

        if (_background == null || _foreground == null)
        {
            GD.PrintErr("[CleanlinessMeter] Missing background/foreground sprite nodes.");
            return;
        }

        // Initialize noise with user-defined frequency
        _noise = new FastNoiseLite();
        _noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _noise.SetFrequency(_frequency);
        _noise.SetFractalType(FastNoiseLite.FractalType.FBm);

        // We assume Scale.x & Scale.y in the background indicates the texture or "grime" dimension.
        // Adjust if you prefer a more direct approach (e.g., background.Texture.GetWidth()).
        int width = Mathf.Max(1, (int)_background.Scale.x);
        int height = Mathf.Max(1, (int)_background.Scale.y);

        _grimeNoiseValues = new Color[width, height];

        // Register readiness with the manager system
        ManagerManager.Instance?.ReportReady(this);
    }

    public override void _Process(float delta)
    {
        // Compare current "PercentGrimy" from GrimeManager
        // If changed, we call Update() to trigger _Draw
        float currentPercent = GrimeManager.Instance?.PercentGrimy ?? 0f;
        if (!Mathf.IsEqualApprox(currentPercent, _lastPercentGrimy))
        {
            _lastPercentGrimy = currentPercent;
            Update();
        }
    }

    /// <summary>
    /// Renders the updated grime texture if the manager system is fully ready,
    /// and if there's a difference in the "percent grimy."
    /// </summary>
    public override void _Draw()
    {
        // We only proceed if everything is set up
        if (!ManagerManager.Instance?.HasCalledOnAllReady ?? true)
            return;

        // Build the "grime" image only in the portion that is considered "dirty"
        // minXToDraw is the portion from which we start applying grime color
        var grimeImage = new Image();
        grimeImage.Create(Width, Height, false, Image.Format.Rgba8);
        grimeImage.Fill(Colors.Transparent);
        grimeImage.Lock();

        int minXToDraw = (int)((1f - _lastPercentGrimy) * Width);
        minXToDraw = Mathf.Clamp(minXToDraw, 0, Width);

        for (int x = minXToDraw; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                grimeImage.SetPixel(x, y, _grimeNoiseValues[x, y]);
            }
        }
        grimeImage.Unlock();

        // Convert to texture and assign to the foreground
        var texture = new ImageTexture();
        texture.CreateFromImage(grimeImage);

        if (_foreground != null)
        {
            _foreground.Texture = texture;
        }
    }

    // ------------------------------------------------------------------------
    // Manager Interface Implementation
    // ------------------------------------------------------------------------

    /// <summary>
    /// Called when all managers are ready; we generate the noise-based color array.
    /// </summary>
    public void OnAllReady()
    {
        // Defensive check for GrimeManager
        if (GrimeManager.Instance == null)
        {
            GD.PrintErr("[CleanlinessMeter] GrimeManager is null. Aborting noise generation.");
            return;
        }

        // Get random pair of colors from GrimeManager
        _grimeColor1 = GrimeManager.Instance.GetRandomGrimeColor(Colors.Transparent);
        _grimeColor2 = GrimeManager.Instance.GetRandomGrimeColor(_grimeColor1);

        // Fill _grimeNoiseValues with interpolated color based on noise
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                float noiseValue = _noise.GetNoise(x, y);
                float t = (1f + noiseValue) / 2f; // remap from [-1,1] to [0,1]

                _grimeNoiseValues[x, y] = ColorInterpolation.LerpHSV(_grimeColor1, _grimeColor2, t);
            }
        }
    }

    // ------------------------------------------------------------------------
    // Helper Methods
    // ------------------------------------------------------------------------

    /// <summary>
    /// Safe node retrieval to avoid repeated null checks.
    /// </summary>
    private T GetNodeOrNull<T>(string path) where T : class
    {
        var node = GetNodeOrNull(path);
        return node as T;
    }
}
