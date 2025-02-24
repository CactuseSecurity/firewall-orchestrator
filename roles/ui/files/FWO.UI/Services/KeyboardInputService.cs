using Microsoft.AspNetCore.Components.Web;
using FWO.Ui.Data;

public class KeyboardInputService
{
    private readonly List<(Action<KeyboardEventArgs>, Action<KeyboardEventArgs>)> subscribers = new();

    /// <summary>
    /// Adds callback pairs to the internal subscriber list.
    /// </summary>
    public void Register(Action<KeyboardEventArgs> callbackUp, Action<KeyboardEventArgs> callbackDown)
    {
        if (!subscribers.Contains((callbackUp, callbackDown)))
        {
            subscribers.Add((callbackUp, callbackDown));
        }
    }

    /// <summary>
    /// Removes callback pairs from the internal subscriber list.
    /// </summary>
    public void Unregister(Action<KeyboardEventArgs> callbackUp, Action<KeyboardEventArgs> callbackDown)
    {

            subscribers.Remove((callbackUp, callbackDown));
    }

    /// <summary>
    /// Invokes all subscribed key up callbacks.
    /// </summary>
    public void NotifyKeyUp(KeyboardEventArgs e)
    {
        foreach (var subscriber in subscribers)
        {
            subscriber.Item1.Invoke(e);
        }
    }

    /// <summary>
    /// Invokes all subscribed key down callbacks.
    /// </summary>
    public void NotifyKeyDown(KeyboardEventArgs e)
    {
        foreach (var subscriber in subscribers)
        {
            subscriber.Item2.Invoke(e);
        }
    }
}
