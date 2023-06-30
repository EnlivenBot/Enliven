using System;

namespace Bot.DiscordRelated.Commands;

public class GroupingAttribute : Attribute {
    public GroupingAttribute(string groupName) {
        GroupName = groupName;
    }
    public string GroupName { get; set; }
}