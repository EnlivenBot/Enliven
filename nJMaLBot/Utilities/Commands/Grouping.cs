using System;

namespace Bot.Utilities.Commands {
    public class GroupingAttribute : Attribute {
        public string GroupName { get; set; }
        public GroupingAttribute(string groupName) {
            GroupName = groupName;
        }
    }
}