using System.Runtime.CompilerServices;

namespace Sekai.MusicScoreMaker.Ingame.Events
{
    /// <summary>
    /// 显示不完整选择警告对话框的事件
    /// 用于提示用户长条或引导线未完整选中
    /// </summary>
    public class ShowIncompleteSelectionWarningEvent : MusicScoreMakerDispatcherEventBase
    {
        /// <summary>
        /// 操作类型（copy/cut/delete）
        /// </summary>
        public string ActionType
        {
            [CompilerGenerated]
            get;
            [CompilerGenerated]
            set;
        }

        public ShowIncompleteSelectionWarningEvent(string actionType = "")
        {
            ActionType = actionType;
        }
    }
}