using System;
using System.Collections.Generic;
using Sekai.MusicScoreMaker.Ingame.Events;
using Sekai.MusicScoreMaker.Ingame.Models;
using Sekai.MusicScoreMaker.Ingame.Views;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sekai.MusicScoreMaker.Ingame.Utilities
{
    /// <summary>
    /// Windows平台键盘快捷键处理器
    /// 处理 Ctrl+C、Ctrl+V、Ctrl+X、Delete、上/下箭头、Ctrl+S 等快捷键
    /// </summary>
    public static class MusicScoreMakerKeyboardHandler
    {
        // 键盘操作的时间间隔常量（用于上/下箭头移动编辑位置）
        private const long ArrowKeyMoveTicksInterval = 480L; // 一个四分音符的时间单位

        /// <summary>
        /// 检测并处理键盘输入（仅在Windows平台生效）
        /// </summary>
        public static void HandleKeyboardInput()
        {
            // 仅在Windows平台处理键盘快捷键
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
            {
                return;
            }

            if (!CanHandleKeyboardInput())
            {
                return;
            }

            // 处理各种快捷键
            HandleUndoShortcut();
            HandleRedoShortcut();
            HandleCopyShortcut();
            HandlePasteShortcut();
            HandleCutShortcut();
            HandleDeleteKey();
            HandleArrowKeys();
            HandleSaveShortcut();
        }

        /// <summary>
        /// 检查是否有不完整的选择（长条或引导线部分选中）
        /// </summary>
        /// <returns>如果存在不完整选择返回true，否则返回false</returns>
        public static bool HasIncompleteSelection()
        {
            MusicScoreMakerData data = GetCurrentData();
            if (data == null)
            {
                return false;
            }

            List<int> selectedNoteIds = data.SelectedNoteIdList;
            if (selectedNoteIds == null || selectedNoteIds.Count == 0)
            {
                return false;
            }

            Dictionary<int, MusicScoreNoteBase> noteIdCache = data.GetNoteIdCacheOrRebuild();
            return MusicScoreMakerUtility.HasPartiallySelectedConnectedNotes(selectedNoteIds, noteIdCache);
        }

        /// <summary>
        /// 检查是否有任何音符被选中
        /// </summary>
        /// <returns>如果有音符被选中返回true</returns>
        public static bool HasSelectedNotes()
        {
            MusicScoreMakerData data = GetCurrentData();
            if (data == null)
            {
                return false;
            }
            return data.SelectedNoteIdList != null && data.SelectedNoteIdList.Count > 0;
        }

        /// <summary>
        /// 处理 Ctrl+C 复制快捷键
        /// </summary>
        private static void HandleCopyShortcut()
        {
            if (IsCtrlKeyPressed() && UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                if (HasIncompleteSelection())
                {
                    ShowIncompleteSelectionDialog("copy");
                    return;
                }
                MusicScoreMakerEventDispatcher.Instance?.Publish(new CopySelectedNotesAndEventsEvent());
            }
        }

        /// <summary>
        /// 处理 Ctrl+V 粘贴快捷键
        /// </summary>
        private static void HandlePasteShortcut()
        {
            if (IsCtrlKeyPressed() && UnityEngine.Input.GetKeyDown(KeyCode.V))
            {
                MusicScoreMakerEventDispatcher.Instance?.Publish(new PasteCopiedNotesAndEventsEvent());
            }
        }

        /// <summary>
        /// 处理 Ctrl+X 剪切快捷键
        /// </summary>
        private static void HandleCutShortcut()
        {
            if (IsCtrlKeyPressed() && UnityEngine.Input.GetKeyDown(KeyCode.X))
            {
                if (HasIncompleteSelection())
                {
                    ShowIncompleteSelectionDialog("cut");
                    return;
                }
                MusicScoreMakerEventDispatcher.Instance?.Publish(new CopySelectedNotesAndEventsEvent { IsCut = true });
            }
        }

        /// <summary>
        /// 处理 Delete 删除快捷键
        /// </summary>
        private static void HandleDeleteKey()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Delete))
            {
                if (HasIncompleteSelection())
                {
                    ShowIncompleteSelectionDialog("delete");
                    return;
                }
                TriggerDeleteSelectedNotes();
            }
        }

        /// <summary>
        /// 处理上/下箭头移动编辑位置
        /// </summary>
        private static void HandleArrowKeys()
        {
            long moveAmount = ArrowKeyMoveTicksInterval;

            // 上箭头 - 向上移动编辑位置（时间向后，增加ticks）
            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveFocusTicks(moveAmount);
            }

            // 下箭头 - 向下移动编辑位置（时间向前，减少ticks）
            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveFocusTicks(-moveAmount);
            }
        }

        /// <summary>
        /// 处理 Ctrl+S 保存快捷键
        /// </summary>
        private static void HandleSaveShortcut()
        {
            if (IsCtrlKeyPressed() && UnityEngine.Input.GetKeyDown(KeyCode.S))
            {
                MusicScoreMakerEventDispatcher.Instance?.Publish(new QuickSaveMusicScoreEvent());
            }
        }

        /// <summary>
        /// 处理 Ctrl+Z 撤销快捷键
        /// </summary>
        private static void HandleUndoShortcut()
        {
            MusicScoreMakerEventDispatcher dispatcher = MusicScoreMakerEventDispatcher.Instance;
            if (dispatcher.CanUndo && IsCtrlKeyPressed() && !IsShiftKeyPressed() && UnityEngine.Input.GetKeyDown(KeyCode.Z))
            {
                dispatcher.Publish(new UndoEvent());
            }
        }

        /// <summary>
        /// 处理 Ctrl+Shift+Z 恢复快捷键
        /// </summary>
        private static void HandleRedoShortcut()
        {
            MusicScoreMakerEventDispatcher dispatcher = MusicScoreMakerEventDispatcher.Instance;
            if (dispatcher.CanRedo && IsCtrlKeyPressed() && IsShiftKeyPressed() && UnityEngine.Input.GetKeyDown(KeyCode.Z))
            {
                dispatcher.Publish(new RedoEvent());
            }
        }

        private static bool CanHandleKeyboardInput()
        {
            if (!Application.isFocused
                || !MusicScoreMakerEventDispatcher.ExistsInstance
                || UnityEngine.Input.GetKey(KeyCode.LeftAlt)
                || UnityEngine.Input.GetKey(KeyCode.RightAlt)
                || UnityEngine.Input.GetMouseButton(0)
                || UnityEngine.Input.GetMouseButton(1)
                || UnityEngine.Input.GetMouseButton(2)
                || UnityEngine.Input.touchCount > 0
                || IsMusicPlaying()
                || IsEditRestricted())
            {
                return false;
            }

            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && (selected.GetComponentInParent<TMPro.TMP_InputField>() != null
                || selected.GetComponentInParent<InputField>() != null))
            {
                return false;
            }
            if (ScreenManager.ExistsInstance && ScreenManager.Instance.ExistsDialog())
            {
                return false;
            }
            foreach (SubWindowSlideAnimationController subWindow in UnityEngine.Object.FindObjectsOfType<SubWindowSlideAnimationController>(true))
            {
                if (subWindow != null && subWindow.gameObject.activeInHierarchy)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 检查Shift键是否按下
        /// </summary>
        private static bool IsShiftKeyPressed()
        {
            return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>
        /// 触发删除选中音符的操作
        /// </summary>
        private static void TriggerDeleteSelectedNotes()
        {
            MusicScoreMakerEventDispatcher.Instance?.Publish(new DeleteSelectedNotesAndEventsEvent());
        }

        /// <summary>
        /// 移动编辑位置焦点时间
        /// </summary>
        /// <param name="deltaTicks">时间偏移量</param>
        private static void MoveFocusTicks(long deltaTicks)
        {
            MusicScoreMakerEventDispatcher dispatcher = MusicScoreMakerEventDispatcher.Instance;
            if (dispatcher == null)
            {
                return;
            }

            long currentTicks = dispatcher.PublishFirst<GetFocusTicksEvent, long>(new GetFocusTicksEvent());
            long newTicks = currentTicks + deltaTicks;

            // 确保不会超出范围
            long maxTicks = dispatcher.PublishFirst<GetMusicScoreTicksMaxEvent, long>(new GetMusicScoreTicksMaxEvent());
            newTicks = Math.Max(0L, Math.Min(newTicks, maxTicks));

            dispatcher.Publish(new SetFocusTicksEvent(newTicks));
        }

        /// <summary>
        /// 检查Ctrl键是否按下
        /// </summary>
        private static bool IsCtrlKeyPressed()
        {
            return UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
        }

        /// <summary>
        /// 检查是否在播放音乐
        /// </summary>
        private static bool IsMusicPlaying()
        {
            if (!MusicScoreMakerEventDispatcher.ExistsInstance)
            {
                return false;
            }
            return MusicScoreMakerEventDispatcher.Instance.PublishFirst<IsMusicPlayingEvent, bool>(new IsMusicPlayingEvent());
        }

        /// <summary>
        /// 检查是否编辑受限
        /// </summary>
        private static bool IsEditRestricted()
        {
            if (!MusicScoreMakerEventDispatcher.ExistsInstance)
            {
                return false;
            }
            return MusicScoreMakerEventDispatcher.Instance.PublishFirst<IsEditRestrictedEvent, bool>(new IsEditRestrictedEvent());
        }

        /// <summary>
        /// 获取当前谱面数据
        /// </summary>
        private static MusicScoreMakerData GetCurrentData()
        {
            if (!MusicScoreMakerEventDispatcher.ExistsInstance)
            {
                return null;
            }
            return MusicScoreMakerEventDispatcher.Instance.PublishFirst<GetMusicScoreMakerDataEvent, MusicScoreMakerData>(new GetMusicScoreMakerDataEvent());
        }

        /// <summary>
        /// 显示不完整选择提示对话框
        /// </summary>
        /// <param name="action">操作名称（copy/cut/delete）</param>
        private static void ShowIncompleteSelectionDialog(string action)
        {
            MusicScoreMakerEventDispatcher.Instance?.Publish(new ShowIncompleteSelectionWarningEvent(action));
        }
    }
}
