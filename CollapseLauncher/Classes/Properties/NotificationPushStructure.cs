using CollapseLauncher.Extension;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hi3Helper.Shared.ClassStruct
{
    public class NotificationProp
    {
        public int MsgId { get; set; }
        public bool? IsClosable { get; set; }
        public bool? IsDisposable { get; set; }
        public bool? Show { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public NotifSeverity Severity { get; set; }
        public string RegionProfile { get; set; }
        public string ValidForVerAbove { get; set; }
        public string ValidForVerBelow { get; set; }
#nullable enable
        public object? OtherUIElement { get; set; }
        public DateTime? TimeBegin { get; set; }
        public DateTime? TimeEnd { get; set; }

        [JsonConverter(typeof(NotificationActionConverter))]
        public NotificationActionBase? ActionProperty { get; set; }
        public bool IsForceShowNotificationPanel { get; set; }
#nullable disable
    }

    [JsonConverter(typeof(JsonStringEnumConverter<NotificationActionTypeDesc>))]
    public enum NotificationActionTypeDesc
    {
        ClickLink = 0
    }

    public class NotificationActionBase
    {
        public NotificationActionTypeDesc? ActionType { get; set; }
        public Type BaseClassType { get; set; }

        public virtual FrameworkElement GetFrameworkElement()
        {
            return null;
        }

        public virtual void BuildFrameworkElement(ref Utf8JsonReader reader) { }
    }

    public class NotificationActionConverter : JsonConverter<NotificationActionBase>
    {
        public override bool CanConvert(Type type)
        {
            return typeof(NotificationActionBase).IsAssignableFrom(type);
        }

        public override NotificationActionBase Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // Check if the token is a start object
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            NotificationActionBase returnValue = new NotificationActionBase();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return returnValue;
                }

                // Check and get the property name
                string section = reader.GetString();
                reader.Read();

                switch (section)
                {
                    case "ActionType":
                        {
                            section = reader.GetString();
                            // For performance, parse with ignoreCase:false first.
                            if (!Enum.TryParse(section, ignoreCase: false, out NotificationActionTypeDesc actionType) &&
                                !Enum.TryParse(section, ignoreCase: true, out actionType))
                            {
                                // Skip if the action is not supported
                                reader.Skip();
                                reader.Skip();
                                break;
                            }
                            returnValue.ActionType = actionType;
                        }
                        break;
                    case "ActionValue":
                        {
                            switch (returnValue.ActionType)
                            {
                                case NotificationActionTypeDesc.ClickLink:
                                    // Build the UI element
                                    returnValue = new NotificationActionClickLink();
                                    returnValue.BuildFrameworkElement(ref reader);
                                    break;
                                default:
                                    reader.Skip();
                                    break;
                            }
                        }
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return returnValue;
        }

        public override void Write(
                Utf8JsonWriter writer,
                NotificationActionBase baseType,
                JsonSerializerOptions options)
        {
            throw new JsonException($"Serializing is not supported!");
        }
    }

    public class NotificationActionClickLink : NotificationActionBase
    {
        public string URL { get; set; }
        public string Description { get; set; }
#nullable enable
        public string? GlyphIcon { get; set; }
        public string? GlyphFont { get; set; }
#nullable disable

        public NotificationActionClickLink()
        {
            // Assign the class type
            BaseClassType = typeof(NotificationActionClickLink);
        }

        public override FrameworkElement GetFrameworkElement() => NotificationPush.GenerateNotificationButton(GlyphIcon, Description, (_, _) =>
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = URL
                }
            }.Start();
        }, GlyphFont);

        public override void BuildFrameworkElement(ref Utf8JsonReader reader)
        {
            // Do loop while the reader == true
            while (reader.Read())
            {
                // If it's already at the end, then return
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return;
                }

                // Start reading the property
                string section = reader.GetString();
                switch (section)
                {
                    case "URL":
                        URL = ReadString(ref reader);
                        break;
                    case "Description":
                        Description = ReadString(ref reader);
                        break;
                    case "GlyphIcon":
                        GlyphIcon = ReadString(ref reader);
                        break;
                    case "GlyphFont":
                        GlyphFont = ReadString(ref reader);
                        break;
                }
            }
        }

        private string ReadString(ref Utf8JsonReader reader)
        {
            reader.Read();
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Null => null,
                _ => throw new JsonException("Value must be a string type or null!")
            };
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<NotifSeverity>))]
    public enum NotifSeverity : uint
    {
        Informational,
        Success,
        Warning,
        Error
    }

    public class NotificationPush
    {
        public List<NotificationProp> AppPush { get; set; } = new List<NotificationProp>();
        public List<NotificationProp> RegionPush { get; set; } = new List<NotificationProp>();
        public List<int> AppPushIgnoreMsgIds { get; set; } = new List<int>();
        public List<int> RegionPushIgnoreMsgIds { get; set; } = new List<int>();
        public List<int> CurrentShowMsgIds { get; set; } = new List<int>();

        public void AddIgnoredMsgIds(int MsgId, bool IsAppPush = true)
        {
            if (IsAppPush ? !AppPushIgnoreMsgIds.Contains(MsgId) : !RegionPushIgnoreMsgIds.Contains(MsgId))
            {
                if (IsAppPush)
                {
                    AppPushIgnoreMsgIds.Add(MsgId);
                }
                else
                {
                    RegionPushIgnoreMsgIds.Add(MsgId);
                }
            }
        }

        public void RemoveIgnoredMsgIds(int MsgId, bool IsAppPush = true)
        {
            if (IsAppPush ? AppPushIgnoreMsgIds.Contains(MsgId) : RegionPushIgnoreMsgIds.Contains(MsgId))
            {
                if (IsAppPush)
                {
                    AppPushIgnoreMsgIds.Remove(MsgId);
                }
                else
                {
                    RegionPushIgnoreMsgIds.Remove(MsgId);
                }
            }
        }

        public bool IsMsgIdIgnored(int MsgId) => AppPushIgnoreMsgIds.Contains(MsgId) || RegionPushIgnoreMsgIds.Contains(MsgId);

        public void EliminatePushList()
        {
            AppPush?.RemoveAll(x => AppPushIgnoreMsgIds.Any(y => x.MsgId == y));
            RegionPush?.RemoveAll(x => RegionPushIgnoreMsgIds.Any(y => x.MsgId == y));
        }

        public static Button GenerateNotificationButton(string IconGlyph, string Text, RoutedEventHandler ButtonAction = null, string FontIconName = "FontAwesomeSolid", string ButtonStyle = "AccentButtonStyle")
        {
            Button Btn =
                UIElementExtensions.CreateButtonWithIcon<Button>(
                    Text,
                    IconGlyph,
                    FontIconName,
                    ButtonStyle
                )
                .WithMargin(0d, 0d, 0d, 8d);

            if (ButtonAction != null) Btn.Click += ButtonAction;
            return Btn;
        }
    }
}
