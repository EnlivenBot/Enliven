using System;

namespace Common.Config;

public struct LavalinkNodeInfo
{
    public Uri RestUri { get; set; }
    public string Password { get; set; }
    public string Name { get; set; }
}