using System;
using System.Threading;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaLiveAudioAnalyzer;

public partial class AnimatedPopup : ContentControl
{
    #region Private Members

    /// <summary>
    /// The underlay control for closing this popup
    /// </summary>
    private Control _underlayControl;

    /// <summary>
    /// Indicates if this is the first time we are animating
    /// </summary>
    private bool _firstAnimation = true;
    
    /// <summary>
    /// Indicates if we have captured the opacity value yet
    /// </summary>
    private bool _opacityCaptured;
    
    /// <summary>
    /// Store the controls original Opacity value at startup
    /// </summary>
    private double _originalOpacity;

    /// <summary>
    /// Get a 60 FPS timespan
    /// </summary>
    private readonly TimeSpan _frameRate = TimeSpan.FromSeconds(1 / 60.0);
    
    /// <summary>
    /// Calculate total ticks that make up the animation time
    /// </summary>
    private int TotalTicks  => (int)(_animationTime.TotalSeconds / _frameRate.TotalSeconds);
    
    /// <summary>
    /// Store the controls desired size
    /// </summary>
    private Size _desiredSize;
    
    /// <summary>
    /// A flag for when we are animating
    /// </summary>
    private bool _animating;

    /// <summary>
    /// Keeps track of if we have found the desired 100% width/height auto size
    /// </summary>
    private bool _sizeFound;
    
    /// <summary>
    /// The animation UI timer
    /// </summary>
    private readonly DispatcherTimer _animationTimer;
    
    /// <summary>
    /// The timeout timer to detect when auto-sizing has finished firing
    /// </summary>
    private Timer _sizingTimer;
    
    /// <summary>
    /// The current position in the animation
    /// </summary>
    private int _animationCurrentTick;

    #endregion

    #region Public Properties

    /// <summary>
    /// Indicates if the control is currently opened
    /// </summary>
    public bool IsOpened => _animationCurrentTick >= TotalTicks;

    #region Open

    private bool _open;
    
    public static readonly DirectProperty<AnimatedPopup, bool> OpenProperty = AvaloniaProperty.RegisterDirect<AnimatedPopup, bool>(
        nameof(Open), o => o.Open, (o, v) => o.Open = v);

