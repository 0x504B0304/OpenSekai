#if UNITY_INCLUDE_TESTS
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Sekai.Core.Live;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using Phase = UnityEngine.InputSystem.TouchPhase;

namespace Sekai.EditorTools.Tests
{
    public sealed class LiveTouchLifecycleEditModeTests
    {
        [TestCase(Phase.Ended, false)]
        [TestCase(Phase.Ended, true)]
        [TestCase(Phase.Canceled, false)]
        [TestCase(Phase.Canceled, true)]
        public void FiveHeldFingersDoNotBlockRepeatedPressesFromOtherFive(Phase release, bool sameTimestamp)
        {
            bool previouslyEnabled = EnhancedTouchSupport.enabled;
            var logic = new LiveLogic(null);
            var screen = InputSystem.AddDevice<Touchscreen>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var should = typeof(LiveLogic).GetMethod("ShouldProcessTouch", flags);
            var execute = typeof(LiveLogic).GetMethod("TouchExecution", flags);
            var remember = typeof(LiveLogic).GetMethod("UpdateLastTouch", flags);
            double eventTime = InputState.currentTime;

            int Process()
            {
                int accepted = 0;
                foreach (var touch in Touch.activeTouches)
                {
                    if (!(bool)should.Invoke(logic, new object[] { touch })) continue;
                    object[] args = { touch, accepted };
                    execute.Invoke(logic, args);
                    accepted = (int)args[1];
                    remember.Invoke(logic, new object[] { touch });
                }
                return accepted;
            }

            void Send(int first, int last, Phase phase)
            {
                if (!sameTimestamp) eventTime += 0.01;
                for (int id = first; id <= last; id++)
                    InputSystem.QueueStateEvent(screen, new TouchState
                    {
                        touchId = id, phase = phase, position = new Vector2(id * 100, 100)
                    }, eventTime);
                InputSystem.Update();
            }

            try
            {
                Send(1, 10, Phase.Began);
                Assert.That(Process(), Is.EqualTo(10));
                Assert.That(logic.TapCount, Is.EqualTo(10));
                for (int cycle = 1; cycle <= 3; cycle++)
                {
                    Send(6, 10, release);
                    Process();
                    Assert.That(Process(), Is.EqualTo(5), "Ended/canceled contacts must not be dispatched twice.");
                    InputSystem.Update();
                    Send(6, 10, Phase.Began);
                    Assert.That(Touch.activeTouches.Count, Is.EqualTo(10));
                    Assert.That(Touch.activeTouches.Count(t => t.touchId >= 6 && (bool)should.Invoke(logic, new object[] { t })), Is.EqualTo(5));
                    Assert.That(Process(), Is.EqualTo(10));
                    Assert.That(logic.TapCount, Is.EqualTo(10 + 5 * cycle), "A reused ID must remain a new press, even at the same timestamp.");
                    Process();
                    Assert.That(logic.TapCount, Is.EqualTo(10 + 5 * cycle), "Repeated logical updates must not count the same press twice.");
                }
            }
            finally
            {
                InputSystem.RemoveDevice(screen);
                if (!previouslyEnabled) EnhancedTouchSupport.Disable();
            }
        }
    }
}
#endif
