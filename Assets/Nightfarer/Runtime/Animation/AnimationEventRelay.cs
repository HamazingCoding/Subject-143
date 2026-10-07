using System;
using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Sits next to the Animator and receives clip events. Add an AnimationEvent calling NFEvent with a
    /// string ("HitStart", "HitEnd", "Footstep", ...) to any clip; attacks with useAnimationEvents use
    /// HitStart/HitEnd from the clip instead of their data timings.
    /// </summary>
    public class AnimationEventRelay : MonoBehaviour
    {
        public event Action<string> EventRaised;

        // Called by AnimationEvents (function name "NFEvent", string parameter).
        public void NFEvent(string eventName) => EventRaised?.Invoke(eventName);
    }
}
