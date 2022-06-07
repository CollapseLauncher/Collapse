using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace Hi3Helper.Shared.ClassStruct
{
    public class NotificationProp
    {
        public int MsgId { get; set; }
        public bool? IsClosable { get; set; }
        public bool? IsDisposable { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public InfoBarSeverity Severity { get; set; }
        public string RegionProfile { get; set; }
        public string ValidForVerAbove { get; set; }
        public string ValidForVerBelow { get; set; }
    }

    public class NotificationPush
    {
        public List<NotificationProp> AppPush { get; set; }
        public List<NotificationProp> RegionPush { get; set; }
        public List<int> AppPushIgnoreMsgIds { get; set; }
        public List<int> RegionPushIgnoreMsgIds { get; set; }

        public void AddIgnoredMsgIds(int MsgId, bool IsAppPush = true)
        {
            if ((IsAppPush ? !AppPushIgnoreMsgIds.Contains(MsgId) : !RegionPushIgnoreMsgIds.Contains(MsgId)))
                if (IsAppPush) AppPushIgnoreMsgIds.Add(MsgId); else RegionPushIgnoreMsgIds.Add(MsgId);
        }

        public void RemoveIgnoredMsgIds(int MsgId, bool IsAppPush = true)
        {
            if ((IsAppPush ? AppPushIgnoreMsgIds.Contains(MsgId) : RegionPushIgnoreMsgIds.Contains(MsgId)))
                if (IsAppPush) AppPushIgnoreMsgIds.Remove(MsgId); else RegionPushIgnoreMsgIds.Remove(MsgId);
        }

        public void EliminatePushList()
        {
            if (AppPush != null || RegionPush != null)
            {
                AppPush.RemoveAll(x => AppPushIgnoreMsgIds.Any(y => x.MsgId == y));
                RegionPush.RemoveAll(x => RegionPushIgnoreMsgIds.Any(y => x.MsgId == y));
            }
        }
    }
}
