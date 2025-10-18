using System;

public static class DeliveryWeatherPause
{
    public static bool IsRaining { get; private set; }

    public static event Action OnRainStarted;
    public static event Action OnRainStopped;

    public static void RaiseRainStarted()
    {
        if (IsRaining) return;
        IsRaining = true;
        OnRainStarted?.Invoke();
    }

    public static void RaiseRainStopped()
    {
        if (!IsRaining) return;
        IsRaining = false;
        OnRainStopped?.Invoke();
    }
}