    /// <summary>
    /// Property to set whether the control should be open or closed
    /// </summary>
    public bool Open
    {
        get => _open;
        set
        {
            // If the value has not changed...
            if(value == _open)
                // Do nothing
                return;
            
            // If we are opening...
            if (value)
            {
                // If the parent is a grid...
                if (Parent is Grid grid)
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Set grid row/column span
                        if(grid.RowDefinitions?.Count > 0)
                            _underlayControl.SetValue(Grid.RowProperty, grid.RowDefinitions.Count);
                
                        if(grid.ColumnDefinitions?.Count > 0)
                            _underlayControl.SetValue(Grid.ColumnProperty, grid.ColumnDefinitions.Count);
                
                        // Insert the underlay control 
                        if(!grid.Children.Contains(_underlayControl))
                            grid.Children.Insert(0, _underlayControl);
                    });
                }
            }
            // If closing...
            else
            {
                // If the control is currently fully open...
                if(IsOpened)
                    // Update desired size
                    UpdateDesiredSize();
            }
            
            // Update animation
            UpdateAnimation();
            
            // Raise the property changed event
            SetAndRaise(OpenProperty, ref _open, value);
        }
    }

    #endregion

    #region Animation Time
    
    private TimeSpan _animationTime = TimeSpan.FromSeconds(3);

    public static readonly DirectProperty<AnimatedPopup, TimeSpan> AnimationTimeProperty =
        AvaloniaProperty.RegisterDirect<AnimatedPopup, TimeSpan>(nameof(AnimationTime), o => o.AnimationTime,
            (o, v) => o.AnimationTime = v);

    public TimeSpan AnimationTime
    {
        get => _animationTime;
        set => SetAndRaise(AnimationTimeProperty, ref _animationTime, value);
    }
    
    #endregion
    
    #region Animate Opacity

    private bool _animateOpacity = true;

    public static readonly DirectProperty<AnimatedPopup, bool> AnimateOpacityProperty = AvaloniaProperty.RegisterDirect<AnimatedPopup, bool>(
        nameof(AnimateOpacity), o => o.AnimateOpacity, (o, v) => o.AnimateOpacity = v);

    public bool AnimateOpacity
    {
        get => _animateOpacity;
        set => SetAndRaise(AnimateOpacityProperty, ref _animateOpacity, value);
    }
    
    #endregion

    #region Underlay Opacity
    
    private double _underlayOpacity = 0.2;
    
    public static readonly DirectProperty<AnimatedPopup, double> UnderlayOpacityProperty = AvaloniaProperty.RegisterDirect<AnimatedPopup, double>(
        nameof(UnderlayOpacity), o => o.UnderlayOpacity, (o, v) => o.UnderlayOpacity = v);

    public double UnderlayOpacity
    {
        get => _underlayOpacity;
        set => SetAndRaise(UnderlayOpacityProperty, ref _underlayOpacity, value);
    }
    
    #endregion

    #endregion

    #region Public Commands

    [RelayCommand]
    public void BeginOpen()
    {
        Open = true;
    }

    [RelayCommand]
    public void BeginClose()
    {
        Open = false;
    }

    #endregion

    #region Constructor
    
    /// <summary>
    /// Default constructor
    /// </summary>
    public AnimatedPopup()
    {
        // Make a new underlay control
        _underlayControl = new Border
        {
            Background = Brushes.Black,
            Opacity = 0,
            ZIndex = 9
        };
        
        // On press, close popup
        _underlayControl.PointerPressed += (sender, args) =>
        {
            BeginClose();
        };
        
        _animationTimer = new DispatcherTimer
        {
            // Set the timer to run 60 times a second
            Interval = _frameRate,
        };

        _sizingTimer = new Timer(t =>
        {
            // If we have already calculated the size...
            if (_sizeFound)
                // No longer accept new sizes
                return;
            
            // We have now found our desired size
            _sizeFound = true;

            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Update the desired size
                UpdateDesiredSize();
                
                // Update animation
                UpdateAnimation();
            });
        });
        
        // Callback on every tick
        _animationTimer.Tick += (s, e) => AnimationTick();
    }
    
    #endregion

    #region Private Methods

    /// <summary>
    /// Updates the animation desired size based on the current visuals desired size
    /// </summary>
    private void UpdateDesiredSize() => _desiredSize = DesiredSize - Margin;

    /// <summary>
    /// Calculate and start any new required animation
    /// </summary>
    private void UpdateAnimation()
    {
        // Do nothing if we still haven't found our initial size
        if (!_sizeFound)
            return;
        
        // Start the animation thread again
        _animationTimer.Start();
    }

    /// <summary>
    /// Should be called when on open or close transition has complete
    /// </summary>
    private void AnimationComplete()
    {
        // If open...
        if (_open)
        {
            // Set size to desired size
            Width = double.NaN;
            Height = double.NaN;

            // Make sure opacity is set to original value
            Opacity = _originalOpacity;
        }
        // If closed...
        else
        {
            // Set size to 0
            Width = 0;
            Height = 0;
            
            // If the parent is a grid...
            if (Parent is Grid grid)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Reset opacity
                    _underlayControl.Opacity = 0;
                
                    // Remove underlay
                    if (grid.Children.Contains(_underlayControl))
                        grid.Children.Remove(_underlayControl);
                });
            }
        }
        
        Width = _open ? _desiredSize.Width : 0;
        Height = _open ? _desiredSize.Height : 0;
    }

    /// <summary>
    /// Update controls sizes based on the next tick of on animation
    /// </summary>
    private void AnimationTick()
    {
        // If this is the first call after calculating the desired size...
        if (_firstAnimation)
        {
            // Clear the flag
            _firstAnimation = false;
            
            // Stop this animation timer
            _animationTimer.Stop();
            
            // Reset opacity
            Opacity = _originalOpacity;
            
            // Set the final size
            AnimationComplete();
            
            // Do on this tick
            return;
        }
            
        // If we have reached the end of our animation...
        if ((_open && _animationCurrentTick >= TotalTicks) ||
            (!_open && _animationCurrentTick == 0))
        {
            // Stop this animation timer
            _animationTimer.Stop();
            
            // Set the final size
            AnimationComplete();
            
            // Clear animating flag
            _animating = false;
                
            // Break out of code
            return;
        }
        
        // Set animating flag
        _animating = true;
        
        // None the tick in the right direction
        _animationCurrentTick += _open ? 1 : -1;
            
        // Get percentage of the way through the current animation
        var percentageAnimated = (float) _animationCurrentTick / TotalTicks;
            
        // Make an animation easing
        var quadraticEasing = new QuadraticEaseIn();
        var linearEasing = new LinearEasing();
            
        // Calculate final width and height
        var finalWidth = _desiredSize.Width * quadraticEasing.Ease(percentageAnimated);
        var finalHeight = _desiredSize.Height * quadraticEasing.Ease(percentageAnimated);
            
        // Do our animation
        Width = finalWidth;
        Height = finalHeight;
        
        // Animate opacity
        if(AnimateOpacity)
            Opacity = _originalOpacity * linearEasing.Ease(percentageAnimated);
        
        // Animate underlay
        _underlayControl.Opacity = _underlayOpacity * quadraticEasing.Ease(percentageAnimated);
            
        Console.WriteLine($"Current tick: {_animationCurrentTick}");
    }

    #endregion

    public override void Render(DrawingContext context)
    {
        // If we have not yet found the desired size...
        if (!_sizeFound)
        {
            // If we have not yet captured the opacity
            if (!_opacityCaptured)
            {
                // Set flag to true
                _opacityCaptured = true;

                // Remember original controls opacity
                _originalOpacity = Opacity;

                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Hide control
                    Opacity = 0;
                });
            }

            _sizingTimer.Change(100, int.MaxValue);
        }

        base.Render(context);
    }
}