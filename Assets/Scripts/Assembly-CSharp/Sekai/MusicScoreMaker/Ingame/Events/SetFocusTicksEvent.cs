using System.Runtime.CompilerServices;

namespace Sekai.MusicScoreMaker.Ingame.Events
{
    /// <summary>
    /// 设置焦点时间的事件
    /// </summary>
    public class SetFocusTicksEvent : MusicScoreMakerDispatcherEventBase
    {
        /// <summary>
        /// 要设置的焦点时间（ticks）
        /// </summary>
        public long Ticks
        {
            [CompilerGenerated]
            get;
            [CompilerGenerated]
            set;
        }

        public SetFocusTicksEvent(long ticks = 0L)
        {
            Ticks = ticks;
        }
    }
}